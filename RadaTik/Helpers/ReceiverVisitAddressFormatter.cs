using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>
/// يبني عنوان زيارة الصيانة من موقع اللاقط المرتبط بالمشترك.
/// </summary>
public static class ReceiverVisitAddressFormatter
{
    public static string? FromClient(Client? client)
    {
        if (client == null)
        {
            return null;
        }

        Receiver? receiver = client.Receiver;
        List<string> lines = [];

        if (receiver != null)
        {
            if (!string.IsNullOrWhiteSpace(receiver.Name))
            {
                lines.Add(receiver.Name.Trim());
            }

            if (!string.IsNullOrWhiteSpace(receiver.Sector?.Name))
            {
                lines.Add("القطاع: " + receiver.Sector.Name.Trim());
            }

            if (HasCoordinates(receiver.Latitude, receiver.Longitude))
            {
                lines.Add($"الموقع: {receiver.Latitude:0.######}, {receiver.Longitude:0.######}");
            }
        }

        if (!string.IsNullOrWhiteSpace(client.ResidenceAddress))
        {
            string residence = client.ResidenceAddress.Trim();
            if (!lines.Exists(line => string.Equals(line, residence, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(residence);
            }
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    public static bool TryGetReceiverCoordinates(Client? client, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        Receiver? receiver = client?.Receiver;
        if (receiver == null || !HasCoordinates(receiver.Latitude, receiver.Longitude))
        {
            return false;
        }

        latitude = receiver.Latitude;
        longitude = receiver.Longitude;
        return true;
    }

    private static bool HasCoordinates(double latitude, double longitude)
        => Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001;
}
