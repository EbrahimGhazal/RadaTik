using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data.Sql;
using RadaTik.Security;

namespace RadaTik.Data;

/// <summary>استدعاء الإجراء المخزّن لإنشاء حساب مدير النظام بعد جاهزية جداول Identity.</summary>
public static class SystemAdminDatabaseBootstrap
{
    public const string ProcedureName = "dbo.usp_EnsureDefaultSystemAdministrator";

    public static async Task EnsureDefaultSystemAdministratorAsync(
        ApplicationDbContext db,
        ILogger logger,
        bool resetPasswordIfExists = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureStoredProcedureInstalledAsync(db, logger, cancellationToken);

        if (!await ProcedureExistsAsync(db, cancellationToken))
        {
            logger.LogWarning(
                "تعذر إنشاء الإجراء {Procedure}. سيتم الاعتماد على بذور UserManager.",
                ProcedureName);
            return;
        }

        SystemAdminBootstrapIdentityValues identity = SystemAdminBootstrapIdentityValues.Create();

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"EXEC {ProcedureName} @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @FullName, @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @RoleName, @NormalizedRoleName, @ResetPasswordIfExists",
                [
                    new SqlParameter("@UserName", SystemAdminBootstrapDefaults.UserName),
                    new SqlParameter("@NormalizedUserName", SystemAdminBootstrapDefaults.UserName.ToUpperInvariant()),
                    new SqlParameter("@Email", SystemAdminBootstrapDefaults.Email),
                    new SqlParameter("@NormalizedEmail", SystemAdminBootstrapDefaults.Email.ToUpperInvariant()),
                    new SqlParameter("@FullName", SystemAdminBootstrapDefaults.FullName),
                    new SqlParameter("@PasswordHash", identity.PasswordHash),
                    new SqlParameter("@SecurityStamp", identity.SecurityStamp),
                    new SqlParameter("@ConcurrencyStamp", identity.ConcurrencyStamp),
                    new SqlParameter("@RoleName", RoleNames.SystemAdministrator),
                    new SqlParameter("@NormalizedRoleName", RoleNames.SystemAdministrator.ToUpperInvariant()),
                    new SqlParameter("@ResetPasswordIfExists", resetPasswordIfExists)
                ],
                cancellationToken);

            logger.LogInformation(
                "تم تنفيذ {Procedure} للمستخدم {UserName} (تغيير كلمة المرور مطلوب عند أول دخول).",
                ProcedureName,
                SystemAdminBootstrapDefaults.UserName);
        }
        catch (Exception ex)
        {
            // لا نرمي الاستثناء: مسار UserManager يكمل إنشاء الحساب عند فشل SQL.
            logger.LogWarning(
                ex,
                "فشل تنفيذ {Procedure}. سيتم الاعتماد على بذور UserManager.",
                ProcedureName);
        }
    }

    public static async Task EnsureStoredProcedureInstalledAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // CREATE OR ALTER — يحدّث الإجراء دائماً ليتوافق مع أعمدة AspNetUsers الجديدة
            await db.Database.ExecuteSqlRawAsync(
                SystemAdminBootstrapSqlScripts.EnsureDefaultSystemAdministratorProcedure,
                cancellationToken);
            logger.LogInformation("تم تثبيت/تحديث الإجراء المخزّن {Procedure}.", ProcedureName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "فشل تثبيت الإجراء {Procedure}. سيتم الاعتماد على بذور UserManager.", ProcedureName);
        }
    }

    private static async Task<bool> ProcedureExistsAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT OBJECT_ID(N'dbo.usp_EnsureDefaultSystemAdministrator', N'P')";
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null and not DBNull;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
