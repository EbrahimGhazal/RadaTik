using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik;

internal static class ProgramCommandTasks
{
    public static bool IsBootstrapAdminCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase));

    public static bool IsReencryptSensitiveFieldsCommand(string[] args) =>
        args.Any(arg => string.Equals(arg, "--reencrypt-sensitive-fields", StringComparison.OrdinalIgnoreCase));

    public static async Task BootstrapSystemAdministratorAsync(IServiceProvider serviceProvider, string[] args)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = GetCommandArgument(args, "--username") ?? "admin";
        var email = GetCommandArgument(args, "--email") ?? "admin@radtik.com";
        var fullName = GetCommandArgument(args, "--full-name") ?? "مدير النظام";
        var password = GetCommandArgument(args, "--password")
            ?? Environment.GetEnvironmentVariable("RADTIK_BOOTSTRAP_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Bootstrap admin password is required. Use --password or RADTIK_BOOTSTRAP_ADMIN_PASSWORD.");
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.SystemAdministrator))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.SystemAdministrator));
        }

        var user = await userManager.FindByNameAsync(userName) ?? await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Bootstrap admin create failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.SystemAdministrator))
        {
            await userManager.AddToRoleAsync(user, RoleNames.SystemAdministrator);
        }

        logger.LogInformation("Bootstrap admin command completed for user {UserName}.", user.UserName);
    }

    public static async Task ReencryptSensitiveFieldsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var clientCount = 0;
        var serverCount = 0;

        var clients = await db.Clients.Where(c => !string.IsNullOrWhiteSpace(c.Password)).ToListAsync();
        foreach (var client in clients)
        {
            db.Entry(client).Property(x => x.Password).IsModified = true;
            clientCount++;
        }

        var servers = await db.MikroTikServers.Where(s => !string.IsNullOrWhiteSpace(s.Pass)).ToListAsync();
        foreach (var server in servers)
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
        for (var i = 0; i < args.Length - 1; i++)
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
