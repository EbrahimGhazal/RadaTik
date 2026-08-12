namespace RadaTik.Data.Sql;

internal static class SystemAdminBootstrapSqlScripts
{
    public const string EnsureDefaultSystemAdministratorProcedure = """
        CREATE OR ALTER PROCEDURE dbo.usp_EnsureDefaultSystemAdministrator
            @UserName NVARCHAR(256) = N'admin',
            @NormalizedUserName NVARCHAR(256) = N'ADMIN',
            @Email NVARCHAR(256) = N'admin@radatik.local',
            @NormalizedEmail NVARCHAR(256) = N'ADMIN@RADATIK.LOCAL',
            @FullName NVARCHAR(100) = N'مدير النظام',
            @PasswordHash NVARCHAR(MAX),
            @SecurityStamp NVARCHAR(MAX),
            @ConcurrencyStamp NVARCHAR(MAX),
            @RoleName NVARCHAR(256) = N'SystemAdministrator',
            @NormalizedRoleName NVARCHAR(256) = N'SYSTEMADMINISTRATOR',
            @ResetPasswordIfExists BIT = 0
        AS
        BEGIN
            SET NOCOUNT ON;
            IF @PasswordHash IS NULL OR LEN(@PasswordHash) = 0
            BEGIN
                RAISERROR(N'PasswordHash is required for usp_EnsureDefaultSystemAdministrator.', 16, 1);
                RETURN;
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUsers')
                RETURN;
            IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = @NormalizedRoleName)
                INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
                VALUES (CAST(NEWID() AS NVARCHAR(450)), @RoleName, @NormalizedRoleName, CAST(NEWID() AS NVARCHAR(MAX)));
            DECLARE @RoleId NVARCHAR(450) = (SELECT TOP (1) Id FROM AspNetRoles WHERE NormalizedName = @NormalizedRoleName);
            DECLARE @UserId NVARCHAR(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedUserName = @NormalizedUserName);
            IF @UserId IS NULL
                SELECT @UserId = Id FROM AspNetUsers WHERE NormalizedUserName = N'ADMIN' AND UserName = N'Admin';
            IF @UserId IS NULL
            BEGIN
                SET @UserId = CAST(NEWID() AS NVARCHAR(450));
                INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
                    PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
                    FullName, IsActive, MustChangePassword, CreatedDate, EmployeeDepartment)
                VALUES (@UserId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, 1,
                    @PasswordHash, @SecurityStamp, @ConcurrencyStamp, 0, 0, 1, 0, @FullName, 1, 1, SYSUTCDATETIME(), 0);
            END
            ELSE
            BEGIN
                UPDATE AspNetUsers SET UserName = @UserName, NormalizedUserName = @NormalizedUserName,
                    Email = COALESCE(NULLIF(Email, N''), @Email), NormalizedEmail = COALESCE(NULLIF(NormalizedEmail, N''), @NormalizedEmail),
                    EmailConfirmed = 1, IsActive = 1,
                    MustChangePassword = CASE WHEN PasswordChangedAt IS NULL THEN 1 ELSE MustChangePassword END,
                    FullName = COALESCE(NULLIF(FullName, N''), @FullName),
                    PasswordHash = CASE WHEN @ResetPasswordIfExists = 1 THEN @PasswordHash ELSE PasswordHash END,
                    SecurityStamp = CASE WHEN @ResetPasswordIfExists = 1 THEN @SecurityStamp ELSE SecurityStamp END,
                    ConcurrencyStamp = CASE WHEN @ResetPasswordIfExists = 1 THEN @ConcurrencyStamp ELSE ConcurrencyStamp END
                WHERE Id = @UserId;
            END;
            IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
                INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
        END;
        """;
}
