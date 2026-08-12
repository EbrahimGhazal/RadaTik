using System.Text.RegularExpressions;
using RadaTik.Models;

namespace RadaTik.Domain.Clients;

/// <summary>عرض قالب عقد الانضمام بمتغيرات {{Name}}.</summary>
public static class ContractTemplateRenderer
{
    public static IReadOnlyDictionary<string, string> VariableLabels { get; } =
        new Dictionary<string, string>
        {
            ["{{SubscriberName}}"] = "اسم المشترك",
            ["{{SubscriberNumber}}"] = "رقم المشترك (SID)",
            ["{{ContractDate}}"] = "تاريخ تحرير العقد",
            ["{{SubscriptionStartDate}}"] = "تاريخ الاشتراك",
            ["{{SubscriptionEndDate}}"] = "تاريخ انتهاء الاشتراك",
            ["{{ProfileName}}"] = "اسم البروفايل",
            ["{{NetworkName}}"] = "اسم الشبكة",
            ["{{ClientUserName}}"] = "اسم مستخدم المشترك"
        };

    public static string Render(string? template, Client client, DateTime contractDate)
    {
        string profileName = client.Profile?.Name ?? client.ProfileName ?? "-";
        string networkName = client.Network?.Name ?? "-";

        Dictionary<string, string> replacements = new()
        {
            ["{{SubscriberName}}"] = client.Name ?? "-",
            ["{{SubscriberNumber}}"] = client.SID ?? "-",
            ["{{ContractDate}}"] = contractDate.ToString("yyyy/MM/dd"),
            ["{{SubscriptionStartDate}}"] = client.ServiceStartDate?.ToString("yyyy/MM/dd") ?? "-",
            ["{{SubscriptionEndDate}}"] = client.AccountExpirationDate?.ToString("yyyy/MM/dd") ?? "-",
            ["{{ProfileName}}"] = profileName,
            ["{{NetworkName}}"] = networkName,
            ["{{ClientUserName}}"] = client.UserName ?? "-"
        };

        string rendered = template ?? string.Empty;
        foreach (KeyValuePair<string, string> pair in replacements)
        {
            rendered = rendered.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        return rendered;
    }

    public static IReadOnlyList<string> FindUnknownVariables(string? template, IEnumerable<string> allowedVariables)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return Array.Empty<string>();
        }

        HashSet<string> allowed = new(allowedVariables, StringComparer.Ordinal);
        List<string> found = Regex.Matches(template, @"\{\{[^{}]+\}\}")
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return found.Where(v => !allowed.Contains(v)).ToList();
    }
}
