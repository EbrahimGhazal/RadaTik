using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Constants;
using RadaTik.Domain.Common;
using RadaTik.Domain.ValueObjects;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Approvals;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Clients;

public sealed partial class ClientProvisioningService
{
    public ClientValidationResult ValidateForCreate(Client client)
    {
        ClientValidationResult result = ClientValidationResult.Ok();

        if (string.IsNullOrWhiteSpace(client.Name))
        {
            result.Add("Name", "الاسم مطلوب");
        }

        ServiceResult<PppoeUsername> userName = PppoeUsername.TryCreate(client.UserName);
        if (!userName.IsSuccess)
        {
            result.Add("UserName", userName.ErrorMessage ?? "اسم المستخدم غير صالح");
        }
        else
        {
            client.UserName = userName.Value!.Value;
        }

        if (string.IsNullOrWhiteSpace(client.Password))
        {
            result.Add("Password", "كلمة المرور مطلوبة");
        }

        if (client.ProfileId <= 0)
        {
            result.Add("ProfileId", "البروفايل مطلوب");
        }

        if (!client.MikroTikServerId.HasValue)
        {
            result.Add("MikroTikServerId", "يجب اختيار خادم المايكروتك");
        }

        if (!string.IsNullOrWhiteSpace(client.PhoneNumber))
        {
            ServiceResult<PhoneNumber> phone = PhoneNumber.TryCreate(client.PhoneNumber);
            if (!phone.IsSuccess)
            {
                result.Add("PhoneNumber", phone.ErrorMessage ?? "رقم الهاتف غير صالح");
            }
        }

        if (!string.IsNullOrWhiteSpace(client.SID))
        {
            ServiceResult<SubscriberSid> sid = SubscriberSid.TryCreate(client.SID);
            if (!sid.IsSuccess)
            {
                result.Add("SID", sid.ErrorMessage ?? "رقم المشترك غير صالح");
            }
        }

        return result;
    }

    public async Task<ClientCreateOutcome> CreateClientAsync(ClientCreateRequest request, CancellationToken ct = default)
    {
        ClientValidationResult validation = ValidateForCreate(request.Client);
        if (!validation.IsValid)
        {
            return ClientCreateOutcome.Validation(validation.Errors);
        }

        if (request.IsEmployee)
        {
            return await CreateAsEmployeePendingAsync(request, ct);
        }

        return await CreateAsAdministratorAsync(request, ct);
    }

    private async Task<ClientCreateOutcome> CreateAsEmployeePendingAsync(ClientCreateRequest request, CancellationToken ct)
    {
        Client client = request.Client;
        Profile? profile = await Db.Profiles
            .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == request.NetworkId, ct);
        if (profile == null)
        {
            return ClientCreateOutcome.Validation(new Dictionary<string, string>
            {
                ["ProfileId"] = "البروفايل المحدد غير موجود في هذه الشبكة"
            });
        }

        if (client.ReceiverId.HasValue && client.ReceiverId.Value <= 0)
        {
            client.ReceiverId = null;
        }

        client.ProfileName = profile.Name;
        client.NetworkId = request.NetworkId;
        client.CreatedDate = DateTime.Now;
        client.LastUpdated = DateTime.Now;
        client.IsActive = false;
        client.ConnectionStatus = EmployeeApprovalStates.PendingClientConnectionStatus;
        client.AccountExpirationDate ??= DateTime.Now.AddMonths(1);
        client.ServiceStartDate ??= DateTime.Now.Date;
        client.LastRenewalDate = DateTime.Now.Date;
        client.VipBenefitKind = ClientVipBenefitKind.None;
        client.VipDiscountPercent = 0m;
        ClientVipAssignment.NormalizeNew(client, DateTime.Now);

        Db.Clients.Add(client);
        await Db.SaveChangesAsync(ct);

        string requestNotes = EmployeeApprovalRequestHelper.BuildClientCreate(
            client.Id,
            request.DbUserName,
            request.DbPassword);
        decimal expectedCharge = await ResolveExpectedClientCreateChargeAsync(request.NetworkId, ct);
        await _approvalRequests.CreatePendingAsync(
            request.NetworkId,
            request.ActorUserId,
            FeatureKeys.Clients,
            requestNotes,
            expectedCharge,
            ct);

        return ClientCreateOutcome.EmployeePending(
            "تم تسجيل إضافة المشترك كطلب موافقة. يُنشأ الحساب على النظام وسيرفر MikroTik بعد اعتماد مدير الشركة.");
    }

    private async Task<ClientCreateOutcome> CreateAsAdministratorAsync(ClientCreateRequest request, CancellationToken ct)
    {
        Client client = request.Client;
        await using IDbContextTransaction transaction = await Db.Database.BeginTransactionAsync(ct);
        bool mikroTikSuccess = false;

        try
        {
            _logger.LogInformation("بدء إضافة عميل جديد: {UserName}", client.UserName);

            client.NetworkId = request.NetworkId;

            Network? selectedNetwork = await Db.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == request.NetworkId, ct);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? request.NetworkId;

            UsageImportChargeEstimate clientChargeEstimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSubscriber,
                1);
            decimal requiredAmount = clientChargeEstimate.RequiredAmountSyp;
            if (requiredAmount > 0m && clientChargeEstimate.WalletBalance < requiredAmount)
            {
                throw new InvalidOperationException(
                    $"رصيد محفظة الشركة غير كافٍ لإضافة عميل جديد. المطلوب {requiredAmount:N2} ل.س.ج والرصيد الحالي {clientChargeEstimate.WalletBalance:N2} ل.س.ج.");
            }

            ApplicationUser? existingUser = await _userManager.FindByNameAsync(client.UserName!);
            if (existingUser != null)
            {
                throw new InvalidOperationException("اسم المستخدم موجود مسبقاً في النظام");
            }

            Profile? profile = await Db.Profiles
                .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == request.NetworkId, ct);
            if (profile == null)
            {
                throw new InvalidOperationException("البروفايل المحدد غير موجود في هذه الشبكة");
            }

            client.ProfileName = profile.Name;
            if (client.ReceiverId.HasValue && client.ReceiverId.Value <= 0)
            {
                client.ReceiverId = null;
            }

            if (client.MikroTikServerId.HasValue)
            {
                await _mikroTik.AddPPPoEUser(client);
                mikroTikSuccess = true;
            }

            client.CreatedDate = DateTime.Now;
            client.LastUpdated = DateTime.Now;
            client.ConnectionStatus = client.IsActive ? "مفعل" : "معطل";
            client.AccountExpirationDate ??= DateTime.Now.AddMonths(1);
            client.ServiceStartDate ??= DateTime.Now.Date;
            client.LastRenewalDate = DateTime.Now.Date;
            ClientVipAssignment.NormalizeNew(client, DateTime.Now);

            try
            {
                Db.Clients.Add(client);
                await Db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx)
            {
                if (mikroTikSuccess)
                {
                    await TryCleanupMikroTikUserAsync(client);
                }

                throw new InvalidOperationException(
                    $"فشل حفظ البيانات في قاعدة البيانات: {dbEx.InnerException?.Message ?? dbEx.Message}",
                    dbEx);
            }

            string normalizedDbUserName = string.IsNullOrWhiteSpace(request.DbUserName)
                ? client.UserName!
                : request.DbUserName.Trim();
            string normalizedDbPassword = string.IsNullOrWhiteSpace(request.DbPassword)
                ? client.Password!
                : request.DbPassword.Trim();

            string userEmail = normalizedDbUserName.Contains('@')
                ? normalizedDbUserName
                : $"{normalizedDbUserName}@radatik.local";

            ApplicationUser newUser = new()
            {
                UserName = normalizedDbUserName,
                Email = userEmail,
                FullName = client.Name,
                PhoneNumber = client.PhoneNumber,
                CreatedDate = DateTime.Now,
                IsActive = client.IsActive,
                ClientId = client.Id,
                NetworkId = request.NetworkId,
                MustChangePassword = true
            };

            try
            {
                IdentityResult createResult = await _userManager.CreateAsync(newUser, normalizedDbPassword);
                if (!createResult.Succeeded)
                {
                    string errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"فشل إنشاء حساب المستخدم: {errors}");
                }

                await _userManager.AddToRoleAsync(newUser, "Client");
            }
            catch (Exception userEx)
            {
                Db.Clients.Remove(client);
                await Db.SaveChangesAsync(ct);
                if (mikroTikSuccess)
                {
                    await TryCleanupMikroTikUserAsync(client);
                }

                throw new InvalidOperationException($"فشل إنشاء حساب المستخدم: {userEx.Message}", userEx);
            }

            await transaction.CommitAsync(ct);
            await _usageChargeService.ChargeUsageIncreaseAsync(
                companyNetworkId,
                request.ActorUserId,
                PricingChargeUnit.PerSubscriber);

            return ClientCreateOutcome.Success(
                "تم إضافة العميل بنجاح في قاعدة البيانات والمايكروتك وإنشاء حساب له في النظام");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);

            if (mikroTikSuccess)
            {
                await TryCleanupMikroTikUserAsync(client);
            }

            _logger.LogError(ex, "فشل إضافة العميل {UserName}", client.UserName);
            return ClientCreateOutcome.Failed(
                MikroTikErrorFormatter.Format("خطأ في الإضافة", ex.Message));
        }
    }

    private async Task TryCleanupMikroTikUserAsync(Client client)
    {
        if (!client.MikroTikServerId.HasValue || string.IsNullOrEmpty(client.UserName))
        {
            return;
        }

        try
        {
            await _mikroTik.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "فشل تنظيف المستخدم {UserName} من المايكروتك", client.UserName);
        }
    }

    private async Task<decimal> ResolveExpectedClientCreateChargeAsync(int selectedNetworkId, CancellationToken ct)
    {
        try
        {
            Network? selectedNetwork = await Db.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSubscriber,
                1);
            return estimate.RequiredAmountSyp;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "تعذر تقدير رسوم إنشاء العميل للشبكة {NetworkId}", selectedNetworkId);
            return 0m;
        }
    }
}
