using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services.Auth;

public interface ILoginIdentityResolver
{
    /// <summary>
    /// يحل حساب الدخول من: اسم مستخدم النظام، البريد، رقم الجوال، أو اسم مستخدم MikroTik للمشترك.
    /// </summary>
    Task<ApplicationUser?> ResolveAsync(string? login, CancellationToken cancellationToken = default);
}

public sealed class LoginIdentityResolver(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : ILoginIdentityResolver
{
    public async Task<ApplicationUser?> ResolveAsync(string? login, CancellationToken cancellationToken = default)
    {
        string input = (login ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return null;
        }

        ApplicationUser? user = await userManager.FindByNameAsync(input);
        if (user != null)
        {
            return user;
        }

        user = await userManager.FindByEmailAsync(input);
        if (user != null)
        {
            return user;
        }

        string digits = DigitsOnly(input);
        if (digits.Length >= 8)
        {
            user = await FindUserByPhoneAsync(digits, cancellationToken);
            if (user != null)
            {
                return user;
            }

            user = await FindUserViaClientPhoneAsync(digits, cancellationToken);
            if (user != null)
            {
                return user;
            }
        }

        return await FindUserViaClientUserNameAsync(input, cancellationToken);
    }

    private async Task<ApplicationUser?> FindUserByPhoneAsync(string digits, CancellationToken cancellationToken)
    {
        string[] tokens = PhoneSearchTokens(digits);
        string t0 = tokens[0];
        string t1 = tokens.Length > 1 ? tokens[1] : t0;
        string t2 = tokens.Length > 2 ? tokens[2] : t0;
        string t3 = tokens.Length > 3 ? tokens[3] : t0;

        List<ApplicationUser> candidates = await userManager.Users
            .Where(u => u.PhoneNumber != null
                        && u.PhoneNumber != ""
                        && (u.PhoneNumber.Contains(t0)
                            || u.PhoneNumber.Contains(t1)
                            || u.PhoneNumber.Contains(t2)
                            || u.PhoneNumber.Contains(t3)))
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(u => PhonesMatch(DigitsOnly(u.PhoneNumber), digits));
    }

    private async Task<ApplicationUser?> FindUserViaClientPhoneAsync(string digits, CancellationToken cancellationToken)
    {
        string[] tokens = PhoneSearchTokens(digits);
        string t0 = tokens[0];
        string t1 = tokens.Length > 1 ? tokens[1] : t0;
        string t2 = tokens.Length > 2 ? tokens[2] : t0;
        string t3 = tokens.Length > 3 ? tokens[3] : t0;

        var phoneRows = await db.Clients
            .AsNoTracking()
            .Where(c => c.PhoneNumber != null
                        && c.PhoneNumber != ""
                        && (c.PhoneNumber.Contains(t0)
                            || c.PhoneNumber.Contains(t1)
                            || c.PhoneNumber.Contains(t2)
                            || c.PhoneNumber.Contains(t3)))
            .Select(c => new { c.Id, c.PhoneNumber })
            .ToListAsync(cancellationToken);

        int? clientId = phoneRows
            .Where(c => PhonesMatch(DigitsOnly(c.PhoneNumber), digits))
            .Select(c => (int?)c.Id)
            .FirstOrDefault();

        if (!clientId.HasValue)
        {
            return null;
        }

        return await userManager.Users.FirstOrDefaultAsync(u => u.ClientId == clientId.Value, cancellationToken);
    }

    private async Task<ApplicationUser?> FindUserViaClientUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        string lowered = userName.ToLowerInvariant();
        int? clientId = await db.Clients
            .AsNoTracking()
            .Where(c => c.UserName != null && c.UserName.ToLower() == lowered)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!clientId.HasValue)
        {
            return null;
        }

        return await userManager.Users.FirstOrDefaultAsync(u => u.ClientId == clientId.Value, cancellationToken);
    }

    public static string DigitsOnly(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

    public static string[] PhoneSearchTokens(string digits)
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { digits };
        string needle = digits.Length <= 9 ? digits : digits[^9..];
        set.Add(needle);

        if (digits.StartsWith("963", StringComparison.Ordinal) && digits.Length > 3)
        {
            set.Add("0" + digits[3..]);
        }

        if (digits.StartsWith('0') && digits.Length > 1)
        {
            set.Add("963" + digits[1..]);
        }

        return set.ToArray();
    }

    public static bool PhonesMatch(string leftDigits, string rightDigits)
    {
        if (string.IsNullOrEmpty(leftDigits) || string.IsNullOrEmpty(rightDigits))
        {
            return false;
        }

        if (string.Equals(leftDigits, rightDigits, StringComparison.Ordinal))
        {
            return true;
        }

        string leftKey = NormalizePhoneKey(leftDigits);
        string rightKey = NormalizePhoneKey(rightDigits);
        if (leftKey.Length >= 8
            && rightKey.Length >= 8
            && string.Equals(leftKey, rightKey, StringComparison.Ordinal))
        {
            return true;
        }

        // تسامح إضافي إن بقي اختلاف في البادئة
        const int minSuffix = 9;
        if (leftDigits.Length >= minSuffix && rightDigits.Length >= minSuffix)
        {
            string shorter = leftDigits.Length <= rightDigits.Length ? leftDigits : rightDigits;
            string longer = leftDigits.Length <= rightDigits.Length ? rightDigits : leftDigits;
            if (longer.EndsWith(shorter, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (leftKey.Length >= minSuffix && rightKey.Length >= minSuffix)
        {
            string shorter = leftKey.Length <= rightKey.Length ? leftKey : rightKey;
            string longer = leftKey.Length <= rightKey.Length ? rightKey : leftKey;
            return longer.EndsWith(shorter, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>يزيل بادئة الدولة 963 والصفر المحلي للمقارنة.</summary>
    public static string NormalizePhoneKey(string digits)
    {
        if (digits.StartsWith("963", StringComparison.Ordinal) && digits.Length > 3)
        {
            digits = digits[3..];
        }

        if (digits.StartsWith('0') && digits.Length > 1)
        {
            digits = digits[1..];
        }

        return digits;
    }
}
