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
    Unknown = 7
}

public enum SubscriberFaultConfidence
{
    Low = 0,
    Medium = 1,
    High = 2
}
