using RadaTik.Domain.FaultDiagnosis;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SubscriberFaultDiagnosisEngineTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0);

    [Fact]
    public void DisabledAccount_IsAccountCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(new SubscriberFaultFacts
        {
            Now = Now,
            IsAccountActive = false,
            HasMikroTikServer = true,
            ServerApiReachable = true
        });

        Assert.Equal(SubscriberFaultComponent.Account, result.Cause);
        Assert.Equal(SubscriberFaultConfidence.High, result.Confidence);
        Assert.Null(result.SuggestedMaintenanceType);
    }

    [Fact]
    public void ExpiredWithoutPpp_IsAccountCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(new SubscriberFaultFacts
        {
            Now = Now,
            AccountExpirationDate = Now.AddDays(-1),
            HasMikroTikServer = true,
            ServerApiReachable = true
        });

        Assert.Equal(SubscriberFaultComponent.Account, result.Cause);
        Assert.Contains("PPPoE", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("ما زالت قائمة", result.Summary);
    }

    [Fact]
    public void ExpiredWithPppStillUp_IsAccountCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(new SubscriberFaultFacts
        {
            Now = Now,
            AccountExpirationDate = Now.AddDays(-2),
            HasMikroTikServer = true,
            ServerApiReachable = true,
            HasPppSession = true
        });

        Assert.Equal(SubscriberFaultComponent.Account, result.Cause);
        Assert.Contains("ما زالت قائمة", result.Summary);
    }

    [Fact]
    public void ActivePppSession_PointsToRouter()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            HasPppSession = true,
            ServerConnectedCount = 4,
            SectorConnectedCount = 3,
            ReceiverConnectedCount = 2
        });

        Assert.Equal(SubscriberFaultComponent.Router, result.Cause);
        Assert.Equal(MaintenanceType.RouterInternetLedOff, result.SuggestedMaintenanceType);
        Assert.Equal(SubscriberFaultConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void ServerApiDown_IsServerCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerApiReachable = false
        });

        Assert.Equal(SubscriberFaultComponent.Server, result.Cause);
        Assert.Equal(SubscriberFaultConfidence.High, result.Confidence);
    }

    [Fact]
    public void AllServerPeersDown_IsServerCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerClientCount = 20,
            ServerConnectedCount = 0,
            SectorClientCount = 8,
            SectorConnectedCount = 0,
            ReceiverClientCount = 3,
            ReceiverConnectedCount = 0
        });

        Assert.Equal(SubscriberFaultComponent.Server, result.Cause);
    }

    [Fact]
    public void AllSectorPeersDown_WhileServerHasOthers_IsSectorCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerClientCount = 20,
            ServerConnectedCount = 12,
            SectorClientCount = 8,
            SectorConnectedCount = 0,
            ReceiverClientCount = 3,
            ReceiverConnectedCount = 0,
            SectorPingOk = false,
            SectorRadioDegraded = true
        });

        Assert.Equal(SubscriberFaultComponent.Sector, result.Cause);
        Assert.Contains("المرسل", result.Summary);
    }

    [Fact]
    public void AllReceiverPeersDown_WhileSectorHasOthers_IsReceiverCause()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorClientCount = 8,
            SectorConnectedCount = 5,
            ReceiverClientCount = 4,
            ReceiverConnectedCount = 0,
            ReceiverPingOk = false
        });

        Assert.Equal(SubscriberFaultComponent.Receiver, result.Cause);
        Assert.Equal(MaintenanceType.PoeChange, result.SuggestedMaintenanceType);
    }

    [Fact]
    public void ReceiverPingOk_AllReceiverPeersDown_IsCableOrSwitch()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorConnectedCount = 5,
            ReceiverClientCount = 4,
            ReceiverConnectedCount = 0,
            ReceiverPingOk = true
        });

        Assert.Equal(SubscriberFaultComponent.CableOrSwitch, result.Cause);
        Assert.Equal(MaintenanceType.CableIssue, result.SuggestedMaintenanceType);
    }

    [Fact]
    public void IsolatedClient_NeighborsOnReceiverConnected_IsCableOrSwitch()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorConnectedCount = 5,
            ReceiverClientCount = 4,
            ReceiverConnectedCount = 3
        });

        Assert.Equal(SubscriberFaultComponent.CableOrSwitch, result.Cause);
        Assert.Equal(SubscriberFaultConfidence.Medium, result.Confidence);
    }

    [Fact]
    public void WanLedOff_RefinesToCable()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorConnectedCount = 5,
            ReceiverClientCount = 4,
            ReceiverConnectedCount = 3,
            ReceiverPingOk = true,
            Led = new SubscriberFaultLedAnswers(RouterPowerOn: true, InternetLedOn: false, WanLedOn: false)
        });

        Assert.Equal(SubscriberFaultComponent.Cable, result.Cause);
        Assert.Equal(MaintenanceType.CableIssue, result.SuggestedMaintenanceType);
        Assert.Equal(SubscriberFaultConfidence.High, result.Confidence);
    }

    [Fact]
    public void NeighborsOnSwitchDown_RefinesToSwitch()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorConnectedCount = 5,
            ReceiverClientCount = 4,
            ReceiverConnectedCount = 3,
            Led = new SubscriberFaultLedAnswers(NeighborsOnSwitchDown: true)
        });

        Assert.Equal(SubscriberFaultComponent.Switch, result.Cause);
        Assert.Equal(MaintenanceType.SwitchReplacement, result.SuggestedMaintenanceType);
    }

    [Fact]
    public void HistoryPrefersCable_OnLastMile()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorClientCount = 8,
            SectorConnectedCount = 7,
            ReceiverClientCount = 1,
            ReceiverConnectedCount = 0,
            LastMileHistory = new SubscriberFaultLastMileStats(10, 1, 1, 0, 12)
        });

        Assert.Equal(SubscriberFaultComponent.Cable, result.Cause);
        Assert.Contains("السجل المؤكد", result.Summary);
    }

    [Fact]
    public void UniqueOnReceiver_SectorHealthy_IsLastMile()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(BaseDisconnected() with
        {
            ServerConnectedCount = 12,
            SectorClientCount = 8,
            SectorConnectedCount = 7,
            ReceiverClientCount = 1,
            ReceiverConnectedCount = 0
        });

        Assert.Equal(SubscriberFaultComponent.LastMile, result.Cause);
        Assert.Equal(SubscriberFaultConfidence.Low, result.Confidence);
    }

    [Fact]
    public void NoServerAssigned_IsUnknown()
    {
        SubscriberFaultDiagnosisResult result = SubscriberFaultDiagnosisEngine.Diagnose(new SubscriberFaultFacts
        {
            Now = Now,
            HasMikroTikServer = false
        });

        Assert.Equal(SubscriberFaultComponent.Unknown, result.Cause);
        Assert.Contains("MikroTik", result.Summary);
    }

    private static SubscriberFaultFacts BaseDisconnected() => new()
    {
        Now = Now,
        IsAccountActive = true,
        AccountExpirationDate = Now.AddDays(10),
        HasMikroTikServer = true,
        ServerApiReachable = true,
        HasPppSession = false,
        ServerClientCount = 20,
        ServerConnectedCount = 12,
        SectorClientCount = 8,
        SectorConnectedCount = 5,
        ReceiverClientCount = 4,
        ReceiverConnectedCount = 0
    };
}
