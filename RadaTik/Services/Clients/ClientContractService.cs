using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Clients;
using RadaTik.Domain.Common;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class ClientContractService(
    ApplicationDbContext context,
    IClientRenewalGuardService renewalGuardService)
    : ApplicationServiceBase(context), IClientContractService
{
    private const string ContractTemplateServiceKey = "CONTRACT_TEMPLATE";
    private const string ContractMetaServiceKey = "CONTRACT_META";

    public string DefaultTemplateBody { get; } = @"
<p>تم إبرام هذا العقد بين كل من:</p>
<p><strong>الطرف الأول:</strong> شركة/شبكة {{NetworkName}} ويُشار إليها لاحقاً بـ (الشركة).</p>
<p><strong>الطرف الثاني:</strong> المشترك السيد/السيدة {{SubscriberName}} رقم المشترك {{SubscriberNumber}}، ويُشار إليه لاحقاً بـ (المشترك).</p>
<p>اتفق الطرفان على تزويد المشترك بخدمة الاتصال وفق الباقة المعتمدة {{ProfileName}} ابتداءً من تاريخ الاشتراك {{SubscriptionStartDate}}.</p>
<p>يلتزم المشترك بسداد الرسوم الدورية في مواعيدها، والمحافظة على تجهيزات الخدمة، وعدم إساءة استخدام الاتصال بما يخالف الأنظمة المعمول بها.</p>
<p>تحتفظ الشركة بحقها في تحديث الإجراءات الفنية والتنظيمية بما يضمن جودة الخدمة واستمراريتها.</p>
<p>يُعد توقيع المشترك أدناه موافقة صريحة على بنود هذا العقد.</p>";

    public async Task<ClientContractMeta> GetMetaAsync(int networkId, CancellationToken ct = default)
    {
        CustomServiceItem? item = await Db.CustomServiceItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == ContractMetaServiceKey, ct);

        if (item == null || string.IsNullOrWhiteSpace(item.Body))
        {
            return new ClientContractMeta();
        }

        try
        {
            return JsonSerializer.Deserialize<ClientContractMeta>(item.Body) ?? new ClientContractMeta();
        }
        catch
        {
            return new ClientContractMeta();
        }
    }

    public async Task<string> GetTemplateBodyAsync(int networkId, CancellationToken ct = default)
    {
        CustomServiceItem? item = await Db.CustomServiceItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == ContractTemplateServiceKey, ct);

        if (item == null || string.IsNullOrWhiteSpace(item.Body))
        {
            return DefaultTemplateBody;
        }

        return item.Body;
    }

    public ClientContractPrintViewData BuildPrintView(
        Client client,
        ClientContractMeta meta,
        string templateBody,
        DateTime contractDate)
    {
        string rendered = ContractTemplateRenderer.Render(templateBody, client, contractDate);
        return new ClientContractPrintViewData
        {
            ContractDate = contractDate,
            ContractTitle = string.IsNullOrWhiteSpace(meta.ContractTitle)
                ? "عقد انضمام إلى الشركة / الشبكة"
                : meta.ContractTitle,
            RecordNumber = string.IsNullOrWhiteSpace(meta.RecordNumber) ? "-" : meta.RecordNumber,
            LicenseNumber = string.IsNullOrWhiteSpace(meta.LicenseNumber) ? "-" : meta.LicenseNumber,
            BodyHtml = rendered
        };
    }

    public ClientContractTemplateSettingsViewData BuildTemplateSettingsView(
        Network network,
        ClientContractMeta meta,
        string templateBody)
    {
        Client sampleClient = new()
        {
            Name = "اسم مشترك تجريبي",
            SID = "000000",
            UserName = "test-user",
            CreatedDate = DateTime.Today,
            ServiceStartDate = DateTime.Today,
            AccountExpirationDate = DateTime.Today.AddMonths(1),
            Profile = new Profile { Name = "باقة تجريبية" },
            Network = network
        };

        return new ClientContractTemplateSettingsViewData
        {
            AvailableVariables = ContractTemplateRenderer.VariableLabels,
            VariableSyntaxHint = "اكتب المتغير بهذا الشكل: {{VariableName}}",
            PreviewHtml = ContractTemplateRenderer.Render(templateBody, sampleClient, DateTime.Now),
            ContractTitle = string.IsNullOrWhiteSpace(meta.ContractTitle)
                ? "عقد انضمام إلى الشركة / الشبكة"
                : meta.ContractTitle,
            RecordNumber = meta.RecordNumber,
            LicenseNumber = meta.LicenseNumber,
            ContractBodyTemplate = templateBody,
            DefaultContractBodyTemplate = DefaultTemplateBody
        };
    }

    public IReadOnlyList<string> ValidateTemplateVariables(string? templateBody) =>
        ContractTemplateRenderer.FindUnknownVariables(templateBody, ContractTemplateRenderer.VariableLabels.Keys);

    public async Task SaveSettingsAsync(int networkId, ClientContractMeta meta, string templateBody, CancellationToken ct = default)
    {
        await UpsertCustomServiceItemAsync(networkId, ContractMetaServiceKey, "إعدادات ميتا عقد الانضمام", JsonSerializer.Serialize(meta), ct);
        await UpsertCustomServiceItemAsync(networkId, ContractTemplateServiceKey, "قالب نص عقد الانضمام", templateBody, ct);
    }

    public Task ResetTemplateToDefaultAsync(int networkId, CancellationToken ct = default) =>
        UpsertCustomServiceItemAsync(networkId, ContractTemplateServiceKey, "قالب نص عقد الانضمام", DefaultTemplateBody, ct);

    public async Task<ClientMembershipContractPageResult> BuildMembershipContractPageAsync(
        int clientId,
        int? restrictToNetworkId,
        CancellationToken ct = default)
    {
        IQueryable<Client> scopedQuery = Db.Clients.AsQueryable();
        if (restrictToNetworkId.HasValue)
        {
            scopedQuery = scopedQuery.Where(c => c.NetworkId == restrictToNetworkId.Value);
        }

        bool clientExists = await scopedQuery.AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists)
        {
            return new ClientMembershipContractPageResult { Status = ClientContractPageStatus.NotFound };
        }

        RenewalBlockResult renewalGuard = await renewalGuardService.CheckBlockingInvoicesAsync(clientId, ct);
        if (!renewalGuard.CanRenew)
        {
            return new ClientMembershipContractPageResult
            {
                Status = ClientContractPageStatus.RenewalBlocked,
                ErrorMessage =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س)."
            };
        }

        Client client = await scopedQuery
            .Include(c => c.Profile)
            .Include(c => c.Network)
            .FirstAsync(c => c.Id == clientId, ct);

        int contractNetworkId = client.NetworkId ?? 0;
        ClientContractMeta meta = contractNetworkId > 0
            ? await GetMetaAsync(contractNetworkId, ct)
            : new ClientContractMeta();
        string templateBody = contractNetworkId > 0
            ? await GetTemplateBodyAsync(contractNetworkId, ct)
            : DefaultTemplateBody;

        return new ClientMembershipContractPageResult
        {
            Status = ClientContractPageStatus.Success,
            Client = client,
            PrintView = BuildPrintView(client, meta, templateBody, DateTime.Now)
        };
    }

    public async Task<ClientContractTemplateSettingsViewData> BuildSettingsPageAsync(int networkId, CancellationToken ct = default)
    {
        ClientContractMeta meta = await GetMetaAsync(networkId, ct);
        string body = await GetTemplateBodyAsync(networkId, ct);
        Network network = await Db.Networks.AsNoTracking().FirstAsync(n => n.Id == networkId, ct);
        return BuildTemplateSettingsView(network, meta, body);
    }

    public ClientContractSettingsSaveResult ValidateSettingsSave(
        Network network,
        ClientContractSettingsSaveCommand command)
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(command.ContractTitle))
        {
            errors.Add("عنوان العقد مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(command.ContractBodyTemplate))
        {
            errors.Add("نص العقد مطلوب.");
        }

        IReadOnlyList<string> unknownVariables = ValidateTemplateVariables(command.ContractBodyTemplate);
        if (unknownVariables.Count > 0)
        {
            errors.Add($"يوجد متغيرات غير معروفة داخل النص: {string.Join(", ", unknownVariables)}");
        }

        if (errors.Count == 0)
        {
            return ClientContractSettingsSaveResult.Valid();
        }

        ClientContractMeta invalidMeta = new()
        {
            ContractTitle = command.ContractTitle,
            RecordNumber = command.RecordNumber,
            LicenseNumber = command.LicenseNumber
        };
        ClientContractTemplateSettingsViewData view = BuildTemplateSettingsView(
            network,
            invalidMeta,
            command.ContractBodyTemplate ?? string.Empty);
        return ClientContractSettingsSaveResult.Invalid(view);
    }

    public async Task SaveSettingsAsync(int networkId, ClientContractSettingsSaveCommand command, CancellationToken ct = default)
    {
        ClientContractMeta meta = new()
        {
            ContractTitle = command.ContractTitle.Trim(),
            RecordNumber = string.IsNullOrWhiteSpace(command.RecordNumber) ? null : command.RecordNumber.Trim(),
            LicenseNumber = string.IsNullOrWhiteSpace(command.LicenseNumber) ? null : command.LicenseNumber.Trim()
        };
        await SaveSettingsAsync(networkId, meta, command.ContractBodyTemplate.Trim(), ct);
    }

    private async Task UpsertCustomServiceItemAsync(
        int networkId,
        string serviceKey,
        string title,
        string body,
        CancellationToken ct)
    {
        CustomServiceItem? existing = await Db.CustomServiceItems
            .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == serviceKey, ct);

        if (existing == null)
        {
            Db.CustomServiceItems.Add(new CustomServiceItem
            {
                NetworkId = networkId,
                ServiceKey = serviceKey,
                Title = title,
                Body = body,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        }
        else
        {
            existing.Title = title;
            existing.Body = body;
            existing.UpdatedAt = DateTime.Now;
        }

        await Db.SaveChangesAsync(ct);
    }
}
