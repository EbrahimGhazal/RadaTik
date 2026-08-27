using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Helpers;
using RadaTik.Security;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed partial class ClientProvisioningService
{
    public async Task<ClientEditOutcome> UpdateClientAsync(ClientEditRequest request, CancellationToken ct = default)
    {
        Client? existingClient = await Db.Clients
            .FirstOrDefaultAsync(c => c.Id == request.ClientId && c.NetworkId == request.NetworkId, ct);
        if (existingClient == null)
        {
            return ClientEditOutcome.NotFound();
        }

        Client? originalClient = await Db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ClientId && c.NetworkId == request.NetworkId, ct);
        if (originalClient == null)
        {
            return ClientEditOutcome.NotFound();
        }

        ClientEditRequest effectiveRequest = request with
        {
            ApplyMikroTikChanges = request.ApplyMikroTikChanges && !request.IsEmployee
        };

        Client submitted = effectiveRequest.SubmittedClient;
        if (effectiveRequest.IsEmployee)
        {
            ApplyEmployeeEditRestrictions(submitted, originalClient);
        }
        else if (!effectiveRequest.ApplyMikroTikChanges)
        {
            PreserveMikroTikIdentity(submitted, originalClient);
        }

        try
        {
            if (effectiveRequest.IsEmployee)
            {
                return await SubmitEmployeeEditApprovalAsync(effectiveRequest, existingClient, submitted, ct);
            }

            return await ApplyAdministratorEditAsync(
                effectiveRequest,
                existingClient,
                originalClient,
                submitted,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في تعديل العميل {ClientId}", request.ClientId);
            return ClientEditOutcome.Failed(MikroTikErrorFormatter.Format("خطأ في التعديل", ex.Message));
        }
    }

    private static void ApplyEmployeeEditRestrictions(Client submitted, Client original)
    {
        submitted.UserName = original.UserName;
        submitted.Password = null;
        submitted.ProfileId = original.ProfileId;
        submitted.ProfileName = original.ProfileName;
        submitted.IsActive = original.IsActive;
        submitted.MikroTikServerId = original.MikroTikServerId;
        submitted.AccountExpirationDate = original.AccountExpirationDate;
        submitted.Service = original.Service;
        submitted.Address = original.Address;
        submitted.Uptime = original.Uptime;
        submitted.ConnectionStatus = original.ConnectionStatus;
        submitted.MacAddress = original.MacAddress;
        submitted.Balance = original.Balance;
        submitted.ServiceStartDate = original.ServiceStartDate;
        submitted.ServiceEndDate = original.ServiceEndDate;
        submitted.NextBillingDate = original.NextBillingDate;
        submitted.VipBenefitKind = original.VipBenefitKind;
        submitted.VipDiscountPercent = original.VipDiscountPercent;
    }

    private static void PreserveMikroTikIdentity(Client submitted, Client original)
    {
        submitted.UserName = original.UserName;
        submitted.Password = null;
        submitted.MikroTikServerId = original.MikroTikServerId;
        submitted.ProfileId = original.ProfileId;
        submitted.ProfileName = original.ProfileName;
        submitted.Service = original.Service;
        submitted.Address = original.Address;
        submitted.IsActive = original.IsActive;
        submitted.Uptime = original.Uptime;
        submitted.ConnectionStatus = original.ConnectionStatus;
        submitted.MacAddress = original.MacAddress;
        submitted.AccountExpirationDate = original.AccountExpirationDate;
    }

    private async Task<ClientEditOutcome> SubmitEmployeeEditApprovalAsync(
        ClientEditRequest request,
        Client existingClient,
        Client submitted,
        CancellationToken ct)
    {
        ClientApprovalPayload payload = new()
        {
            Name = submitted.Name,
            UserName = submitted.UserName,
            Password = string.IsNullOrWhiteSpace(submitted.Password) ? null : submitted.Password,
            PhoneNumber = submitted.PhoneNumber,
            ResidenceAddress = submitted.ResidenceAddress,
            Occupation = submitted.Occupation,
            Workplace = submitted.Workplace,
            Latitude = submitted.Latitude,
            Longitude = submitted.Longitude,
            PowerSource = submitted.PowerSource,
            Building = submitted.Building,
            Floor = submitted.Floor,
            ReceiverId = submitted.ReceiverId,
            IsVip = submitted.IsVip,
            VipNote = submitted.VipNote,
            DbUserName = request.DbUserName,
            DbPassword = request.DbPassword
        };

        string? requestNotes = EmployeeApprovalRequestHelper.BuildClientEdit(existingClient.Id, payload);
        if (string.IsNullOrWhiteSpace(requestNotes))
        {
            return ClientEditOutcome.Failed("تعذر إنشاء طلب الموافقة: حجم البيانات كبير جداً.");
        }

        await _approvalRequests.CreatePendingAsync(
            request.NetworkId,
            request.ActorUserId,
            FeatureKeys.Clients,
            requestNotes,
            0m,
            ct);

        return ClientEditOutcome.EmployeePending("تم إرسال تعديل العميل كطلب موافقة لمدير الشركة.");
    }

    private async Task<ClientEditOutcome> ApplyAdministratorEditAsync(
        ClientEditRequest request,
        Client existingClient,
        Client originalClient,
        Client submitted,
        CancellationToken ct)
    {
        Profile? profile = await Db.Profiles.FindAsync([submitted.ProfileId], ct);
        if (profile == null)
        {
            throw new InvalidOperationException("البروفايل المحدد غير موجود");
        }

        existingClient.Name = submitted.Name;
        existingClient.UserName = submitted.UserName;
        existingClient.PhoneNumber = submitted.PhoneNumber;
        existingClient.ProfileId = submitted.ProfileId;
        existingClient.ProfileName = profile.Name;
        existingClient.ReceiverId = submitted.ReceiverId;
        existingClient.MikroTikServerId = submitted.MikroTikServerId;
        existingClient.AccountExpirationDate = submitted.AccountExpirationDate;
        existingClient.ResidenceAddress = submitted.ResidenceAddress;
        existingClient.Occupation = string.IsNullOrWhiteSpace(submitted.Occupation) ? null : submitted.Occupation.Trim();
        existingClient.Workplace = string.IsNullOrWhiteSpace(submitted.Workplace) ? null : submitted.Workplace.Trim();
        existingClient.Latitude = submitted.Latitude;
        existingClient.Longitude = submitted.Longitude;
        existingClient.PowerSource = submitted.PowerSource;
        existingClient.Building = submitted.Building;
        existingClient.Floor = submitted.Floor;
        existingClient.ServiceStartDate = submitted.ServiceStartDate;
        ClientVipAssignment.Apply(
            existingClient,
            submitted.IsVip,
            submitted.VipNote,
            DateTime.Now,
            submitted.VipBenefitKind,
            submitted.VipDiscountPercent);
        existingClient.LastUpdated = DateTime.Now;
        existingClient.NetworkId = request.NetworkId;

        if (!string.IsNullOrWhiteSpace(submitted.Password))
        {
            existingClient.Password = submitted.Password;
        }

        existingClient.IsActive = submitted.IsActive;
        existingClient.Service = submitted.Service;
        existingClient.Address = submitted.Address;
        existingClient.ConnectionStatus = submitted.IsActive ? "مفعل" : "معطل";

        if (request.ApplyMikroTikChanges)
        {
            await PushEditedClientToMikroTikAsync(existingClient, originalClient, ct);
        }
        else
        {
            await Db.SaveChangesAsync(ct);
        }

        await UpdateLinkedIdentityUserAsync(existingClient, request, ct);

        return ClientEditOutcome.Success(request.ApplyMikroTikChanges
            ? "تم تعديل بيانات العميل بنجاح في قاعدة البيانات والمايكروتك"
            : "تم تعديل بيانات العميل في قاعدة البيانات دون تغيير إعدادات MikroTik");
    }

    private async Task PushEditedClientToMikroTikAsync(
        Client existingClient,
        Client originalClient,
        CancellationToken ct)
    {
        int? originalServerId = originalClient.MikroTikServerId;
        int? newServerId = existingClient.MikroTikServerId;
        string? originalUserName = originalClient.UserName;
        bool userNameChanged = !string.Equals(originalClient.UserName, existingClient.UserName, StringComparison.Ordinal);

        if (originalServerId.HasValue && newServerId.HasValue && originalServerId.Value != newServerId.Value)
        {
            bool existsOnNewServer = await _mikroTik.CheckUserExists(existingClient.UserName!, newServerId.Value);
            if (!existsOnNewServer)
            {
                await _mikroTik.AddPPPoEUser(existingClient);
            }
            else
            {
                await _mikroTik.UpdatePPPoEUser(existingClient);
            }

            await Db.SaveChangesAsync(ct);
            if (!string.IsNullOrEmpty(originalUserName))
            {
                await _mikroTik.DeletePPPoEUser(originalUserName, originalServerId.Value);
            }

            return;
        }

        if (existingClient.MikroTikServerId.HasValue)
        {
            if (userNameChanged)
            {
                await _mikroTik.UpdatePPPoEUserWithOriginalUsername(
                    existingClient,
                    originalUserName ?? string.Empty);
            }
            else
            {
                await _mikroTik.UpdatePPPoEUser(existingClient);
            }
        }

        await Db.SaveChangesAsync(ct);
    }

    private async Task UpdateLinkedIdentityUserAsync(
        Client existingClient,
        ClientEditRequest request,
        CancellationToken ct)
    {
        ApplicationUser? linkedUser = await Db.Users
            .FirstOrDefaultAsync(u => u.ClientId == existingClient.Id, ct);
        if (linkedUser == null)
        {
            return;
        }

        string? normalizedDbUserName = string.IsNullOrWhiteSpace(request.DbUserName)
            ? linkedUser.UserName
            : request.DbUserName.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedDbUserName) &&
            !string.Equals(linkedUser.UserName, normalizedDbUserName, StringComparison.Ordinal))
        {
            IdentityResult setUserNameResult = await _userManager.SetUserNameAsync(linkedUser, normalizedDbUserName);
            if (!setUserNameResult.Succeeded)
            {
                _logger.LogWarning(
                    "فشل تحديث اسم المستخدم في النظام: {Errors}",
                    string.Join(", ", setUserNameResult.Errors.Select(e => e.Description)));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DbPassword))
        {
            string token = await _userManager.GeneratePasswordResetTokenAsync(linkedUser);
            IdentityResult resetResult = await _userManager.ResetPasswordAsync(
                linkedUser,
                token,
                request.DbPassword.Trim());
            if (!resetResult.Succeeded)
            {
                _logger.LogWarning(
                    "فشل تحديث كلمة المرور في النظام: {Errors}",
                    string.Join(", ", resetResult.Errors.Select(e => e.Description)));
            }
        }

        linkedUser.FullName = existingClient.Name;
        linkedUser.PhoneNumber = existingClient.PhoneNumber;
        if (!request.IsEmployee)
        {
            linkedUser.IsActive = existingClient.IsActive;
        }

        await _userManager.UpdateAsync(linkedUser);
    }
}
