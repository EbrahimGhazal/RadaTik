using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadTik.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
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
                name: "CashBoxWithdrawals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    WithdrawnByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
                    ManagerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
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
                name: "CollectionPointAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NetworkId = table.Column<int>(type: "int", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
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
                    LastSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
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
                name: "CashBoxDeposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DepositedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    NetworkTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    CollectionPointTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    ServiceStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledInstallationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextBillingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    AccountExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRenewalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Building = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Floor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NetworkId = table.Column<int>(type: "int", nullable: true)
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
                name: "NetworkWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SignedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetworkServiceRequestId = table.Column<int>(type: "int", nullable: true),
                    NetworkTopUpRequestId = table.Column<int>(type: "int", nullable: true),
                    NetworkServiceSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    RelatedPaymentTransactionId = table.Column<int>(type: "int", nullable: true),
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
                name: "IX_CashBoxWithdrawals_WithdrawnAt",
                table: "CashBoxWithdrawals",
                column: "WithdrawnAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxWithdrawals_WithdrawnByUserId",
                table: "CashBoxWithdrawals",
                column: "WithdrawnByUserId");

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
                name: "IX_CustomServiceItems_CreatedAt",
                table: "CustomServiceItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomServiceItems_NetworkId_ServiceKey",
                table: "CustomServiceItems",
                columns: new[] { "NetworkId", "ServiceKey" });

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
                name: "IX_MikroTikServers_Host_Port",
                table: "MikroTikServers",
                columns: new[] { "Host", "Port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MikroTikServers_NetworkId",
                table: "MikroTikServers",
                column: "NetworkId");

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
                name: "CashBoxDeposits");

            migrationBuilder.DropTable(
                name: "CashBoxWithdrawals");

            migrationBuilder.DropTable(
                name: "ClientTopUpTransactions");

            migrationBuilder.DropTable(
                name: "ClientWalletTopUpRequests");

            migrationBuilder.DropTable(
                name: "CollectionPointRenewalRequests");

            migrationBuilder.DropTable(
                name: "CustomServiceItems");

            migrationBuilder.DropTable(
                name: "FeaturePublicInfos");

            migrationBuilder.DropTable(
                name: "ItemPricings");

            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests");

            migrationBuilder.DropTable(
                name: "NetworkFeatures");

            migrationBuilder.DropTable(
                name: "NetworkReportTemplates");

            migrationBuilder.DropTable(
                name: "NetworkServiceRequests");

            migrationBuilder.DropTable(
                name: "NetworkWalletTransactions");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropTable(
                name: "ProfilePriceHistories");

            migrationBuilder.DropTable(
                name: "SectorRadioEvents");

            migrationBuilder.DropTable(
                name: "ServiceUnitChargeLedgers");

            migrationBuilder.DropTable(
                name: "SpeedChangeRequests");

            migrationBuilder.DropTable(
                name: "SystemServiceCatalog");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CollectionPointTopUpRequests");

            migrationBuilder.DropTable(
                name: "NetworkTopUpRequests");

            migrationBuilder.DropTable(
                name: "CashBoxes");

            migrationBuilder.DropTable(
                name: "FeaturePricings");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "SectorRadioMetricSamples");

            migrationBuilder.DropTable(
                name: "NetworkServiceSubscriptions");

            migrationBuilder.DropTable(
                name: "Permissions");

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
                name: "Sectors");

            migrationBuilder.DropTable(
                name: "MikroTikServers");

            migrationBuilder.DropTable(
                name: "Networks");
        }
    }
}
