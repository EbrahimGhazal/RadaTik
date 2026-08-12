using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Security;

/// <summary>
/// Ensures the default system administrator account exists after Identity tables are created.
/// Default credentials: username <see cref="SystemAdminBootstrapDefaults.UserName"/> /
/// password <see cref="SystemAdminBootstrapDefaults.Password"/>.
/// </summary>
public static class DefaultSystemAdministratorAccountBootstrap
{
    /// <summary>
    /// Creates roles (if needed) and the default admin account once <c>AspNetUsers</c> and <c>AspNetRoles</c> exist.
    /// Safe to call on every startup; skips creation when the bootstrap admin is already present.
    /// </summary>
    public static async Task EnsureWhenIdentityTablesExistAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DefaultSystemAdministratorAccountBootstrap");

        await EnsureWhenIdentityTablesExistAsync(db, userManager, roleManager, logger, cancellationToken);
    }

    public static async Task EnsureWhenIdentityTablesExistAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!await IdentityTablesExistAsync(db, cancellationToken))
        {
            logger.LogDebug(
                "Skipped default admin bootstrap: Identity tables (AspNetUsers/AspNetRoles) are not ready yet.");
            return;
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.SystemAdministrator))
        {
            IdentityResult roleResult = await roleManager.CreateAsync(new IdentityRole(RoleNames.SystemAdministrator));
            if (!roleResult.Succeeded)
            {
                logger.LogWarning(
                    "Could not create role {RoleName}: {Errors}",
                    RoleNames.SystemAdministrator,
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        await SystemAdminDatabaseBootstrap.EnsureDefaultSystemAdministratorAsync(
            db,
            logger,
            cancellationToken: cancellationToken);

        await EnsureViaUserManagerAsync(userManager, roleManager, logger, cancellationToken);
    }

    private static async Task EnsureViaUserManagerAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (!await roleManager.RoleExistsAsync(RoleNames.SystemAdministrator))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.SystemAdministrator));
        }

        ApplicationUser? bootstrapUser = await userManager.FindByNameAsync(SystemAdminBootstrapDefaults.UserName)
            ?? await userManager.FindByEmailAsync(SystemAdminBootstrapDefaults.Email);

        if (bootstrapUser != null)
        {
            await SyncExistingAdministratorAsync(userManager, bootstrapUser, logger);
            if (!await userManager.IsInRoleAsync(bootstrapUser, RoleNames.SystemAdministrator))
            {
                IdentityResult addRole = await userManager.AddToRoleAsync(bootstrapUser, RoleNames.SystemAdministrator);
                if (!addRole.Succeeded)
                {
                    logger.LogWarning(
                        "Could not add role {RoleName} to existing {UserName}: {Errors}",
                        RoleNames.SystemAdministrator,
                        bootstrapUser.UserName,
                        string.Join("; ", addRole.Errors.Select(e => e.Description)));
                }
            }

            return;
        }

        ApplicationUser? legacyAdmin = await userManager.FindByNameAsync("Admin");
        if (legacyAdmin != null)
        {
            await SyncExistingAdministratorAsync(userManager, legacyAdmin, logger);
            if (!await userManager.IsInRoleAsync(legacyAdmin, RoleNames.SystemAdministrator))
            {
                await userManager.AddToRoleAsync(legacyAdmin, RoleNames.SystemAdministrator);
            }

            logger.LogInformation(
                "Synced legacy system administrator (Admin) to {UserName}.",
                SystemAdminBootstrapDefaults.UserName);
            return;
        }

        IList<ApplicationUser> systemAdmins = await userManager.GetUsersInRoleAsync(RoleNames.SystemAdministrator);
        if (systemAdmins.Count > 0)
        {
            logger.LogInformation(
                "{Count} system administrator(s) already exist; default account {UserName} was not created.",
                systemAdmins.Count,
                SystemAdminBootstrapDefaults.UserName);
            return;
        }

        ApplicationUser user = new()
        {
            UserName = SystemAdminBootstrapDefaults.UserName,
            Email = SystemAdminBootstrapDefaults.Email,
            FullName = SystemAdminBootstrapDefaults.FullName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            EmployeeDepartment = EmployeeDepartment.None,
            CreatedDate = DateTime.UtcNow
        };

        IdentityResult createResult;
        using (BootstrapPasswordValidationScope.Enter())
        {
            createResult = await userManager.CreateAsync(user, SystemAdminBootstrapDefaults.Password);
        }

        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Could not create default system administrator ({UserName}): {Errors}",
                SystemAdminBootstrapDefaults.UserName,
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, RoleNames.SystemAdministrator);
        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Created user {UserName} but could not assign {RoleName}: {Errors}",
                user.UserName,
                RoleNames.SystemAdministrator,
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation(
            "Created default system administrator automatically (username: {UserName}). Change password on first login.",
            SystemAdminBootstrapDefaults.UserName);
    }

    private static async Task SyncExistingAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser existing,
        ILogger logger)
    {
        bool needsUpdate = false;

        if (!string.Equals(existing.UserName, SystemAdminBootstrapDefaults.UserName, StringComparison.Ordinal))
        {
            existing.UserName = SystemAdminBootstrapDefaults.UserName;
            existing.NormalizedUserName = userManager.NormalizeName(SystemAdminBootstrapDefaults.UserName);
            needsUpdate = true;
        }

        if (string.IsNullOrWhiteSpace(existing.Email))
        {
            existing.Email = SystemAdminBootstrapDefaults.Email;
            existing.NormalizedEmail = userManager.NormalizeEmail(SystemAdminBootstrapDefaults.Email);
            existing.EmailConfirmed = true;
            needsUpdate = true;
        }

        if (!existing.IsActive)
        {
            existing.IsActive = true;
            needsUpdate = true;
        }

        if (!existing.PasswordChangedAt.HasValue && !existing.MustChangePassword)
        {
            existing.MustChangePassword = true;
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            IdentityResult updateResult = await userManager.UpdateAsync(existing);
            if (!updateResult.Succeeded)
            {
                logger.LogWarning(
                    "Could not update system administrator ({UserName}): {Errors}",
                    existing.UserName,
                    string.Join("; ", updateResult.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(existing, RoleNames.SystemAdministrator))
        {
            IdentityResult addRole = await userManager.AddToRoleAsync(existing, RoleNames.SystemAdministrator);
            if (!addRole.Succeeded)
            {
                logger.LogWarning(
                    "Could not add role {RoleName} to {UserName}: {Errors}",
                    RoleNames.SystemAdministrator,
                    existing.UserName,
                    string.Join("; ", addRole.Errors.Select(e => e.Description)));
            }
        }
    }

    public static async Task<bool> IdentityTablesExistAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        return await IdentityTableExistsAsync(db, "AspNetRoles", cancellationToken)
            && await IdentityTableExistsAsync(db, "AspNetUsers", cancellationToken);
    }

    private static async Task<bool> IdentityTableExistsAsync(
        ApplicationDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        try
        {
            DbConnection conn = db.Database.GetDbConnection();
            bool wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
            {
                await conn.OpenAsync(cancellationToken);
            }

            try
            {
                await using DbCommand cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = @tableName
                    """;
                DbParameter param = cmd.CreateParameter();
                param.ParameterName = "@tableName";
                param.Value = tableName;
                cmd.Parameters.Add(param);
                object? scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(scalar) > 0;
            }
            finally
            {
                if (!wasOpen)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch
        {
            return false;
        }
    }
}
