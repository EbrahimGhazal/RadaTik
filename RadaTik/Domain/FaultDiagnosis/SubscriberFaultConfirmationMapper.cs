using RadaTik.Models;

namespace RadaTik.Domain.FaultDiagnosis;

public static class SubscriberFaultProblemTypes
{
    public static bool IsOutage(MaintenanceType type) =>
        type is MaintenanceType.NoInternet
            or MaintenanceType.ServiceOutage
            or MaintenanceType.SlowConnection
            or MaintenanceType.RouterNotWorking
            or MaintenanceType.RouterInternetLedOff
            or MaintenanceType.RouterInternetAndWanLedsOff;
}

public static class SubscriberFaultConfirmationMapper
{
    public static SubscriberFaultComponent FromMaintenanceTypes(IReadOnlyList<MaintenanceType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        if (types.Any(t => t == MaintenanceType.SwitchReplacement))
        {
            return SubscriberFaultComponent.Switch;
        }

        if (types.Any(t =>
                t is MaintenanceType.CableReplacement
                    or MaintenanceType.CableIssue
                    or MaintenanceType.Rg45ConnectorReplacement))
        {
            return SubscriberFaultComponent.Cable;
        }

        if (types.Any(t => t is MaintenanceType.ReceiverReplacement or MaintenanceType.PoeChange))
        {
            return SubscriberFaultComponent.Receiver;
        }

        if (types.Any(t =>
                t is MaintenanceType.RouterReplacement
                    or MaintenanceType.RouterSettingsChange
                    or MaintenanceType.RouterPasswordChange
                    or MaintenanceType.RouterIssue
                    or MaintenanceType.RouterNotWorking
                    or MaintenanceType.RouterInternetLedOff
                    or MaintenanceType.RouterInternetAndWanLedsOff))
        {
            return SubscriberFaultComponent.Router;
        }

        return SubscriberFaultComponent.Unknown;
    }

    public static bool MatchesSuggestion(SubscriberFaultComponent suggested, SubscriberFaultComponent confirmed)
    {
        if (suggested == confirmed)
        {
            return true;
        }

        if (suggested == SubscriberFaultComponent.CableOrSwitch
            && confirmed is SubscriberFaultComponent.Cable or SubscriberFaultComponent.Switch)
        {
            return true;
        }

        if (suggested == SubscriberFaultComponent.LastMile
            && confirmed is SubscriberFaultComponent.Cable
                or SubscriberFaultComponent.Switch
                or SubscriberFaultComponent.Router
                or SubscriberFaultComponent.Receiver)
        {
            return true;
        }

        return false;
    }
}
