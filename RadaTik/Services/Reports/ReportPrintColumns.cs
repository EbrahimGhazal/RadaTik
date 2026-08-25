using System.Globalization;
using RadaTik.Models;
using RadaTik.ViewModels.CompanyAdmin;

namespace RadaTik.Services.Reports;

public sealed record ReportColumnOption(string Key, string Title);

public static class ReportPrintColumns
{
    public const string SequenceKey = "seq";
    public const string SequenceTitle = "تسلسل";

    public static IReadOnlyList<ReportColumnOption> Selectable(CompanyReportKind kind) => kind switch
    {
        CompanyReportKind.Subscribers => Subscriber,
        CompanyReportKind.Sectors => Sectors,
        CompanyReportKind.Receivers => Receivers,
        CompanyReportKind.Servers => Servers,
        CompanyReportKind.Subcontractors => Subcontractors,
        _ => []
    };

    public static IReadOnlyList<string> DefaultKeys(CompanyReportKind kind) => kind switch
    {
        CompanyReportKind.Subscribers => SubscriberDefaults,
        CompanyReportKind.Sectors => SectorDefaults,
        CompanyReportKind.Receivers => ReceiverDefaults,
        CompanyReportKind.Servers => ServerDefaults,
        CompanyReportKind.Subcontractors => SubcontractorDefaults,
        _ => []
    };

    public static IReadOnlyList<string> ResolveSelected(CompanyReportKind kind, IEnumerable<string>? requested)
    {
        IReadOnlyList<ReportColumnOption> options = Selectable(kind);
        HashSet<string> allowed = options.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> ordered = [];
        if (requested != null)
        {
            foreach (string raw in requested)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string key = raw.Trim();
                if (key.Equals(SequenceKey, StringComparison.OrdinalIgnoreCase)
                    || ordered.Contains(key, StringComparer.OrdinalIgnoreCase)
                    || !allowed.Contains(key))
                {
                    continue;
                }

                ordered.Add(options.First(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Key);
            }
        }

        return ordered.Count > 0 ? ordered : DefaultKeys(kind);
    }

    public static string Serialize(CompanyReportKind kind, IEnumerable<string> keys) =>
        string.Join(",", ResolveSelected(kind, keys));

    public static IReadOnlyList<string> Deserialize(CompanyReportKind kind, string? stored) =>
        ResolveSelected(
            kind,
            string.IsNullOrWhiteSpace(stored)
                ? null
                : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildTable<T>(
        CompanyReportKind kind,
        IReadOnlyList<T> items,
        IEnumerable<string>? requestedColumns,
        Func<T, string, string> valueOf)
    {
        IReadOnlyList<string> selected = ResolveSelected(kind, requestedColumns);
        IReadOnlyList<ReportColumnOption> options = Selectable(kind);
        string[] headers =
        [
            SequenceTitle,
            .. selected.Select(key => options.First(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Title)
        ];
        List<IReadOnlyList<string>> rows = new(items.Count);
        int seq = 1;
        foreach (T item in items)
        {
            List<string> cells = new(selected.Count + 1) { seq.ToString(CultureInfo.InvariantCulture) };
            foreach (string key in selected)
            {
                cells.Add(valueOf(item, key));
            }

            rows.Add(cells);
            seq++;
        }

        return (headers, rows);
    }

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildSubscribers(
        IReadOnlyList<Client> clients,
        IEnumerable<string>? requestedColumns) =>
        BuildTable(CompanyReportKind.Subscribers, clients, requestedColumns, SubscriberValue);

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildSectors(
        IReadOnlyList<Sector> sectors,
        IEnumerable<string>? requestedColumns) =>
        BuildTable(CompanyReportKind.Sectors, sectors, requestedColumns, SectorValue);

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildReceivers(
        IReadOnlyList<Receiver> receivers,
        IEnumerable<string>? requestedColumns) =>
        BuildTable(CompanyReportKind.Receivers, receivers, requestedColumns, ReceiverValue);

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildServers(
        IReadOnlyList<MikroTikServer> servers,
        IEnumerable<string>? requestedColumns) =>
        BuildTable(CompanyReportKind.Servers, servers, requestedColumns, ServerValue);

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) BuildSubcontractors(
        IReadOnlyList<CollectionPointAccount> accounts,
        IEnumerable<string>? requestedColumns) =>
        BuildTable(CompanyReportKind.Subcontractors, accounts, requestedColumns, SubcontractorValue);

    private static readonly IReadOnlyList<ReportColumnOption> Subscriber =
    [
        new("name", "الاسم الثلاثي"),
        new("sid", "الرقم الوطني"),
        new("username", "اسم المستخدم"),
        new("phone", "الجوال"),
        new("profile", "البروفايل"),
        new("joinDate", "تاريخ الانضمام"),
        new("address", "العنوان"),
        new("status", "الحالة"),
        new("expiration", "تاريخ الانتهاء"),
        new("serviceStart", "بداية الخدمة"),
        new("receiver", "المستقبل"),
        new("network", "الشبكة"),
        new("ip", "عنوان IP"),
        new("mac", "الماك"),
        new("vip", "مميز")
    ];

    private static readonly IReadOnlyList<string> SubscriberDefaults =
        ["name", "sid", "username", "phone", "profile", "joinDate", "address"];

    private static readonly IReadOnlyList<ReportColumnOption> Sectors =
    [
        new("name", "اسم المرسل"),
        new("ip", "عنوان IP"),
        new("mask", "قناع الشبكة"),
        new("coords", "الإحداثيات (عرض، طول)"),
        new("lat", "خط العرض"),
        new("lng", "خط الطول"),
        new("elevation", "الارتفاع (م)"),
        new("antenna", "ارتفاع الهوائي (م)"),
        new("direction", "الاتجاه"),
        new("angle", "زاوية الانتشار"),
        new("range", "مدى (كم)"),
        new("server", "خادم MikroTik"),
        new("network", "الشبكة"),
        new("status", "الحالة"),
        new("created", "تاريخ الإضافة")
    ];

    private static readonly IReadOnlyList<string> SectorDefaults =
        ["name", "ip", "coords", "elevation", "direction", "angle", "range", "network"];

    private static readonly IReadOnlyList<ReportColumnOption> Receivers =
    [
        new("name", "اسم المستقبل"),
        new("ip", "عنوان IP"),
        new("mask", "قناع الشبكة"),
        new("sector", "المرسل"),
        new("network", "الشبكة"),
        new("lat", "خط العرض"),
        new("lng", "خط الطول"),
        new("elevation", "الارتفاع (م)"),
        new("antenna", "ارتفاع الهوائي (م)"),
        new("clients", "عدد المشتركين"),
        new("status", "الحالة"),
        new("created", "تاريخ الإنشاء")
    ];

    private static readonly IReadOnlyList<string> ReceiverDefaults =
        ["name", "ip", "mask", "sector", "network", "lat", "lng", "clients", "status"];

    private static readonly IReadOnlyList<ReportColumnOption> Servers =
    [
        new("name", "الاسم"),
        new("host", "المضيف"),
        new("port", "المنفذ"),
        new("user", "المستخدم"),
        new("notes", "ملاحظات"),
        new("network", "الشبكة"),
        new("status", "الحالة"),
        new("created", "تاريخ الإنشاء")
    ];

    private static readonly IReadOnlyList<string> ServerDefaults =
        ["name", "host", "port", "user", "network", "status"];

    private static readonly IReadOnlyList<ReportColumnOption> Subcontractors =
    [
        new("username", "اسم المستخدم"),
        new("fullName", "الاسم الكامل"),
        new("email", "البريد"),
        new("phone", "الهاتف"),
        new("network", "الشبكة"),
        new("balance", "الرصيد"),
        new("created", "تاريخ الإنشاء")
    ];

    private static readonly IReadOnlyList<string> SubcontractorDefaults =
        ["username", "fullName", "phone", "network", "balance"];

    private static string SubscriberValue(Client client, string key) => key switch
    {
        "name" => client.Name ?? "",
        "sid" => client.SID ?? "",
        "username" => client.UserName ?? "",
        "phone" => client.PhoneNumber ?? "",
        "profile" => client.Profile?.Name ?? client.ProfileName ?? "",
        "joinDate" => client.CreatedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        "address" => client.ResidenceAddress ?? "",
        "status" => client.IsActive ? "نشط" : "متوقف",
        "expiration" => client.AccountExpirationDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
        "serviceStart" => client.ServiceStartDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "",
        "receiver" => client.Receiver?.Name ?? "",
        "network" => client.Network?.Name ?? "",
        "ip" => client.Address ?? "",
        "mac" => client.MacAddress ?? "",
        "vip" => client.IsVip ? "نعم" : "لا",
        _ => ""
    };

    private static string SectorValue(Sector sector, string key) => key switch
    {
        "name" => sector.Name ?? "",
        "ip" => sector.IPAddress ?? "",
        "mask" => sector.NetworkMask ?? "",
        "coords" => string.Create(CultureInfo.InvariantCulture, $"{sector.Latitude:F5}، {sector.Longitude:F5}"),
        "lat" => sector.Latitude.ToString("F5", CultureInfo.InvariantCulture),
        "lng" => sector.Longitude.ToString("F5", CultureInfo.InvariantCulture),
        "elevation" => sector.ElevationMeters?.ToString("F1", CultureInfo.InvariantCulture) ?? "",
        "antenna" => sector.AntennaHeightAglMeters?.ToString("F1", CultureInfo.InvariantCulture) ?? "",
        "direction" => sector.Direction.ToString("F0", CultureInfo.InvariantCulture),
        "angle" => sector.CoverageAngle.ToString("F0", CultureInfo.InvariantCulture),
        "range" => sector.CoverageRange.ToString("F2", CultureInfo.InvariantCulture),
        "server" => sector.MikroTikServer?.Name ?? "",
        "network" => sector.Network?.Name ?? "",
        "status" => sector.IsActive ? "نشط" : "متوقف",
        "created" => sector.CreatedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        _ => ""
    };

    private static string ReceiverValue(Receiver receiver, string key) => key switch
    {
        "name" => receiver.Name ?? "",
        "ip" => receiver.IPAddress ?? "",
        "mask" => receiver.NetworkMask ?? "",
        "sector" => receiver.Sector?.Name ?? "",
        "network" => receiver.Network?.Name ?? "",
        "lat" => receiver.Latitude.ToString("F6", CultureInfo.InvariantCulture),
        "lng" => receiver.Longitude.ToString("F6", CultureInfo.InvariantCulture),
        "elevation" => receiver.ElevationMeters?.ToString("F1", CultureInfo.InvariantCulture) ?? "",
        "antenna" => receiver.AntennaHeightAglMeters?.ToString("F1", CultureInfo.InvariantCulture) ?? "",
        "clients" => receiver.Clients?.Count.ToString(CultureInfo.InvariantCulture) ?? "0",
        "status" => receiver.IsActive ? "نشط" : "متوقف",
        "created" => receiver.CreatedDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        _ => ""
    };

    private static string ServerValue(MikroTikServer server, string key) => key switch
    {
        "name" => server.Name ?? "",
        "host" => server.Host ?? "",
        "port" => server.Port.ToString(CultureInfo.InvariantCulture),
        "user" => server.User ?? "",
        "notes" => server.Notes ?? "",
        "network" => server.Network?.Name ?? "",
        "status" => server.IsActive ? "نشط" : "متوقف",
        "created" => server.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        _ => ""
    };

    private static string SubcontractorValue(CollectionPointAccount account, string key) => key switch
    {
        "username" => account.User?.UserName ?? "",
        "fullName" => account.User?.FullName ?? "",
        "email" => account.User?.Email ?? "",
        "phone" => account.User?.PhoneNumber ?? "",
        "network" => account.Network?.Name ?? "",
        "balance" => account.Balance.ToString("N2", CultureInfo.InvariantCulture),
        "created" => account.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        _ => ""
    };
}
