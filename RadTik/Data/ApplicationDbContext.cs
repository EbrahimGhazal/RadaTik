using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RadTik.Data.Configurations;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Network> Networks { get; set; }
        public DbSet<Sector> Sectors { get; set; }
        public DbSet<Receiver> Receivers { get; set; }
        public DbSet<SectorRadioMetricSample> SectorRadioMetricSamples { get; set; }
        public DbSet<SectorRadioEvent> SectorRadioEvents { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<MikroTikServer> MikroTikServers { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<ProfilePriceHistory> ProfilePriceHistories { get; set; }
        public DbSet<JoinRequest> JoinRequests { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<SpeedChangeRequest> SpeedChangeRequests { get; set; }
        public DbSet<CollectionPointAccount> CollectionPointAccounts { get; set; }
        public DbSet<CollectionPointRenewalRequest> CollectionPointRenewalRequests { get; set; }
        public DbSet<CollectionPointTopUpRequest> CollectionPointTopUpRequests { get; set; }
        public DbSet<ClientWalletTopUpRequest> ClientWalletTopUpRequests { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<ClientTopUpTransaction> ClientTopUpTransactions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<NetworkFeature> NetworkFeatures { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemPricingItem> SystemPricingItems { get; set; }
        public DbSet<FeaturePricing> FeaturePricings { get; set; }
        public DbSet<NetworkServiceRequest> NetworkServiceRequests { get; set; }
        public DbSet<NetworkServiceSubscription> NetworkServiceSubscriptions { get; set; }
        public DbSet<NetworkWalletTransaction> NetworkWalletTransactions { get; set; }
        public DbSet<NetworkTopUpRequest> NetworkTopUpRequests { get; set; }
        public DbSet<ServiceUnitChargeLedger> ServiceUnitChargeLedgers { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<SystemService> SystemServices { get; set; }
        public DbSet<FeaturePublicInfo> FeaturePublicInfos { get; set; }
        public DbSet<CustomServiceItem> CustomServiceItems { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<CashBox> CashBoxes { get; set; }
        public DbSet<CashBoxWithdrawal> CashBoxWithdrawals { get; set; }
        public DbSet<CashBoxDeposit> CashBoxDeposits { get; set; }
        public DbSet<NetworkReportTemplate> NetworkReportTemplates { get; set; }
        public DbSet<MaintenanceInvoice> MaintenanceInvoices { get; set; }
        public DbSet<NetworkMaintenancePrice> NetworkMaintenancePrices { get; set; }
        public DbSet<MikroTikServerTrafficSample> MikroTikServerTrafficSamples { get; set; }
        public DbSet<ClientTrafficTestSession> ClientTrafficTestSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var sensitiveNullableStringConverter = new ValueConverter<string?, string?>(
                value => SensitiveDataProtector.Protect(value),
                value => SensitiveDataProtector.Unprotect(value));

            var sensitiveStringConverter = new ValueConverter<string, string>(
                value => SensitiveDataProtector.Protect(value) ?? string.Empty,
                value => SensitiveDataProtector.Unprotect(value) ?? string.Empty);

            // Network Configuration
            modelBuilder.Entity<Network>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Governorates).HasMaxLength(500);
                entity.Property(e => e.LogoPath).HasMaxLength(500);
                entity.Property(e => e.CreationDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasDefaultValue(NetworkStatus.Active);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.ManagerUserId).HasMaxLength(450);

                // إضافة فهرس للاسم
                entity.HasIndex(e => e.Name).IsUnique();

                // علاقة Self-Reference لدعم الشبكات الفرعية
                entity.HasOne(n => n.ParentNetwork)
                      .WithMany(n => n.ChildNetworks)
                      .HasForeignKey(n => n.ParentNetworkId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired(false);

                entity.HasIndex(e => e.ParentNetworkId);

                entity.HasOne(n => n.ManagerUser)
                      .WithMany()
                      .HasForeignKey(n => n.ManagerUserId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // Profile Configuration - تم التحديث
            modelBuilder.Entity<Profile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.VATPercentage).HasPrecision(5, 2).HasDefaultValue(15);
                entity.Property(e => e.DownloadSpeed);
                entity.Property(e => e.DownloadSpeedUnit).HasConversion<int>();
                entity.Property(e => e.UploadSpeed);
                entity.Property(e => e.UploadSpeedUnit).HasConversion<int?>();
                entity.Property(e => e.DataLimit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsForNewClients).HasDefaultValue(true);
                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.IsSyncedWithMikroTik).HasDefaultValue(false);
                entity.Property(e => e.MikroTikOnlyOne).HasDefaultValue(true);
                entity.Property(e => e.MikroTikService).HasDefaultValue("pppoe");
                entity.Property(e => e.MinDevices).HasDefaultValue(1);
                entity.Property(e => e.MaxDevices).HasDefaultValue(1);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedDate).HasDefaultValueSql("GETDATE()");

                // اسم البروفايل يجب أن يكون فريداً داخل نفس خادم MikroTik
                entity.HasIndex(e => new { e.MikroTikServerId, e.Name }).IsUnique();

                // إضافة العلاقة مع MikroTikServer
                entity.HasOne(p => p.MikroTikServer)
                      .WithMany()
                      .HasForeignKey(p => p.MikroTikServerId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
            });

            // ProfilePriceHistory Configuration
            modelBuilder.Entity<ProfilePriceHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OldPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OldVATPercentage).HasPrecision(5, 2);
                entity.Property(e => e.NewVATPercentage).HasPrecision(5, 2);
                entity.Property(e => e.ChangeDate).HasDefaultValueSql("GETDATE()");

                entity.HasOne(pph => pph.Profile)
                    .WithMany(p => p.ProfilePriceHistories)
                    .HasForeignKey(pph => pph.ProfileId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Sector Configuration
            modelBuilder.Entity<Sector>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IPAddress).IsRequired();
                entity.Property(e => e.NetworkMask).IsRequired();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.RadioInterfaceName).HasMaxLength(100);
                entity.Property(e => e.NoiseAlertThresholdDbm).HasDefaultValue(-90);
                entity.Property(e => e.SnrAlertMinDb).HasDefaultValue(20);
                entity.Property(e => e.CcqAlertMinPercent).HasDefaultValue(70);

                entity.HasOne(s => s.MikroTikServer)
                      .WithMany(m => m.Sectors)
                      .HasForeignKey(s => s.MikroTikServerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Ignore(e => e.ReceiverCount);
                entity.Ignore(e => e.UserCount);
                entity.Ignore(e => e.ProfileNames);
            });

            // Receiver Configuration
            modelBuilder.Entity<Receiver>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IPAddress).IsRequired();
                entity.Property(e => e.NetworkMask).IsRequired();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasOne(r => r.Sector)
                      .WithMany(s => s.Receivers)
                      .HasForeignKey(r => r.SectorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Ignore(e => e.UserCount);
                entity.Ignore(e => e.ProfileNames);
                entity.Ignore(e => e.MikroTikServerName);
            });

            // Sector radio metrics (PoC)
            modelBuilder.Entity<SectorRadioMetricSample>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CapturedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Source).HasMaxLength(40).HasDefaultValue("MikroTik");
                entity.Property(e => e.StatusMessage).HasMaxLength(500);
                entity.Property(e => e.TxRateMbps).HasColumnType("decimal(10,2)");
                entity.Property(e => e.RxRateMbps).HasColumnType("decimal(10,2)");

                entity.HasIndex(e => new { e.SectorId, e.CapturedAt });

                entity.HasOne(e => e.Sector)
                    .WithMany()
                    .HasForeignKey(e => e.SectorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.MikroTikServer)
                    .WithMany()
                    .HasForeignKey(e => e.MikroTikServerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SectorRadioEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Severity).HasMaxLength(16);
                entity.Property(e => e.EventType).HasMaxLength(32);
                entity.Property(e => e.MetricName).HasMaxLength(64);
                entity.Property(e => e.Message).HasMaxLength(400);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.MetricValue).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ThresholdValue).HasColumnType("decimal(10,2)");

                entity.HasIndex(e => new { e.SectorId, e.CreatedAt });
                entity.HasIndex(e => new { e.SectorId, e.EventType, e.MetricName, e.CreatedAt });

                entity.HasOne(e => e.Sector)
                    .WithMany()
                    .HasForeignKey(e => e.SectorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.MetricSample)
                    .WithMany()
                    .HasForeignKey(e => e.MetricSampleId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Client Configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SID).IsRequired().HasMaxLength(20);
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasMaxLength(512)
                    .HasConversion(sensitiveNullableStringConverter);
                entity.Property(e => e.ProfileName).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(15);
                entity.Property(e => e.ResidenceAddress).HasMaxLength(500).IsRequired(false);
                entity.Property(e => e.Latitude).IsRequired(false);
                entity.Property(e => e.Longitude).IsRequired(false);
                entity.Property(e => e.Service).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.Address).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.Uptime).HasMaxLength(100).IsRequired(false);
                entity.Property(e => e.ConnectionStatus).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.MacAddress).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.PowerSource).HasMaxLength(100).IsRequired(false);
                entity.Property(e => e.Building).HasMaxLength(150).IsRequired(false);
                entity.Property(e => e.Floor).HasMaxLength(50).IsRequired(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.ReceiverId).IsRequired(false);
                entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.LastUpdated).HasDefaultValueSql("GETDATE()");

                // اسم المستخدم يجب أن يكون فريداً داخل نفس خادم MikroTik (عند ربطه بسيرفر)
                entity.HasIndex(e => new { e.MikroTikServerId, e.UserName })
                      .IsUnique()
                      .HasFilter("[MikroTikServerId] IS NOT NULL");

                entity.HasIndex(e => e.NetworkId);

                entity.HasOne(c => c.Receiver)
                      .WithMany(r => r.Clients)
                      .HasForeignKey(c => c.ReceiverId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(c => c.MikroTikServer)
                      .WithMany()
                      .HasForeignKey(c => c.MikroTikServerId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(c => c.Profile)
                      .WithMany(p => p.Clients)
                      .HasForeignKey(c => c.ProfileId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
            });

            // MikroTikServer Configuration - تم التحديث
            modelBuilder.Entity<MikroTikServer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Host).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Port).HasDefaultValue(8728);
                entity.Property(e => e.User).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Pass)
                    .IsRequired()
                    .HasMaxLength(512)
                    .HasConversion(sensitiveStringConverter);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

                // إضافة فهرس للمضيف والمنفذ
                entity.HasIndex(e => new { e.Host, e.Port }).IsUnique();
            });

            // ApplicationUser Configuration
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Client)
                      .WithMany()
                      .HasForeignKey(u => u.ClientId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.HasOne(u => u.Network)
                      .WithMany(n => n.Users)
                      .HasForeignKey(u => u.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            modelBuilder.Entity<JoinRequest>(entity =>
            {
                entity.Property(e => e.RequestedPassword)
                    .HasMaxLength(512)
                    .HasConversion(sensitiveNullableStringConverter);
            });

            // NetworkFeature Configuration (Entitlements / paid features)
            modelBuilder.Entity<NetworkFeature>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => new { e.NetworkId, e.Key }).IsUnique();
                entity.HasIndex(e => e.NetworkId);

                entity.HasOne(e => e.Network)
                      .WithMany()
                      .HasForeignKey(e => e.NetworkId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();
            });

            // MikroTikServer Network Configuration
            modelBuilder.Entity<MikroTikServer>(entity =>
            {
                entity.HasOne(m => m.Network)
                      .WithMany(n => n.MikroTikServers)
                      .HasForeignKey(m => m.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // Sector Network Configuration
            modelBuilder.Entity<Sector>(entity =>
            {
                entity.HasOne(s => s.Network)
                      .WithMany(n => n.Sectors)
                      .HasForeignKey(s => s.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // Receiver Network Configuration
            modelBuilder.Entity<Receiver>(entity =>
            {
                entity.HasOne(r => r.Network)
                      .WithMany(n => n.Receivers)
                      .HasForeignKey(r => r.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // Client Network Configuration
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasOne(c => c.Network)
                      .WithMany(n => n.Clients)
                      .HasForeignKey(c => c.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // Profile Network Configuration
            modelBuilder.Entity<Profile>(entity =>
            {
                entity.HasOne(p => p.Network)
                      .WithMany(n => n.Profiles)
                      .HasForeignKey(p => p.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // MaintenanceRequest Configuration
            modelBuilder.Entity<MaintenanceRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.RequestDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasDefaultValue(MaintenanceRequestStatus.Pending);
                entity.Property(e => e.Priority);

                entity.HasOne(m => m.Client)
                      .WithMany()
                      .HasForeignKey(m => m.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.AssignedTo)
                      .WithMany()
                      .HasForeignKey(m => m.AssignedToId)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired(false);

                entity.HasOne(m => m.ProcessedBy)
                      .WithMany()
                      .HasForeignKey(m => m.ProcessedById)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired(false);
            });

            // SpeedChangeRequest Configuration
            modelBuilder.Entity<SpeedChangeRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RequestDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasDefaultValue(SpeedChangeRequestStatus.Pending);
                entity.Property(e => e.PriceDifference).HasColumnType("decimal(18,2)");

                entity.HasOne(s => s.Client)
                      .WithMany()
                      .HasForeignKey(s => s.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(s => s.CurrentProfile)
                      .WithMany()
                      .HasForeignKey(s => s.CurrentProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(s => s.RequestedProfile)
                      .WithMany()
                      .HasForeignKey(s => s.RequestedProfileId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(s => s.ProcessedBy)
                      .WithMany()
                      .HasForeignKey(s => s.ProcessedById)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired(false);

                entity.HasOne(s => s.ImplementedBy)
                      .WithMany()
                      .HasForeignKey(s => s.ImplementedById)
                      .OnDelete(DeleteBehavior.NoAction)
                      .IsRequired(false);
            });

            // CollectionPointAccount Configuration
            modelBuilder.Entity<CollectionPointAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasIndex(e => e.NetworkId);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Network)
                      .WithMany()
                      .HasForeignKey(e => e.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // PaymentTransaction Configuration
            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OperationType).HasMaxLength(40).HasDefaultValue("ReceivePayment");
                entity.Property(e => e.ReferenceNumber).HasMaxLength(40);
                entity.Property(e => e.PreviousClientBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewClientBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PreviousPointBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewPointBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PaymentDate).HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.ReceivedByUserId);
                entity.HasIndex(e => e.PaymentDate);
                entity.HasIndex(e => e.NetworkId);
                entity.HasIndex(e => e.ReferenceNumber);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ReceivedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ReceivedByUserId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Network)
                      .WithMany()
                      .HasForeignKey(e => e.NetworkId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);
            });

            // ClientTopUpTransaction Configuration (تغذية رصيد العميل)
            modelBuilder.Entity<ClientTopUpTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PreviousBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewBalance).HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.SourceType);

                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Network)
                    .WithMany()
                    .HasForeignKey(e => e.NetworkId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);

                entity.HasOne(e => e.CollectionPointAccount)
                    .WithMany()
                    .HasForeignKey(e => e.CollectionPointAccountId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });

            // CollectionPointRenewalRequest
            modelBuilder.Entity<CollectionPointRenewalRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
                entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
                entity.Property(e => e.AdminNotes).HasMaxLength(500);
                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.NetworkId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Network).WithMany().HasForeignKey(e => e.NetworkId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.RequestedByUser).WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ProcessedByUser).WithMany().HasForeignKey(e => e.ProcessedByUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            });

            // CollectionPointTopUpRequest (طلبات تغذية رصيد نقطة التحصيل)
            modelBuilder.Entity<CollectionPointTopUpRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Method).HasMaxLength(200);
                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
                entity.Property(e => e.ReceiptImagePath).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
                entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
                entity.Property(e => e.AdminNotes).HasMaxLength(500);
                entity.HasIndex(e => e.CollectionPointAccountId);
                entity.HasIndex(e => e.PaymentMethodId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasOne(e => e.CollectionPointAccount)
                    .WithMany()
                    .HasForeignKey(e => e.CollectionPointAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentMethod)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentMethodId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
                entity.HasOne(e => e.RequestedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.RequestedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ProcessedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ProcessedByUserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });

            // ClientWalletTopUpRequest (طلبات تغذية رصيد المشترك من البوابة)
            modelBuilder.Entity<ClientWalletTopUpRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
                entity.Property(e => e.ReceiptImagePath).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.RequestedByUserId).HasMaxLength(450);
                entity.Property(e => e.ProcessedByUserId).HasMaxLength(450);
                entity.Property(e => e.AdminNotes).HasMaxLength(500);
                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.NetworkId);
                entity.HasIndex(e => e.TargetCollectionPointAccountId);
                entity.HasIndex(e => e.PaymentMethodId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestedAt);
                entity.HasOne(e => e.Client)
                    .WithMany()
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Network)
                    .WithMany()
                    .HasForeignKey(e => e.NetworkId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.TargetCollectionPointAccount)
                    .WithMany()
                    .HasForeignKey(e => e.TargetCollectionPointAccountId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
                entity.HasOne(e => e.PaymentMethod)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentMethodId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.RequestedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.RequestedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ProcessedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ProcessedByUserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .IsRequired(false);
            });

            // Automatically apply all IEntityTypeConfiguration<T> in this assembly.
            // This keeps OnModelCreating concise while preserving existing schema mapping.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}