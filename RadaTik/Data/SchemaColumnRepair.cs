using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RadaTik.Data;

/// <summary>
/// إصلاح أعمدة نُسجّلت في Model/Snapshot لكن لم تُطبَّق فعلياً (هجرات فارغة أو قاعدة قديمة).
/// </summary>
public static class SchemaColumnRepair
{
    public static async Task EnsurePendingColumnsAsync(ApplicationDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        await EnsureColumnAsync(db, "AspNetUsers", "EmployeeDepartment", """
            ALTER TABLE [dbo].[AspNetUsers] ADD [EmployeeDepartment] int NOT NULL
                CONSTRAINT [DF_AspNetUsers_EmployeeDepartment] DEFAULT (0);
            """, logger, ct);

        await EnsureColumnAsync(db, "FeaturePublicInfos", "RenewalPolicyHtml", """
            ALTER TABLE [dbo].[FeaturePublicInfos] ADD [RenewalPolicyHtml] nvarchar(max) NULL;
            """, logger, ct);

        await EnsureColumnAsync(db, "Clients", "IsVip", """
            ALTER TABLE [dbo].[Clients] ADD [IsVip] bit NOT NULL
                CONSTRAINT [DF_Clients_IsVip] DEFAULT (0);
            """, logger, ct);
        await EnsureColumnAsync(db, "Clients", "VipNote", """
            ALTER TABLE [dbo].[Clients] ADD [VipNote] nvarchar(200) NULL;
            """, logger, ct);
        await EnsureColumnAsync(db, "Clients", "VipSince", """
            ALTER TABLE [dbo].[Clients] ADD [VipSince] datetime2 NULL;
            """, logger, ct);
        await EnsureColumnAsync(db, "Clients", "Occupation", """
            ALTER TABLE [dbo].[Clients] ADD [Occupation] nvarchar(100) NULL;
            """, logger, ct);
        await EnsureColumnAsync(db, "Clients", "Workplace", """
            ALTER TABLE [dbo].[Clients] ADD [Workplace] nvarchar(200) NULL;
            """, logger, ct);

        await EnsureColumnAsync(db, "Networks", "VipDiscountPercent", """
            ALTER TABLE [dbo].[Networks] ADD [VipDiscountPercent] decimal(5,2) NOT NULL
                CONSTRAINT [DF_Networks_VipDiscountPercent] DEFAULT (0);
            """, logger, ct);
        await EnsureColumnAsync(db, "Networks", "VipGraceDays", """
            ALTER TABLE [dbo].[Networks] ADD [VipGraceDays] int NOT NULL
                CONSTRAINT [DF_Networks_VipGraceDays] DEFAULT (0);
            """, logger, ct);
        await EnsureColumnAsync(db, "Networks", "VipSkipAutoDisable", """
            ALTER TABLE [dbo].[Networks] ADD [VipSkipAutoDisable] bit NOT NULL
                CONSTRAINT [DF_Networks_VipSkipAutoDisable] DEFAULT (0);
            """, logger, ct);
    }

    private static async Task EnsureColumnAsync(
        ApplicationDbContext db,
        string tableName,
        string columnName,
        string addColumnSql,
        ILogger? logger,
        CancellationToken ct)
    {
        bool exists = await ColumnExistsAsync(db, tableName, columnName, ct);
        if (exists)
        {
            return;
        }

        logger?.LogWarning(
            "إصلاح Schema: إضافة العمود {Column} إلى {Table} لأنه مفقود في قاعدة البيانات.",
            columnName,
            tableName);

        await db.Database.ExecuteSqlRawAsync(addColumnSql, ct);
    }

    private static async Task<bool> ColumnExistsAsync(
        ApplicationDbContext db,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        int count = await db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(1) AS [Value]
                FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = N'dbo' AND t.name = {0} AND c.name = {1}
                """,
                tableName,
                columnName)
            .SingleAsync(ct);

        return count > 0;
    }
}
