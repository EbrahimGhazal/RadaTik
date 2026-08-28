namespace RadaTik.Domain.FaultDiagnosis;

public enum SubscriberFaultComponent
{
    Account = 0,
    Server = 1,
    Sector = 2,
    Receiver = 3,
    CableOrSwitch = 4,
    Router = 5,
    LastMile = 6,
    Unknown = 7,
    Cable = 8,
    Switch = 9
}

public enum SubscriberFaultConfidence
{
    Low = 0,
    Medium = 1,
    High = 2
}

public sealed record SubscriberFaultLedAnswers(
    bool? RouterPowerOn = null,
    bool? InternetLedOn = null,
    bool? WanLedOn = null,
    bool? NeighborsOnSwitchDown = null)
{
    public bool HasAny =>
        RouterPowerOn.HasValue
        || InternetLedOn.HasValue
        || WanLedOn.HasValue
        || NeighborsOnSwitchDown.HasValue;
}

public sealed record SubscriberFaultLastMileStats(
    int CableCount,
    int SwitchCount,
    int RouterCount,
    int ReceiverCount,
    int SampleCount)
{
    public int TotalLastMile => CableCount + SwitchCount + RouterCount + ReceiverCount;
}

public static class SubscriberFaultLedAnswersParser
{
    public static SubscriberFaultLedAnswers From(
        string? routerPowerOn,
        string? internetLedOn,
        string? wanLedOn,
        string? neighborsOnSwitchDown) =>
        new(
            Parse(routerPowerOn),
            Parse(internetLedOn),
            Parse(wanLedOn),
            Parse(neighborsOnSwitchDown));

    public static bool? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.OrdinalIgnoreCase)
            || value.Equals("no", StringComparison.OrdinalIgnoreCase)
            || value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
