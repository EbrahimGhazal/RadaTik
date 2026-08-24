using System.Text.Json;

namespace RadaTik.Helpers;

public enum EmployeeApprovalRequestKind
{
    Unknown = 0,
    ReceiverCreate = 1,
    ReceiverEdit = 2,
    ClientCreate = 3,
    ClientEdit = 4
}

public sealed class ReceiverEditApprovalPayload
{
    public string? Name { get; set; }
    public int SectorId { get; set; }
    public string? IPAddress { get; set; }
    public string? NetworkMask { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationMeters { get; set; }
    public double? AntennaHeightAglMeters { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ClientApprovalPayload
{
    public string? Name { get; set; }
    public string? SID { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int ProfileId { get; set; }
    public string? ProfileName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public string? Occupation { get; set; }
    public string? Workplace { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PowerSource { get; set; }
    public string? Building { get; set; }
    public string? Floor { get; set; }
    public int? ReceiverId { get; set; }
    public int? MikroTikServerId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? AccountExpirationDate { get; set; }
    public bool IsVip { get; set; }
    public string? VipNote { get; set; }
    public string? DbUserName { get; set; }
    public string? DbPassword { get; set; }
}

public static class EmployeeApprovalRequestHelper
{
    private const string Prefix = "EMP_REQ:";
    private const int MaxNotesLength = 980;

    public static string BuildReceiverCreate(int receiverId) =>
        $"{Prefix}RECEIVER_CREATE:{receiverId}";

    public static string? BuildReceiverEdit(int receiverId, ReceiverEditApprovalPayload payload) =>
        BuildWithPayload("RECEIVER_EDIT", receiverId, payload);

    public static string? BuildClientCreate(int clientId, ClientApprovalPayload payload) =>
        BuildWithPayload("CLIENT_CREATE", clientId, payload);

    public static string? BuildClientEdit(int clientId, ClientApprovalPayload payload) =>
        BuildWithPayload("CLIENT_EDIT", clientId, payload);

    public static bool TryParse(
        string? notes,
        out EmployeeApprovalRequestKind kind,
        out int entityId,
        out string? payloadJson)
    {
        kind = EmployeeApprovalRequestKind.Unknown;
        entityId = 0;
        payloadJson = null;

        if (string.IsNullOrWhiteSpace(notes) || !notes.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = notes.Split(':', 4, StringSplitOptions.None);
        if (parts.Length < 3 || !int.TryParse(parts[2], out entityId) || entityId <= 0)
        {
            return false;
        }

        var token = parts[1];
        kind = token switch
        {
            "RECEIVER_CREATE" => EmployeeApprovalRequestKind.ReceiverCreate,
            "RECEIVER_EDIT" => EmployeeApprovalRequestKind.ReceiverEdit,
            "CLIENT_CREATE" => EmployeeApprovalRequestKind.ClientCreate,
            "CLIENT_EDIT" => EmployeeApprovalRequestKind.ClientEdit,
            _ => EmployeeApprovalRequestKind.Unknown
        };

        if (kind == EmployeeApprovalRequestKind.Unknown)
        {
            return false;
        }

        if (parts.Length == 4 && !string.IsNullOrWhiteSpace(parts[3]))
        {
            payloadJson = Uri.UnescapeDataString(parts[3]);
        }

        return true;
    }

    public static T? DeserializePayload<T>(string? payloadJson) where T : class
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildWithPayload<T>(string token, int entityId, T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var encoded = Uri.EscapeDataString(json);
        var value = $"{Prefix}{token}:{entityId}:{encoded}";
        return value.Length <= MaxNotesLength ? value : null;
    }
}
