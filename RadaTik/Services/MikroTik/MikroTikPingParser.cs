namespace RadaTik.Services.MikroTik;

/// <summary>يفسّر جمل ردّ أمر <c>/ping</c> في RouterOS.</summary>
public static class MikroTikPingParser
{
    public static bool IsReachable(IEnumerable<(string Received, string Time, string Status, string PacketLoss)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        foreach ((string received, string time, string status, string packetLoss) in rows)
        {
            if (ContainsToken(status, "timeout") || ContainsToken(status, "unreachable"))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(time))
            {
                return true;
            }

            if (int.TryParse(received, out int receivedCount) && receivedCount > 0)
            {
                return true;
            }

            if (TryParseLoss(packetLoss, out decimal loss) && loss < 100m)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseLoss(string packetLoss, out decimal loss)
    {
        loss = 100m;
        if (string.IsNullOrWhiteSpace(packetLoss))
        {
            return false;
        }

        string trimmed = packetLoss.Trim().TrimEnd('%');
        return decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out loss);
    }

    private static bool ContainsToken(string value, string token) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
