using System.Text.Json;

namespace RadTik.Services.SystemAdminPricing;

public abstract class RecurringPricingPolicy
{
    public virtual int FreeInitialUnits => 0;
    public virtual int FreeRenewalUnits => 0;

    public static RecurringPricingPolicy Create(int freeInitialUnits, int freeRenewalUnits)
    {
        if (freeInitialUnits > 0 || freeRenewalUnits > 0)
        {
            return new PartiallyFreeRecurringPolicy(
                Math.Max(0, freeInitialUnits),
                Math.Max(0, freeRenewalUnits));
        }

        return new FullyPaidRecurringPolicy();
    }
}

public sealed class PartiallyFreeRecurringPolicy : RecurringPricingPolicy
{
    private readonly int _freeInitialUnits;
    private readonly int _freeRenewalUnits;

    public PartiallyFreeRecurringPolicy(int freeInitialUnits, int freeRenewalUnits)
    {
        _freeInitialUnits = Math.Max(0, freeInitialUnits);
        _freeRenewalUnits = Math.Max(0, freeRenewalUnits);
    }

    public override int FreeInitialUnits => _freeInitialUnits;
    public override int FreeRenewalUnits => _freeRenewalUnits;
}

public sealed class FullyPaidRecurringPolicy : RecurringPricingPolicy
{
}

internal sealed class RecurringPricingPolicyDto
{
    public int FreeInitialUnits { get; set; }
    public int FreeRenewalUnits { get; set; }
}

public static class RecurringPricingPolicyCodec
{
    public const int MaxFreeUnitsLimit = 100000;
    public const string NonNegativeFreeUnitsMessage = "عدد الوحدات المجانية يجب أن يكون أكبر من أو يساوي صفر.";
    private const string Marker = "##PRICING_POLICY##:";

    public static string BuildMaxFreeUnitsExceededMessage()
    {
        return $"عدد الوحدات المجانية كبير جدًا. الحد الأعلى هو {MaxFreeUnitsLimit:N0}.";
    }

    public static string BuildRenewalPeriodicRequiredMessage(string serviceDisplayName)
    {
        return $"زمن تجديد {serviceDisplayName} يجب أن يكون دورياً.";
    }

    public static string BuildNonNegativePriceMessage(string serviceDisplayName)
    {
        return $"أسعار {serviceDisplayName} يجب أن تكون أكبر من أو تساوي صفر.";
    }

    public static string BuildRecurringPricingSavedMessage(string serviceDisplayName)
    {
        return $"تم حفظ تسعير خدمة {serviceDisplayName} والتجديد بنجاح.";
    }

    public static string BuildRecurringPricingSaveFailedMessage(string serviceDisplayName)
    {
        return $"تعذر حفظ إعدادات تسعير خدمة {serviceDisplayName}.";
    }

    public static string WriteNotes(string baseNote, RecurringPricingPolicy policy)
    {
        var dto = new RecurringPricingPolicyDto
        {
            FreeInitialUnits = policy.FreeInitialUnits,
            FreeRenewalUnits = policy.FreeRenewalUnits
        };
        var json = JsonSerializer.Serialize(dto);
        return $"{baseNote}{Environment.NewLine}{Marker}{json}";
    }

    public static RecurringPricingPolicy ReadFromNotes(params string?[] notes)
    {
        foreach (var note in notes)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                continue;
            }

            var markerIdx = note.IndexOf(Marker, StringComparison.Ordinal);
            if (markerIdx < 0)
            {
                continue;
            }

            var json = note[(markerIdx + Marker.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                var dto = JsonSerializer.Deserialize<RecurringPricingPolicyDto>(json);
                if (dto == null)
                {
                    continue;
                }

                return RecurringPricingPolicy.Create(
                    dto.FreeInitialUnits,
                    dto.FreeRenewalUnits);
            }
            catch
            {
                // ignore invalid payload and fallback to default mode
            }
        }

        return new FullyPaidRecurringPolicy();
    }
}
