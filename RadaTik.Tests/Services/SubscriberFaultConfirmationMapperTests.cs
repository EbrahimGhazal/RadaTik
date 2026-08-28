using RadaTik.Domain.FaultDiagnosis;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SubscriberFaultConfirmationMapperTests
{
    [Fact]
    public void SwitchReplacement_MapsToSwitch()
    {
        Assert.Equal(
            SubscriberFaultComponent.Switch,
            SubscriberFaultConfirmationMapper.FromMaintenanceTypes([MaintenanceType.SwitchReplacement]));
    }

    [Fact]
    public void CableIssue_MapsToCable()
    {
        Assert.Equal(
            SubscriberFaultComponent.Cable,
            SubscriberFaultConfirmationMapper.FromMaintenanceTypes([MaintenanceType.CableIssue, MaintenanceType.TechnicianVisit]));
    }

    [Fact]
    public void CableOrSwitch_MatchesCableOrSwitch()
    {
        Assert.True(SubscriberFaultConfirmationMapper.MatchesSuggestion(
            SubscriberFaultComponent.CableOrSwitch,
            SubscriberFaultComponent.Cable));
        Assert.True(SubscriberFaultConfirmationMapper.MatchesSuggestion(
            SubscriberFaultComponent.LastMile,
            SubscriberFaultComponent.Router));
        Assert.False(SubscriberFaultConfirmationMapper.MatchesSuggestion(
            SubscriberFaultComponent.Receiver,
            SubscriberFaultComponent.Cable));
    }

    [Fact]
    public void LedParser_ReadsYesNo()
    {
        SubscriberFaultLedAnswers led = SubscriberFaultLedAnswersParser.From("true", "0", "yes", null);
        Assert.True(led.RouterPowerOn);
        Assert.False(led.InternetLedOn);
        Assert.True(led.WanLedOn);
        Assert.Null(led.NeighborsOnSwitchDown);
        Assert.True(led.HasAny);
    }

    [Fact]
    public void AppendToDescription_TruncatesToMax()
    {
        string text = SubscriberFaultDiagnosisText.AppendToDescription(new string('أ', 980), "الكبل", new string('ب', 80), 1000);
        Assert.Equal(1000, text.Length);
        Assert.StartsWith(new string('أ', 980), text);
    }
}
