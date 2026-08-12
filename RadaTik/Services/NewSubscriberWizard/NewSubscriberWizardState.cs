using System.Text.Json;
using RadaTik.Models;

namespace RadaTik.Services.NewSubscriberWizard;

public sealed class NewSubscriberWizardState
{
    public const string SessionKey = "NewSubscriberWizard.State";

    public NewSubscriberWizardPath Path { get; set; }
    public int? ReceiverId { get; set; }
    public int? MikroTikServerId { get; set; }
    public int? SectorId { get; set; }
    public int? ClientId { get; set; }
    public int? InvoiceId { get; set; }
}

public static class NewSubscriberWizardSessionExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static NewSubscriberWizardState? GetWizardState(this ISession session)
    {
        byte[]? data = session.Get(NewSubscriberWizardState.SessionKey);
        if (data == null || data.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<NewSubscriberWizardState>(data, JsonOptions);
    }

    public static void SetWizardState(this ISession session, NewSubscriberWizardState state)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        session.Set(NewSubscriberWizardState.SessionKey, data);
    }

    public static void ClearWizardState(this ISession session)
    {
        session.Remove(NewSubscriberWizardState.SessionKey);
    }
}
