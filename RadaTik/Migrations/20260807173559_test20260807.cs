using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadaTik.Migrations
{
    /// <inheritdoc />
    public partial class test20260807 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Roles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Controller = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    BalanceUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeaturePricings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    ChargeUnit = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AmountSYP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturePricings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeaturePublicInfos",
                columns: table => new
                {
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DetailHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PricingPolicyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenewalPolicyHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturePublicInfos", x => x.FeatureKey);
                });

            migrationBuilder.CreateTable(
                name: "ItemPricings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    AmountSYP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPricings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsCash = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAdminWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    BalanceSyp = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAdminWallets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemServiceCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IconClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemServiceCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NetworkServiceSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShamCashQrCodePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    OnboardingCompanyDismissedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OnboardingSystemDismissedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmployeeDepartment = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Networks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentNetworkId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Governorates = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    DefaultUsdToSypExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BalanceUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    DefaultMaterialInvoiceCurrency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ManagerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Networks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Networks_AspNetUsers_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Networks_Networks_ParentNetworkId",
                        column: x => x.ParentNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResetMethod = table.Column<int>(type: "int", nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    CodeExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_ChartOfAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPointAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPointAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPointAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionPointAccounts_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CompanyEmployeeTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyEmployeeTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeTasks_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeTasks_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompanyEmployeeTasks_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyProfileCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VATPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 15m),
                    DownloadSpeed = table.Column<int>(type: "int", nullable: false),
                    DownloadSpeedUnit = table.Column<int>(type: "int", nullable: false),
                    UploadSpeed = table.Column<int>(type: "int", nullable: true),
                    UploadSpeedUnit = table.Column<int>(type: "int", nullable: true),
                    DataLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TimeLimit = table.Column<int>(type: "int", nullable: true),
                    IPTVDevices = table.Column<int>(type: "int", nullable: true),
                    IsDataCapped = table.Column<bool>(type: "bit", nullable: false),
                    IsTimeCapped = table.Column<bool>(type: "bit", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MinDevices = table.Column<int>(type: "int", nullable: false),
                    MaxDevices = table.Column<int>(type: "int", nullable: false),
                    AllowedPorts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsForNewClients = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    MikroTikLocalAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikRemoteAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikRateLimit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikOnlyOne = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MikroTikService = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: "pppoe"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfileCatalogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyProfileCatalogs_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomServiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    ServiceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomServiceItems_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ErpSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpSuppliers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ErpSuppliers_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PostedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_AspNetUsers_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MikroTikServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Host = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false, defaultValue: 8728),
                    User = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pass = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    NetworkId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MikroTikServers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MikroTikServers_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MoneyDiaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    CategoryKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaterialPurchaseInvoiceId = table.Column<int>(type: "int", nullable: true),
                    MaterialSalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    PayrollPaymentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyDiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoneyDiaryEntries_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MoneyDiaryEntries_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkClientRenewalReminderSettings",
                columns: table => new
                {
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RemindDaysBefore5 = table.Column<bool>(type: "bit", nullable: false),
                    RemindDaysBefore4 = table.Column<bool>(type: "bit", nullable: false),
                    RemindDaysBefore3 = table.Column<bool>(type: "bit", nullable: false),
                    MessageTemplate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SendWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    WhatsAppDisplayNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    WhatsAppVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WhatsAppApiUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    WhatsAppApiAuthorizationHeader = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatsAppApiBodyTemplate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SendTelegram = table.Column<bool>(type: "bit", nullable: false),
                    TelegramBotToken = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TelegramVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TelegramTestChatId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WhatsAppTestPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkClientRenewalReminderSettings", x => x.NetworkId);
                    table.ForeignKey(
                        name: "FK_NetworkClientRenewalReminderSettings_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkFeatures_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkMaintenancePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    MaintenanceType = table.Column<int>(type: "int", nullable: false),
                    AmountSYP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkMaintenancePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkMaintenancePrices_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkMaintenancePrices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkReportTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    ReportKind = table.Column<int>(type: "int", nullable: false),
                    BodyContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkReportTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkReportTemplates_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkReportTemplates_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkServiceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FeaturePricingId = table.Column<int>(type: "int", nullable: true),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    AmountSYP = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChargeWalletTransactionId = table.Column<int>(type: "int", nullable: true),
                    RefundWalletTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkServiceRequests_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NetworkServiceRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NetworkServiceRequests_FeaturePricings_FeaturePricingId",
                        column: x => x.FeaturePricingId,
                        principalTable: "FeaturePricings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkServiceRequests_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkServiceSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingPeriod = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastApprovedRequestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkServiceSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkServiceSubscriptions_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTopUpRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeductFromCompanyCashBoxOnApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedWalletTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTopUpRequests_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NetworkTopUpRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NetworkTopUpRequests_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTopUpRequests_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEmployees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmploymentType = table.Column<int>(type: "int", nullable: false),
                    WeeklyWorkHours = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WalletBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEmployees_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollEmployees_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollMonthAccrualRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeesProcessed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollMonthAccrualRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollMonthAccrualRuns_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ModelNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PurchaseCurrency = table.Column<int>(type: "int", nullable: true),
                    WholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseItems_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPointTopUpRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointAccountId = table.Column<int>(type: "int", nullable: false),
                    RequestTargetType = table.Column<int>(type: "int", nullable: false),
                    TargetNetworkId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceiptImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPointTopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPointTopUpRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CollectionPointTopUpRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPointTopUpRequests_CollectionPointAccounts_CollectionPointAccountId",
                        column: x => x.CollectionPointAccountId,
                        principalTable: "CollectionPointAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPointTopUpRequests_Networks_TargetNetworkId",
                        column: x => x.TargetNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CollectionPointTopUpRequests_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MaterialPurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ErpSupplierId = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WalletTransactionId = table.Column<int>(type: "int", nullable: true),
                    MoneyDiaryEntryId = table.Column<int>(type: "int", nullable: true),
                    CashBoxWithdrawalId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialPurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialPurchaseInvoices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialPurchaseInvoices_ErpSuppliers_ErpSupplierId",
                        column: x => x.ErpSupplierId,
                        principalTable: "ErpSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialPurchaseInvoices_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    ChartOfAccountId = table.Column<int>(type: "int", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MikroTikServerTrafficSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    InterfaceCount = table.Column<int>(type: "int", nullable: false),
                    RxBps = table.Column<double>(type: "float", nullable: false),
                    TxBps = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MikroTikServerTrafficSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MikroTikServerTrafficSamples_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MikroTikServerTrafficSamples_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VATPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 15m),
                    DownloadSpeed = table.Column<int>(type: "int", nullable: false),
                    DownloadSpeedUnit = table.Column<int>(type: "int", nullable: false),
                    UploadSpeed = table.Column<int>(type: "int", nullable: true),
                    UploadSpeedUnit = table.Column<int>(type: "int", nullable: true),
                    DataLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TimeLimit = table.Column<int>(type: "int", nullable: true),
                    IPTVDevices = table.Column<int>(type: "int", nullable: true),
                    IsDataCapped = table.Column<bool>(type: "bit", nullable: false),
                    IsTimeCapped = table.Column<bool>(type: "bit", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MinDevices = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    MaxDevices = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    AllowedPorts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedAddresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Features = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsForNewClients = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsSyncedWithMikroTik = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MikroTikProfileId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    MikroTikLocalAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikRemoteAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikRateLimit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MikroTikOnlyOne = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MikroTikService = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: "pppoe"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    LastSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompanyProfileCatalogId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Profiles_CompanyProfileCatalogs_CompanyProfileCatalogId",
                        column: x => x.CompanyProfileCatalogId,
                        principalTable: "CompanyProfileCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Profiles_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Profiles_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ElevationMeters = table.Column<double>(type: "float", nullable: true),
                    AntennaHeightAglMeters = table.Column<double>(type: "float", nullable: true),
                    Direction = table.Column<double>(type: "float", nullable: false),
                    CoverageAngle = table.Column<double>(type: "float", nullable: false),
                    CoverageRange = table.Column<double>(type: "float", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NetworkMask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RadioInterfaceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NoiseAlertThresholdDbm = table.Column<int>(type: "int", nullable: true, defaultValue: -90),
                    SnrAlertMinDb = table.Column<int>(type: "int", nullable: true, defaultValue: 20),
                    CcqAlertMinPercent = table.Column<int>(type: "int", nullable: true, defaultValue: 70),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sectors_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sectors_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceUnitChargeLedgers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkServiceSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    ChargeUnit = table.Column<int>(type: "int", nullable: false),
                    UnitEntityKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FirstChargedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastChargedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceUnitChargeLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceUnitChargeLedgers_NetworkServiceSubscriptions_NetworkServiceSubscriptionId",
                        column: x => x.NetworkServiceSubscriptionId,
                        principalTable: "NetworkServiceSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashBoxWithdrawals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    WithdrawnByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialPurchaseInvoiceId = table.Column<int>(type: "int", nullable: true),
                    NetworkTopUpRequestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxWithdrawals_AspNetUsers_WithdrawnByUserId",
                        column: x => x.WithdrawnByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxWithdrawals_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalTable: "CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashBoxWithdrawals_NetworkTopUpRequests_NetworkTopUpRequestId",
                        column: x => x.NetworkTopUpRequestId,
                        principalTable: "NetworkTopUpRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWalletTopUpRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformCommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestSource = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWalletTopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTopUpRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTopUpRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTopUpRequests_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTopUpRequests_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Bonus = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollPayments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollPayments_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPayments_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSalaryRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    PreviousSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    AdjustmentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSalaryRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryRevisions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryRevisions_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollSalaryRevisions_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollTransactions_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriberInstallationMaterialPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    MaterialKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberInstallationMaterialPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationMaterialPrices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationMaterialPrices_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WarehouseStocktakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    StocktakeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseStocktakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseStocktakes_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarehouseStocktakes_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseStocktakes_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CashBoxDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DepositedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DepositedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    NetworkTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    CollectionPointTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaterialSalesInvoiceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxDeposits_AspNetUsers_DepositedByUserId",
                        column: x => x.DepositedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxDeposits_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalTable: "CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashBoxDeposits_CollectionPointTopUpRequests_CollectionPointTopUpRequestId",
                        column: x => x.CollectionPointTopUpRequestId,
                        principalTable: "CollectionPointTopUpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashBoxDeposits_NetworkTopUpRequests_NetworkTopUpRequestId",
                        column: x => x.NetworkTopUpRequestId,
                        principalTable: "NetworkTopUpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashBoxDeposits_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MaterialPurchaseInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialPurchaseInvoiceId = table.Column<int>(type: "int", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModelNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PackageUnit = table.Column<int>(type: "int", nullable: false),
                    UnitsPerPackage = table.Column<int>(type: "int", nullable: false),
                    PackageQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialPurchaseInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialPurchaseInvoiceLines_MaterialPurchaseInvoices_MaterialPurchaseInvoiceId",
                        column: x => x.MaterialPurchaseInvoiceId,
                        principalTable: "MaterialPurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialPurchaseInvoiceLines_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequestedProfileId = table.Column<int>(type: "int", nullable: true),
                    Qualification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Experience = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DesiredPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedPassword = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JoinRequests_Profiles_RequestedProfileId",
                        column: x => x.RequestedProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProfilePriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    OldPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldVATPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NewVATPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangeDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ChangedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilePriceHistories_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Receivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ElevationMeters = table.Column<double>(type: "float", nullable: true),
                    AntennaHeightAglMeters = table.Column<double>(type: "float", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NetworkMask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receivers_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Receivers_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectorRadioMetricSamples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    FrequencyMhz = table.Column<int>(type: "int", nullable: true),
                    ChannelWidthMhz = table.Column<int>(type: "int", nullable: true),
                    NoiseFloorDbm = table.Column<int>(type: "int", nullable: true),
                    SignalDbm = table.Column<int>(type: "int", nullable: true),
                    SnrDb = table.Column<int>(type: "int", nullable: true),
                    CcqPercent = table.Column<int>(type: "int", nullable: true),
                    TxRateMbps = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    RxRateMbps = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "MikroTik"),
                    StatusMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorRadioMetricSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectorRadioMetricSamples_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SectorRadioMetricSamples_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformCommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    EmployeeWalletTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTransactions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTransactions_EmployeeWalletTopUpRequests_EmployeeWalletTopUpRequestId",
                        column: x => x.EmployeeWalletTopUpRequestId,
                        principalTable: "EmployeeWalletTopUpRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeWalletTransactions_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeRewardPenalties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PayrollTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRewardPenalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeRewardPenalties_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeRewardPenalties_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeRewardPenalties_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeRewardPenalties_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeRewardPenalties_PayrollTransactions_PayrollTransactionId",
                        column: x => x.PayrollTransactionId,
                        principalTable: "PayrollTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PayrollWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    PayrollEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayrollTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollWithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollWithdrawalRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollWithdrawalRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollWithdrawalRequests_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollWithdrawalRequests_PayrollEmployees_PayrollEmployeeId",
                        column: x => x.PayrollEmployeeId,
                        principalTable: "PayrollEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayrollWithdrawalRequests_PayrollTransactions_PayrollTransactionId",
                        column: x => x.PayrollTransactionId,
                        principalTable: "PayrollTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriberInstallationMaterialWarehouseLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialPriceId = table.Column<int>(type: "int", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberInstallationMaterialWarehouseLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationMaterialWarehouseLinks_SubscriberInstallationMaterialPrices_MaterialPriceId",
                        column: x => x.MaterialPriceId,
                        principalTable: "SubscriberInstallationMaterialPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationMaterialWarehouseLinks_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseStocktakeLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseStocktakeId = table.Column<int>(type: "int", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseStocktakeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseStocktakeLines_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseStocktakeLines_WarehouseStocktakes_WarehouseStocktakeId",
                        column: x => x.WarehouseStocktakeId,
                        principalTable: "WarehouseStocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashBoxCurrencyExchanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    FromCurrency = table.Column<int>(type: "int", nullable: false),
                    ToCurrency = table.Column<int>(type: "int", nullable: false),
                    SourceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashBoxWithdrawalId = table.Column<int>(type: "int", nullable: false),
                    CashBoxDepositId = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxCurrencyExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxCurrencyExchanges_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxCurrencyExchanges_CashBoxDeposits_CashBoxDepositId",
                        column: x => x.CashBoxDepositId,
                        principalTable: "CashBoxDeposits",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxCurrencyExchanges_CashBoxWithdrawals_CashBoxWithdrawalId",
                        column: x => x.CashBoxWithdrawalId,
                        principalTable: "CashBoxWithdrawals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxCurrencyExchanges_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalTable: "CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    ProfileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TelegramChatId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResidenceAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReceiverId = table.Column<int>(type: "int", nullable: true),
                    Service = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Uptime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConnectionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MikroTikServerId = table.Column<int>(type: "int", nullable: true),
                    IsCrossServerDuplicate = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ServiceStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledInstallationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    AccountCurrency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AccountExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRenewalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Building = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Floor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_MikroTikServers_MikroTikServerId",
                        column: x => x.MikroTikServerId,
                        principalTable: "MikroTikServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Clients_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Clients_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clients_Receivers_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Receivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SectorRadioEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    MetricSampleId = table.Column<long>(type: "bigint", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MetricValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ThresholdValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorRadioEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectorRadioEvents_SectorRadioMetricSamples_MetricSampleId",
                        column: x => x.MetricSampleId,
                        principalTable: "SectorRadioMetricSamples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SectorRadioEvents_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientRenewalReminderSendLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "date", nullable: false),
                    DaysBefore = table.Column<byte>(type: "tinyint", nullable: false),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRenewalReminderSendLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientRenewalReminderSendLogs_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientTopUpTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    CollectionPointAccountId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTopUpTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTopUpTransactions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTopUpTransactions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTopUpTransactions_CollectionPointAccounts_CollectionPointAccountId",
                        column: x => x.CollectionPointAccountId,
                        principalTable: "CollectionPointAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientTopUpTransactions_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClientTrafficTestSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    ChargeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTrafficTestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTrafficTestSessions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientTrafficTestSessions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientWalletTopUpRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    RecipientTarget = table.Column<int>(type: "int", nullable: false),
                    TargetCollectionPointAccountId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientWalletTopUpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_CollectionPointAccounts_TargetCollectionPointAccountId",
                        column: x => x.TargetCollectionPointAccountId,
                        principalTable: "CollectionPointAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientWalletTopUpRequests_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPointRenewalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProcessedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPointRenewalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPointRenewalRequests_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CollectionPointRenewalRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPointRenewalRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionPointRenewalRequests_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ErpCustomers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpCustomers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpCustomers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ErpCustomers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ErpCustomers_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    AcceptedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TechnicianNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssignedToId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ProcessedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreferredContactTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScheduledVisitDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_AspNetUsers_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_AspNetUsers_ProcessedById",
                        column: x => x.ProcessedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentCurrency = table.Column<int>(type: "int", nullable: false),
                    AccountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccountCurrency = table.Column<int>(type: "int", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ReceivedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OperationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "ReceivePayment"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PreviousClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousPointBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewPointBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SpeedChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    CurrentProfileId = table.Column<int>(type: "int", nullable: false),
                    RequestedProfileId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImplementedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ImplementedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PriceDifference = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsPriceDifferencePaid = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeedChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpeedChangeRequests_AspNetUsers_ImplementedById",
                        column: x => x.ImplementedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SpeedChangeRequests_AspNetUsers_ProcessedById",
                        column: x => x.ProcessedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SpeedChangeRequests_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpeedChangeRequests_Profiles_CurrentProfileId",
                        column: x => x.CurrentProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SpeedChangeRequests_Profiles_RequestedProfileId",
                        column: x => x.RequestedProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriberInstallationInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReceiverMode = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClientSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmployeeSignature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberInstallationInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoices_AspNetUsers_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSalesInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ErpCustomerId = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WalletTransactionId = table.Column<int>(type: "int", nullable: true),
                    MoneyDiaryEntryId = table.Column<int>(type: "int", nullable: true),
                    CashBoxDepositId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSalesInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSalesInvoices_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialSalesInvoices_ErpCustomers_ErpCustomerId",
                        column: x => x.ErpCustomerId,
                        principalTable: "ErpCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialSalesInvoices_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    IssuedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FaultExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FixExplanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ServiceBasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransportFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionMode = table.Column<int>(type: "int", nullable: false),
                    CommissionValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmountToCompany = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    PaidByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NewClientBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_AspNetUsers_PaidByUserId",
                        column: x => x.PaidByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceInvoices_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NetworkWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetworkServiceRequestId = table.Column<int>(type: "int", nullable: true),
                    NetworkTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    NetworkServiceSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    MaterialPurchaseInvoiceId = table.Column<int>(type: "int", nullable: true),
                    MaterialSalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    RelatedPaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    EmployeeWalletTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkWalletTransactions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NetworkWalletTransactions_Networks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkWalletTransactions_PaymentTransactions_RelatedPaymentTransactionId",
                        column: x => x.RelatedPaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriberInstallationInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberInstallationInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MaterialKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsStockItem = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberInstallationInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoiceItems_SubscriberInstallationInvoices_SubscriberInstallationInvoiceId",
                        column: x => x.SubscriberInstallationInvoiceId,
                        principalTable: "SubscriberInstallationInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoiceItems_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SubscriberInstallationInvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberInstallationInvoiceId = table.Column<int>(type: "int", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "int", nullable: true),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceivedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberInstallationInvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoicePayments_AspNetUsers_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoicePayments_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriberInstallationInvoicePayments_SubscriberInstallationInvoices_SubscriberInstallationInvoiceId",
                        column: x => x.SubscriberInstallationInvoiceId,
                        principalTable: "SubscriberInstallationInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSalesInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialSalesInvoiceId = table.Column<int>(type: "int", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: false),
                    PriceMode = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSalesInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSalesInvoiceLines_MaterialSalesInvoices_MaterialSalesInvoiceId",
                        column: x => x.MaterialSalesInvoiceId,
                        principalTable: "MaterialSalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialSalesInvoiceLines_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyNetworkId = table.Column<int>(type: "int", nullable: false),
                    WarehouseItemId = table.Column<int>(type: "int", nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaterialPurchaseInvoiceId = table.Column<int>(type: "int", nullable: true),
                    MaterialSalesInvoiceId = table.Column<int>(type: "int", nullable: true),
                    WarehouseStocktakeId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_MaterialPurchaseInvoices_MaterialPurchaseInvoiceId",
                        column: x => x.MaterialPurchaseInvoiceId,
                        principalTable: "MaterialPurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_MaterialSalesInvoices_MaterialSalesInvoiceId",
                        column: x => x.MaterialSalesInvoiceId,
                        principalTable: "MaterialSalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_Networks_CompanyNetworkId",
                        column: x => x.CompanyNetworkId,
                        principalTable: "Networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_WarehouseItems_WarehouseItemId",
                        column: x => x.WarehouseItemId,
                        principalTable: "WarehouseItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarehouseMovements_WarehouseStocktakes_WarehouseStocktakeId",
                        column: x => x.WarehouseStocktakeId,
                        principalTable: "WarehouseStocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SystemAdminWallets",
                columns: new[] { "Id", "BalanceSyp", "BalanceUsd", "UpdatedAt" },
                values: new object[] { 1, 0m, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_AppUserNotifications_CreatedAt",
                table: "AppUserNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserNotifications_IsRead",
                table: "AppUserNotifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserNotifications_Key",
                table: "AppUserNotifications",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUserNotifications_NetworkId",
                table: "AppUserNotifications",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserNotifications_UserId",
                table: "AppUserNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ClientId",
                table: "AspNetUsers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NetworkId",
                table: "AspNetUsers",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Controller_Action",
                table: "AuditLogs",
                columns: new[] { "Controller", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_NetworkId",
                table: "AuditLogs",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencyExchanges_CashBoxDepositId",
                table: "CashBoxCurrencyExchanges",
                column: "CashBoxDepositId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencyExchanges_CashBoxId",
                table: "CashBoxCurrencyExchanges",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencyExchanges_CashBoxWithdrawalId",
                table: "CashBoxCurrencyExchanges",
                column: "CashBoxWithdrawalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencyExchanges_CreatedAt",
                table: "CashBoxCurrencyExchanges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencyExchanges_CreatedByUserId",
                table: "CashBoxCurrencyExchanges",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_CashBoxId",
                table: "CashBoxDeposits",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_CollectionPointTopUpRequestId",
                table: "CashBoxDeposits",
                column: "CollectionPointTopUpRequestId",
                unique: true,
                filter: "[CollectionPointTopUpRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_DepositedAt",
                table: "CashBoxDeposits",
                column: "DepositedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_DepositedByUserId",
                table: "CashBoxDeposits",
                column: "DepositedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_NetworkTopUpRequestId",
                table: "CashBoxDeposits",
                column: "NetworkTopUpRequestId",
                unique: true,
                filter: "[NetworkTopUpRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxDeposits_PaymentMethodId",
                table: "CashBoxDeposits",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxes_OwnerType_OwnerId",
                table: "CashBoxes",
                columns: new[] { "OwnerType", "OwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxWithdrawals_CashBoxId",
                table: "CashBoxWithdrawals",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxWithdrawals_NetworkTopUpRequestId",
                table: "CashBoxWithdrawals",
                column: "NetworkTopUpRequestId",
                unique: true,
                filter: "[NetworkTopUpRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxWithdrawals_WithdrawnAt",
                table: "CashBoxWithdrawals",
                column: "WithdrawnAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxWithdrawals_WithdrawnByUserId",
                table: "CashBoxWithdrawals",
                column: "WithdrawnByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_CompanyNetworkId_Code",
                table: "ChartOfAccounts",
                columns: new[] { "CompanyNetworkId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_ParentAccountId",
                table: "ChartOfAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRenewalReminderSendLogs_ClientId",
                table: "ClientRenewalReminderSendLogs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_MikroTikServerId_UserName",
                table: "Clients",
                columns: new[] { "MikroTikServerId", "UserName" },
                unique: true,
                filter: "[MikroTikServerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NetworkId",
                table: "Clients",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NetworkId_UserName_IsCrossServerDuplicate",
                table: "Clients",
                columns: new[] { "NetworkId", "UserName", "IsCrossServerDuplicate" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ProfileId",
                table: "Clients",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ReceiverId",
                table: "Clients",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_ClientId",
                table: "ClientTopUpTransactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_CollectionPointAccountId",
                table: "ClientTopUpTransactions",
                column: "CollectionPointAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_CreatedAt",
                table: "ClientTopUpTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_CreatedByUserId",
                table: "ClientTopUpTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_NetworkId",
                table: "ClientTopUpTransactions",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTopUpTransactions_SourceType",
                table: "ClientTopUpTransactions",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_ClientId_StartedAtUtc",
                table: "ClientTrafficTestSessions",
                columns: new[] { "ClientId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_CreatedByUserId",
                table: "ClientTrafficTestSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTrafficTestSessions_StartedAtUtc",
                table: "ClientTrafficTestSessions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_ClientId",
                table: "ClientWalletTopUpRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_NetworkId",
                table: "ClientWalletTopUpRequests",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_PaymentMethodId",
                table: "ClientWalletTopUpRequests",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_ProcessedByUserId",
                table: "ClientWalletTopUpRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_RequestedAt",
                table: "ClientWalletTopUpRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_RequestedByUserId",
                table: "ClientWalletTopUpRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_Status",
                table: "ClientWalletTopUpRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClientWalletTopUpRequests_TargetCollectionPointAccountId",
                table: "ClientWalletTopUpRequests",
                column: "TargetCollectionPointAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointAccounts_NetworkId",
                table: "CollectionPointAccounts",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointAccounts_UserId",
                table: "CollectionPointAccounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_ClientId",
                table: "CollectionPointRenewalRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_NetworkId",
                table: "CollectionPointRenewalRequests",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_ProcessedByUserId",
                table: "CollectionPointRenewalRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_RequestedAt",
                table: "CollectionPointRenewalRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_RequestedByUserId",
                table: "CollectionPointRenewalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointRenewalRequests_Status",
                table: "CollectionPointRenewalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_CollectionPointAccountId",
                table: "CollectionPointTopUpRequests",
                column: "CollectionPointAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_PaymentMethodId",
                table: "CollectionPointTopUpRequests",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_ProcessedByUserId",
                table: "CollectionPointTopUpRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_RequestedAt",
                table: "CollectionPointTopUpRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_RequestedByUserId",
                table: "CollectionPointTopUpRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_Status",
                table: "CollectionPointTopUpRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPointTopUpRequests_TargetNetworkId",
                table: "CollectionPointTopUpRequests",
                column: "TargetNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_AssignedByUserId",
                table: "CompanyEmployeeTasks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_AssignedToUserId",
                table: "CompanyEmployeeTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_CompanyNetworkId",
                table: "CompanyEmployeeTasks",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyEmployeeTasks_CompanyNetworkId_Status",
                table: "CompanyEmployeeTasks",
                columns: new[] { "CompanyNetworkId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfileCatalogs_CompanyNetworkId_Name",
                table: "CompanyProfileCatalogs",
                columns: new[] { "CompanyNetworkId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomServiceItems_CreatedAt",
                table: "CustomServiceItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomServiceItems_NetworkId_ServiceKey",
                table: "CustomServiceItems",
                columns: new[] { "NetworkId", "ServiceKey" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_CompanyNetworkId",
                table: "EmployeeRewardPenalties",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_CompanyNetworkId_Status",
                table: "EmployeeRewardPenalties",
                columns: new[] { "CompanyNetworkId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_CreatedByUserId",
                table: "EmployeeRewardPenalties",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_PayrollEmployeeId",
                table: "EmployeeRewardPenalties",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_PayrollTransactionId",
                table: "EmployeeRewardPenalties",
                column: "PayrollTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRewardPenalties_ReviewedByUserId",
                table: "EmployeeRewardPenalties",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTopUpRequests_CompanyNetworkId_Status_RequestedAt",
                table: "EmployeeWalletTopUpRequests",
                columns: new[] { "CompanyNetworkId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTopUpRequests_PayrollEmployeeId_Status",
                table: "EmployeeWalletTopUpRequests",
                columns: new[] { "PayrollEmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTopUpRequests_ProcessedByUserId",
                table: "EmployeeWalletTopUpRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTopUpRequests_RequestedByUserId",
                table: "EmployeeWalletTopUpRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTransactions_CreatedByUserId",
                table: "EmployeeWalletTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTransactions_EmployeeWalletTopUpRequestId",
                table: "EmployeeWalletTransactions",
                column: "EmployeeWalletTopUpRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWalletTransactions_PayrollEmployeeId_CreatedAt",
                table: "EmployeeWalletTransactions",
                columns: new[] { "PayrollEmployeeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_ClientId",
                table: "ErpCustomers",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CompanyNetworkId",
                table: "ErpCustomers",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CompanyNetworkId_Name",
                table: "ErpCustomers",
                columns: new[] { "CompanyNetworkId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpCustomers_CreatedByUserId",
                table: "ErpCustomers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpSuppliers_CompanyNetworkId",
                table: "ErpSuppliers",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpSuppliers_CompanyNetworkId_Name",
                table: "ErpSuppliers",
                columns: new[] { "CompanyNetworkId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpSuppliers_CreatedByUserId",
                table: "ErpSuppliers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePricings_FeatureKey",
                table: "FeaturePricings",
                column: "FeatureKey");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturePricings_FeatureKey_BillingPeriod",
                table: "FeaturePricings",
                columns: new[] { "FeatureKey", "BillingPeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemPricings_ItemType",
                table: "ItemPricings",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ProcessedByUserId",
                table: "JoinRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_RequestedProfileId",
                table: "JoinRequests",
                column: "RequestedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CompanyNetworkId",
                table: "JournalEntries",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CreatedByUserId",
                table: "JournalEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_EntryDate",
                table: "JournalEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PostedByUserId",
                table: "JournalEntries",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_ChartOfAccountId",
                table: "JournalEntryLines",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId",
                table: "JournalEntryLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_ClientId",
                table: "MaintenanceInvoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_CreatedAt",
                table: "MaintenanceInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_IssuedByUserId",
                table: "MaintenanceInvoices",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_MaintenanceRequestId",
                table: "MaintenanceInvoices",
                column: "MaintenanceRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_NetworkId",
                table: "MaintenanceInvoices",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_PaidByUserId",
                table: "MaintenanceInvoices",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_PaymentTransactionId",
                table: "MaintenanceInvoices",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceInvoices_Status",
                table: "MaintenanceInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_AssignedToId",
                table: "MaintenanceRequests",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ClientId",
                table: "MaintenanceRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ProcessedById",
                table: "MaintenanceRequests",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoiceLines_MaterialPurchaseInvoiceId",
                table: "MaterialPurchaseInvoiceLines",
                column: "MaterialPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoiceLines_WarehouseItemId",
                table: "MaterialPurchaseInvoiceLines",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_CashBoxWithdrawalId",
                table: "MaterialPurchaseInvoices",
                column: "CashBoxWithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_CompanyNetworkId",
                table: "MaterialPurchaseInvoices",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_CreatedByUserId",
                table: "MaterialPurchaseInvoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_ErpSupplierId",
                table: "MaterialPurchaseInvoices",
                column: "ErpSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_InvoiceDate",
                table: "MaterialPurchaseInvoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialPurchaseInvoices_MoneyDiaryEntryId",
                table: "MaterialPurchaseInvoices",
                column: "MoneyDiaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoiceLines_MaterialSalesInvoiceId",
                table: "MaterialSalesInvoiceLines",
                column: "MaterialSalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoiceLines_WarehouseItemId",
                table: "MaterialSalesInvoiceLines",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_CashBoxDepositId",
                table: "MaterialSalesInvoices",
                column: "CashBoxDepositId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_CompanyNetworkId",
                table: "MaterialSalesInvoices",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_CreatedByUserId",
                table: "MaterialSalesInvoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_ErpCustomerId",
                table: "MaterialSalesInvoices",
                column: "ErpCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_InvoiceDate",
                table: "MaterialSalesInvoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSalesInvoices_MoneyDiaryEntryId",
                table: "MaterialSalesInvoices",
                column: "MoneyDiaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServers_NetworkId_Host_Port",
                table: "MikroTikServers",
                columns: new[] { "NetworkId", "Host", "Port" },
                unique: true,
                filter: "[NetworkId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServerTrafficSamples_MikroTikServerId_CapturedAtUtc",
                table: "MikroTikServerTrafficSamples",
                columns: new[] { "MikroTikServerId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServerTrafficSamples_NetworkId_CapturedAtUtc",
                table: "MikroTikServerTrafficSamples",
                columns: new[] { "NetworkId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MoneyDiaryEntries_CompanyNetworkId",
                table: "MoneyDiaryEntries",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyDiaryEntries_CreatedByUserId",
                table: "MoneyDiaryEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyDiaryEntries_EntryDate",
                table: "MoneyDiaryEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyDiaryEntries_MaterialPurchaseInvoiceId",
                table: "MoneyDiaryEntries",
                column: "MaterialPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MoneyDiaryEntries_MaterialSalesInvoiceId",
                table: "MoneyDiaryEntries",
                column: "MaterialSalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkFeatures_NetworkId",
                table: "NetworkFeatures",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkFeatures_NetworkId_Key",
                table: "NetworkFeatures",
                columns: new[] { "NetworkId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_IsActive",
                table: "NetworkMaintenancePrices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_NetworkId_MaintenanceType",
                table: "NetworkMaintenancePrices",
                columns: new[] { "NetworkId", "MaintenanceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMaintenancePrices_UpdatedByUserId",
                table: "NetworkMaintenancePrices",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkReportTemplates_CompanyNetworkId_ReportKind",
                table: "NetworkReportTemplates",
                columns: new[] { "CompanyNetworkId", "ReportKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkReportTemplates_UpdatedByUserId",
                table: "NetworkReportTemplates",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Networks_ManagerUserId",
                table: "Networks",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Networks_Name",
                table: "Networks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Networks_ParentNetworkId",
                table: "Networks",
                column: "ParentNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_DecidedByUserId",
                table: "NetworkServiceRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_FeatureKey",
                table: "NetworkServiceRequests",
                column: "FeatureKey");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_FeaturePricingId",
                table: "NetworkServiceRequests",
                column: "FeaturePricingId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_NetworkId",
                table: "NetworkServiceRequests",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_RequestedAt",
                table: "NetworkServiceRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_RequestedByUserId",
                table: "NetworkServiceRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceRequests_Status",
                table: "NetworkServiceRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceSubscriptions_ExpiresAt",
                table: "NetworkServiceSubscriptions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceSubscriptions_NetworkId",
                table: "NetworkServiceSubscriptions",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceSubscriptions_NetworkId_FeatureKey",
                table: "NetworkServiceSubscriptions",
                columns: new[] { "NetworkId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkServiceSubscriptions_Status",
                table: "NetworkServiceSubscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_DecidedByUserId",
                table: "NetworkTopUpRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_NetworkId",
                table: "NetworkTopUpRequests",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_PaymentMethodId",
                table: "NetworkTopUpRequests",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_RequestedAt",
                table: "NetworkTopUpRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_RequestedByUserId",
                table: "NetworkTopUpRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopUpRequests_Status",
                table: "NetworkTopUpRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_CreatedAt",
                table: "NetworkWalletTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_CreatedByUserId",
                table: "NetworkWalletTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_MaterialPurchaseInvoiceId",
                table: "NetworkWalletTransactions",
                column: "MaterialPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_MaterialSalesInvoiceId",
                table: "NetworkWalletTransactions",
                column: "MaterialSalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_NetworkId",
                table: "NetworkWalletTransactions",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_RelatedPaymentTransactionId",
                table: "NetworkWalletTransactions",
                column: "RelatedPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWalletTransactions_Type",
                table: "NetworkWalletTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ProcessedByUserId",
                table: "PasswordResetRequests",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId",
                table: "PasswordResetRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_DisplayOrder",
                table: "PaymentMethods",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_IsActive",
                table: "PaymentMethods",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_IsCash",
                table: "PaymentMethods",
                column: "IsCash");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Name",
                table: "PaymentMethods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ClientId",
                table: "PaymentTransactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_NetworkId",
                table: "PaymentTransactions",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentDate",
                table: "PaymentTransactions",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReceivedByUserId",
                table: "PaymentTransactions",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReferenceNumber",
                table: "PaymentTransactions",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_ApplicationUserId",
                table: "PayrollEmployees",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEmployees_CompanyNetworkId",
                table: "PayrollEmployees",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollMonthAccrualRuns_CompanyNetworkId_Year_Month",
                table: "PayrollMonthAccrualRuns",
                columns: new[] { "CompanyNetworkId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_CompanyNetworkId",
                table: "PayrollPayments",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_CompanyNetworkId_Year_Month",
                table: "PayrollPayments",
                columns: new[] { "CompanyNetworkId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_CreatedByUserId",
                table: "PayrollPayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_PayrollEmployeeId_Year_Month",
                table: "PayrollPayments",
                columns: new[] { "PayrollEmployeeId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryRevisions_CompanyNetworkId",
                table: "PayrollSalaryRevisions",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryRevisions_CreatedByUserId",
                table: "PayrollSalaryRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSalaryRevisions_PayrollEmployeeId",
                table: "PayrollSalaryRevisions",
                column: "PayrollEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_CompanyNetworkId",
                table: "PayrollTransactions",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_CreatedByUserId",
                table: "PayrollTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollTransactions_PayrollEmployeeId_Year_Month",
                table: "PayrollTransactions",
                columns: new[] { "PayrollEmployeeId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollWithdrawalRequests_CompanyNetworkId",
                table: "PayrollWithdrawalRequests",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollWithdrawalRequests_PayrollEmployeeId_Year_Month_Status",
                table: "PayrollWithdrawalRequests",
                columns: new[] { "PayrollEmployeeId", "Year", "Month", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollWithdrawalRequests_PayrollTransactionId",
                table: "PayrollWithdrawalRequests",
                column: "PayrollTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollWithdrawalRequests_RequestedByUserId",
                table: "PayrollWithdrawalRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollWithdrawalRequests_ReviewedByUserId",
                table: "PayrollWithdrawalRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Key",
                table: "Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePriceHistories_ProfileId",
                table: "ProfilePriceHistories",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CompanyProfileCatalogId_MikroTikServerId",
                table: "Profiles",
                columns: new[] { "CompanyProfileCatalogId", "MikroTikServerId" },
                unique: true,
                filter: "[CompanyProfileCatalogId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_MikroTikServerId_Name",
                table: "Profiles",
                columns: new[] { "MikroTikServerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_NetworkId",
                table: "Profiles",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Receivers_NetworkId",
                table: "Receivers",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_Receivers_SectorId",
                table: "Receivers",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_SectorRadioEvents_MetricSampleId",
                table: "SectorRadioEvents",
                column: "MetricSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_SectorRadioEvents_SectorId_CreatedAt",
                table: "SectorRadioEvents",
                columns: new[] { "SectorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SectorRadioEvents_SectorId_EventType_MetricName_CreatedAt",
                table: "SectorRadioEvents",
                columns: new[] { "SectorId", "EventType", "MetricName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SectorRadioMetricSamples_MikroTikServerId",
                table: "SectorRadioMetricSamples",
                column: "MikroTikServerId");

            migrationBuilder.CreateIndex(
                name: "IX_SectorRadioMetricSamples_SectorId_CapturedAt",
                table: "SectorRadioMetricSamples",
                columns: new[] { "SectorId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_MikroTikServerId",
                table: "Sectors",
                column: "MikroTikServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_NetworkId",
                table: "Sectors",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceUnitChargeLedgers_NetworkServiceSubscriptionId",
                table: "ServiceUnitChargeLedgers",
                column: "NetworkServiceSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceUnitChargeLedgers_NetworkServiceSubscriptionId_ChargeUnit_UnitEntityKey",
                table: "ServiceUnitChargeLedgers",
                columns: new[] { "NetworkServiceSubscriptionId", "ChargeUnit", "UnitEntityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeedChangeRequests_ClientId",
                table: "SpeedChangeRequests",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeedChangeRequests_CurrentProfileId",
                table: "SpeedChangeRequests",
                column: "CurrentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeedChangeRequests_ImplementedById",
                table: "SpeedChangeRequests",
                column: "ImplementedById");

            migrationBuilder.CreateIndex(
                name: "IX_SpeedChangeRequests_ProcessedById",
                table: "SpeedChangeRequests",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_SpeedChangeRequests_RequestedProfileId",
                table: "SpeedChangeRequests",
                column: "RequestedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoiceItems_SubscriberInstallationInvoiceId",
                table: "SubscriberInstallationInvoiceItems",
                column: "SubscriberInstallationInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoiceItems_WarehouseItemId",
                table: "SubscriberInstallationInvoiceItems",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoicePayments_CreatedAt",
                table: "SubscriberInstallationInvoicePayments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoicePayments_PaymentTransactionId",
                table: "SubscriberInstallationInvoicePayments",
                column: "PaymentTransactionId",
                unique: true,
                filter: "[PaymentTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoicePayments_ReceivedByUserId",
                table: "SubscriberInstallationInvoicePayments",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoicePayments_SubscriberInstallationInvoiceId",
                table: "SubscriberInstallationInvoicePayments",
                column: "SubscriberInstallationInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_ClientId",
                table: "SubscriberInstallationInvoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_ClientId_Kind",
                table: "SubscriberInstallationInvoices",
                columns: new[] { "ClientId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_CreatedAt",
                table: "SubscriberInstallationInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_CreatedByUserId",
                table: "SubscriberInstallationInvoices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_FinalizedByUserId",
                table: "SubscriberInstallationInvoices",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_NetworkId",
                table: "SubscriberInstallationInvoices",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationInvoices_Status",
                table: "SubscriberInstallationInvoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationMaterialPrices_IsActive",
                table: "SubscriberInstallationMaterialPrices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationMaterialPrices_NetworkId_MaterialKey",
                table: "SubscriberInstallationMaterialPrices",
                columns: new[] { "NetworkId", "MaterialKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationMaterialPrices_WarehouseItemId",
                table: "SubscriberInstallationMaterialPrices",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationMaterialWarehouseLinks_MaterialPriceId_WarehouseItemId",
                table: "SubscriberInstallationMaterialWarehouseLinks",
                columns: new[] { "MaterialPriceId", "WarehouseItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberInstallationMaterialWarehouseLinks_WarehouseItemId",
                table: "SubscriberInstallationMaterialWarehouseLinks",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemServiceCatalog_IsActive",
                table: "SystemServiceCatalog",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SystemServiceCatalog_Key",
                table: "SystemServiceCatalog",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions",
                columns: new[] { "UserId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_CompanyNetworkId",
                table: "WarehouseItems",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_CompanyNetworkId_Name",
                table: "WarehouseItems",
                columns: new[] { "CompanyNetworkId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_CompanyNetworkId_Name_ModelNumber",
                table: "WarehouseItems",
                columns: new[] { "CompanyNetworkId", "Name", "ModelNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_CompanyNetworkId",
                table: "WarehouseMovements",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_CreatedByUserId",
                table: "WarehouseMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_MaterialPurchaseInvoiceId",
                table: "WarehouseMovements",
                column: "MaterialPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_MaterialSalesInvoiceId",
                table: "WarehouseMovements",
                column: "MaterialSalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_MovementDate",
                table: "WarehouseMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_WarehouseItemId",
                table: "WarehouseMovements",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseMovements_WarehouseStocktakeId",
                table: "WarehouseMovements",
                column: "WarehouseStocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakeLines_WarehouseItemId",
                table: "WarehouseStocktakeLines",
                column: "WarehouseItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakeLines_WarehouseStocktakeId",
                table: "WarehouseStocktakeLines",
                column: "WarehouseStocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakes_CompanyNetworkId",
                table: "WarehouseStocktakes",
                column: "CompanyNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakes_CreatedByUserId",
                table: "WarehouseStocktakes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakes_StocktakeDate",
                table: "WarehouseStocktakes",
                column: "StocktakeDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseStocktakes_WarehouseItemId",
                table: "WarehouseStocktakes",
                column: "WarehouseItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserNotifications_AspNetUsers_UserId",
                table: "AppUserNotifications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUserNotifications_Networks_NetworkId",
                table: "AppUserNotifications",
                column: "NetworkId",
                principalTable: "Networks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Clients_ClientId",
                table: "AspNetUsers",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Networks_NetworkId",
                table: "AspNetUsers",
                column: "NetworkId",
                principalTable: "Networks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Networks_AspNetUsers_ManagerUserId",
                table: "Networks");

            migrationBuilder.DropTable(
                name: "AppUserNotifications");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CashBoxCurrencyExchanges");

            migrationBuilder.DropTable(
                name: "ClientRenewalReminderSendLogs");

            migrationBuilder.DropTable(
                name: "ClientTopUpTransactions");

            migrationBuilder.DropTable(
                name: "ClientTrafficTestSessions");

            migrationBuilder.DropTable(
                name: "ClientWalletTopUpRequests");

            migrationBuilder.DropTable(
                name: "CollectionPointRenewalRequests");

            migrationBuilder.DropTable(
                name: "CompanyEmployeeTasks");

            migrationBuilder.DropTable(
                name: "CustomServiceItems");

            migrationBuilder.DropTable(
                name: "EmployeeRewardPenalties");

            migrationBuilder.DropTable(
                name: "EmployeeWalletTransactions");

            migrationBuilder.DropTable(
                name: "FeaturePublicInfos");

            migrationBuilder.DropTable(
                name: "ItemPricings");

            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropTable(
                name: "JournalEntryLines");

            migrationBuilder.DropTable(
                name: "MaintenanceInvoices");

            migrationBuilder.DropTable(
                name: "MaterialPurchaseInvoiceLines");

            migrationBuilder.DropTable(
                name: "MaterialSalesInvoiceLines");

            migrationBuilder.DropTable(
                name: "MikroTikServerTrafficSamples");

            migrationBuilder.DropTable(
                name: "MoneyDiaryEntries");

            migrationBuilder.DropTable(
                name: "NetworkClientRenewalReminderSettings");

            migrationBuilder.DropTable(
                name: "NetworkFeatures");

            migrationBuilder.DropTable(
                name: "NetworkMaintenancePrices");

            migrationBuilder.DropTable(
                name: "NetworkReportTemplates");

            migrationBuilder.DropTable(
                name: "NetworkServiceRequests");

            migrationBuilder.DropTable(
                name: "NetworkWalletTransactions");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropTable(
                name: "PayrollMonthAccrualRuns");

            migrationBuilder.DropTable(
                name: "PayrollPayments");

            migrationBuilder.DropTable(
                name: "PayrollSalaryRevisions");

            migrationBuilder.DropTable(
                name: "PayrollWithdrawalRequests");

            migrationBuilder.DropTable(
                name: "ProfilePriceHistories");

            migrationBuilder.DropTable(
                name: "SectorRadioEvents");

            migrationBuilder.DropTable(
                name: "ServiceUnitChargeLedgers");

            migrationBuilder.DropTable(
                name: "SpeedChangeRequests");

            migrationBuilder.DropTable(
                name: "SubscriberInstallationInvoiceItems");

            migrationBuilder.DropTable(
                name: "SubscriberInstallationInvoicePayments");

            migrationBuilder.DropTable(
                name: "SubscriberInstallationMaterialWarehouseLinks");

            migrationBuilder.DropTable(
                name: "SystemAdminWallets");

            migrationBuilder.DropTable(
                name: "SystemServiceCatalog");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "WarehouseMovements");

            migrationBuilder.DropTable(
                name: "WarehouseStocktakeLines");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CashBoxDeposits");

            migrationBuilder.DropTable(
                name: "CashBoxWithdrawals");

            migrationBuilder.DropTable(
                name: "EmployeeWalletTopUpRequests");

            migrationBuilder.DropTable(
                name: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests");

            migrationBuilder.DropTable(
                name: "FeaturePricings");

            migrationBuilder.DropTable(
                name: "PayrollTransactions");

            migrationBuilder.DropTable(
                name: "SectorRadioMetricSamples");

            migrationBuilder.DropTable(
                name: "NetworkServiceSubscriptions");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "SubscriberInstallationInvoices");

            migrationBuilder.DropTable(
                name: "SubscriberInstallationMaterialPrices");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "MaterialPurchaseInvoices");

            migrationBuilder.DropTable(
                name: "MaterialSalesInvoices");

            migrationBuilder.DropTable(
                name: "WarehouseStocktakes");

            migrationBuilder.DropTable(
                name: "CollectionPointTopUpRequests");

            migrationBuilder.DropTable(
                name: "CashBoxes");

            migrationBuilder.DropTable(
                name: "NetworkTopUpRequests");

            migrationBuilder.DropTable(
                name: "PayrollEmployees");

            migrationBuilder.DropTable(
                name: "ErpSuppliers");

            migrationBuilder.DropTable(
                name: "ErpCustomers");

            migrationBuilder.DropTable(
                name: "WarehouseItems");

            migrationBuilder.DropTable(
                name: "CollectionPointAccounts");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "Receivers");

            migrationBuilder.DropTable(
                name: "CompanyProfileCatalogs");

            migrationBuilder.DropTable(
                name: "Sectors");

            migrationBuilder.DropTable(
                name: "MikroTikServers");

            migrationBuilder.DropTable(
                name: "Networks");
        }
    }
}
