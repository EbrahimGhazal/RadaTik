using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik;

internal static class ProgramCommandTasks
{
    public static bool IsBootstrapAdminCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase));

    public static bool IsEnsureDefaultAdminSqlCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--ensure-default-admin-sql", StringComparison.OrdinalIgnoreCase));

    public static bool IsReencryptSensitiveFieldsCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--reencrypt-sensitive-fields", StringComparison.OrdinalIgnoreCase));

    public static bool IsPrintBootstrapIdentityCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--print-bootstrap-identity", StringComparison.OrdinalIgnoreCase));

    public static void PrintBootstrapIdentityValues()
    {
        SystemAdminBootstrapIdentityValues values = SystemAdminBootstrapIdentityValues.Create();
        Console.WriteLine(values.PasswordHash);
        Console.WriteLine(values.SecurityStamp);
        Console.WriteLine(values.ConcurrencyStamp);
    }

    public static async Task EnsureDefaultAdminViaSqlAsync(IServiceProvider serviceProvider, bool resetPassword)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await SystemAdminDatabaseBootstrap.EnsureDefaultSystemAdministratorAsync(db, logger, resetPassword);
        await DefaultSystemAdministratorAccountBootstrap.EnsureWhenIdentityTablesExistAsync(serviceProvider);
    }

    public static async Task BootstrapSystemAdministratorAsync(IServiceProvider serviceProvider, string[] args)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        RoleManager<IdentityRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        string userName = GetCommandArgument(args, "--username") ?? SystemAdminBootstrapDefaults.UserName;
        string email = GetCommandArgument(args, "--email") ?? SystemAdminBootstrapDefaults.Email;
        string fullName = GetCommandArgument(args, "--full-name") ?? SystemAdminBootstrapDefaults.FullName;
        string? password = GetCommandArgument(args, "--password")
            ?? Environment.GetEnvironmentVariable("RADATIK_BOOTSTRAP_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Bootstrap admin password is required. Use --password or RADATIK_BOOTSTRAP_ADMIN_PASSWORD.");
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.SystemAdministrator))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.SystemAdministrator));
        }

        ApplicationUser? user = await userManager.FindByNameAsync(userName) ?? await userManager.FindByEmailAsync(email);
        bool resetPassword = args.Any(a => string.Equals(a, "--reset-password", StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = true,
                EmployeeDepartment = EmployeeDepartment.None,
                CreatedDate = DateTime.UtcNow
            };

            IdentityResult createResult;
            using (BootstrapPasswordValidationScope.Enter())
            {
                createResult = await userManager.CreateAsync(user, password);
            }
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Bootstrap admin create failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }
        else if (resetPassword)
        {
            string token = await userManager.GeneratePasswordResetTokenAsync(user);
            IdentityResult resetResult;
            using (BootstrapPasswordValidationScope.Enter())
            {
                resetResult = await userManager.ResetPasswordAsync(user, token, password);
            }

            if (!resetResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Bootstrap admin password reset failed: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }

            user.IsActive = true;
            user.MustChangePassword = true;
            user.PasswordChangedAt = null;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Reset password for bootstrap admin {UserName}.", user.UserName);
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.SystemAdministrator))
        {
            await userManager.AddToRoleAsync(user, RoleNames.SystemAdministrator);
        }

        if (!user.MustChangePassword && !user.PasswordChangedAt.HasValue)
        {
            user.MustChangePassword = true;
            await userManager.UpdateAsync(user);
        }

        logger.LogInformation("Bootstrap admin command completed for user {UserName}.", user.UserName);
    }

    public static async Task ReencryptSensitiveFieldsAsync(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        int clientCount = 0;
        int serverCount = 0;

        List<Client> clients = await db.Clients.Where(c => !string.IsNullOrWhiteSpace(c.Password)).ToListAsync();
        foreach (Client? client in clients)
        {
            db.Entry(client).Property(x => x.Password).IsModified = true;
            clientCount++;
        }

        List<MikroTikServer> servers = await db.MikroTikServers.Where(s => !string.IsNullOrWhiteSpace(s.Pass)).ToListAsync();
        foreach (MikroTikServer? server in servers)
        {
            db.Entry(server).Property(x => x.Pass).IsModified = true;
            serverCount++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Sensitive fields re-encryption command completed. Clients={ClientCount}, MikroTikServers={ServerCount}.",
            clientCount,
            serverCount);
    }

    private static string? GetCommandArgument(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return args[i + 1];
        }

        return null;
    }
}
