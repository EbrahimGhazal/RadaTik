using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Admin;

namespace RadaTik.Helpers;

public sealed class CreateEmployeeAccountValidationResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, List<string>> FieldErrors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> GeneralErrors { get; set; } = [];

    public void AddFieldError(string field, string message)
    {
        if (!FieldErrors.TryGetValue(field, out List<string>? list))
        {
            list = [];
            FieldErrors[field] = list;
        }

        if (!list.Contains(message))
        {
            list.Add(message);
        }

        IsValid = false;
    }

    public void AddGeneral(string message)
    {
        if (!GeneralErrors.Contains(message))
        {
            GeneralErrors.Add(message);
        }

        IsValid = false;
    }
}

public static class CreateEmployeeAccountValidator
{
    public static void ValidateRequiredFields(CreateEmployeeViewModel model, CreateEmployeeAccountValidationResult result)
    {
        string userName = (model.UserName ?? string.Empty).Trim();
        string email = (model.Email ?? string.Empty).Trim();
        string password = model.Password ?? string.Empty;
        string confirm = model.ConfirmPassword ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.UserName), "اسم المستخدم مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.Email), "البريد الإلكتروني مطلوب.");
        }
        else if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.Email), "صيغة البريد الإلكتروني غير صحيحة.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.Password), "كلمة المرور مطلوبة.");
        }
        else
        {
            foreach (string err in StrongPasswordRules.Validate(password, userName, email))
            {
                result.AddFieldError(nameof(CreateEmployeeViewModel.Password), err);
            }
        }

        if (string.IsNullOrWhiteSpace(confirm))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.ConfirmPassword), "تأكيد كلمة المرور مطلوب.");
        }
        else if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            result.AddFieldError(nameof(CreateEmployeeViewModel.ConfirmPassword), "كلمة المرور وتأكيدها غير متطابقتين.");
        }
    }

    public static async Task ValidateUniquenessAsync(
        UserManager<ApplicationUser> userManager,
        CreateEmployeeViewModel model,
        CreateEmployeeAccountValidationResult result,
        CancellationToken cancellationToken = default)
    {
        string userName = (model.UserName ?? string.Empty).Trim();
        string email = (model.Email ?? string.Empty).Trim();
        string? phone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        string? phoneDigits = NormalizePhoneDigits(phone);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            ApplicationUser? byName = await userManager.FindByNameAsync(userName);
            if (byName != null)
            {
                result.AddFieldError(nameof(CreateEmployeeViewModel.UserName), "اسم المستخدم مستخدم مسبقاً.");
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            ApplicationUser? byEmail = await userManager.FindByEmailAsync(email);
            if (byEmail != null)
            {
                result.AddFieldError(nameof(CreateEmployeeViewModel.Email), "البريد الإلكتروني مستخدم مسبقاً.");
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            List<string> existingPhones = await userManager.Users
                .AsNoTracking()
                .Where(u => u.PhoneNumber != null && u.PhoneNumber != "")
                .Select(u => u.PhoneNumber!)
                .ToListAsync(cancellationToken);

            bool duplicate = existingPhones.Any(p =>
            {
                string normalized = p.Trim();
                if (string.Equals(normalized, phone, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string? digits = NormalizePhoneDigits(normalized);
                return !string.IsNullOrWhiteSpace(phoneDigits) &&
                       !string.IsNullOrWhiteSpace(digits) &&
                       digits == phoneDigits;
            });

            if (duplicate)
            {
                result.AddFieldError(nameof(CreateEmployeeViewModel.PhoneNumber), "رقم الجوال مستخدم مسبقاً.");
            }
        }
    }

    public static async Task<CreateEmployeeAccountValidationResult> ValidateAsync(
        UserManager<ApplicationUser> userManager,
        CreateEmployeeViewModel model,
        CancellationToken cancellationToken = default)
    {
        CreateEmployeeAccountValidationResult result = new CreateEmployeeAccountValidationResult { IsValid = true };
        ValidateRequiredFields(model, result);
        await ValidateUniquenessAsync(userManager, model, result, cancellationToken);
        return result;
    }

    private static string? NormalizePhoneDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        string digits = new string(phone.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }
}
