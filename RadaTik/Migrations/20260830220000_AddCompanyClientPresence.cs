using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RadaTik.Data;

#nullable disable

namespace RadaTik.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260830220000_AddCompanyClientPresence")]
    public partial class AddCompanyClientPresence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.CompanySocialLinks', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CompanySocialLinks] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [CompanyNetworkId] int NOT NULL,
                        [Platform] int NOT NULL,
                        [DisplayName] nvarchar(80) NOT NULL,
                        [Url] nvarchar(500) NOT NULL,
                        [IsVisibleToClients] bit NOT NULL CONSTRAINT [DF_CompanySocialLinks_IsVisibleToClients] DEFAULT (1),
                        [SortOrder] int NOT NULL CONSTRAINT [DF_CompanySocialLinks_SortOrder] DEFAULT (0),
                        [UpdatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_CompanySocialLinks_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT [PK_CompanySocialLinks] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CompanySocialLinks_Networks_CompanyNetworkId]
                            FOREIGN KEY ([CompanyNetworkId]) REFERENCES [dbo].[Networks] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_CompanySocialLinks_CompanyNetworkId_SortOrder]
                        ON [dbo].[CompanySocialLinks] ([CompanyNetworkId], [SortOrder]);
                END

                IF OBJECT_ID(N'dbo.CompanyComplaintContacts', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CompanyComplaintContacts] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [CompanyNetworkId] int NOT NULL,
                        [Label] nvarchar(80) NOT NULL,
                        [PhoneNumber] nvarchar(40) NOT NULL,
                        [IsVisibleToClients] bit NOT NULL CONSTRAINT [DF_CompanyComplaintContacts_IsVisibleToClients] DEFAULT (1),
                        [SortOrder] int NOT NULL CONSTRAINT [DF_CompanyComplaintContacts_SortOrder] DEFAULT (0),
                        [UpdatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_CompanyComplaintContacts_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT [PK_CompanyComplaintContacts] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CompanyComplaintContacts_Networks_CompanyNetworkId]
                            FOREIGN KEY ([CompanyNetworkId]) REFERENCES [dbo].[Networks] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_CompanyComplaintContacts_CompanyNetworkId_SortOrder]
                        ON [dbo].[CompanyComplaintContacts] ([CompanyNetworkId], [SortOrder]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.CompanyComplaintContacts', N'U') IS NOT NULL
                    DROP TABLE [dbo].[CompanyComplaintContacts];
                IF OBJECT_ID(N'dbo.CompanySocialLinks', N'U') IS NOT NULL
                    DROP TABLE [dbo].[CompanySocialLinks];
                """);
        }
    }
}
