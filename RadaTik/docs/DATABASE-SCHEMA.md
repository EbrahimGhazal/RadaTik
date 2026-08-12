# مخطط قاعدة البيانات — RadaTik

## المجالات (Bounded Contexts)

| المجال | الجداول الرئيسية |
|--------|------------------|
| **النواة / ISP** | `Networks`, `Sectors`, `Receivers`, `Clients`, `Profiles`, `MikroTikServers` |
| **الهوية والصلاحيات** | `AspNetUsers`, `Permissions`, `UserPermissions`, `JoinRequests` |
| **المحافظ والتحصيل** | `NetworkWalletTransactions`, `NetworkTopUpRequests`, `PaymentTransactions`, `ClientTopUpTransactions`, `ClientWalletTopUpRequests`, `CollectionPointAccounts`, `CollectionPointTopUpRequests`, `SystemAdminWallets`, `vw_WalletLedgerUnified` |
| **الصندوق النقدي** | `CashBoxes`, `CashBoxDeposits`, `CashBoxWithdrawals`, `CashBoxCurrencyExchanges` |
| **الأعمال** | `WarehouseItems`, `MaterialPurchaseInvoices`, `MaterialSalesInvoices`, `MoneyDiaryEntries`, `PayrollEmployees`, … |
| **الصيانة والتركيب** | `MaintenanceRequests`, `MaintenanceInvoices`, `SubscriberInstallationInvoices` |
| **الاشتراكات والتسعير** | `NetworkServiceSubscriptions`, `SystemPricingItems`, `FeaturePricings` |
| **التدقيق** | `AuditLogs`, `UserNotifications` |

## تكوين EF Core

- **`ApplicationDbContext`**: `DbSet` + `OnModelCreating` يستدعي `ApplyConfigurationsFromAssembly` فقط (~85 سطر).
- **`Data/Configurations/`**: كل كيان له `IEntityTypeConfiguration<T>` (60+ ملف).
- **`Data/Infrastructure/`**: `SensitiveDataConverters`, `BalanceConcurrencyExtensions`, فلاتر `HasQueryFilter` لعزل الشبكة.
- **`NetworkTenantMiddleware`**: يربط نطاق الشبكة الحالي بكل طلب HTTP.

## أرصدة حرجة + تزامن

أعمدة `RowVersion` (`rowversion` في SQL Server) على:

- `Networks`
- `Clients`
- `CollectionPointAccounts`
- `CashBoxes`
- `SystemAdminWallets`

## سجل الحركات المالي (نمط)

| الجدول | الغرض |
|--------|--------|
| `NetworkWalletTransactions` | محفظة الشركة |
| `PaymentTransactions` | تحصيل من المشترك |
| `ClientTopUpTransactions` | تغذية رصيد المشترك |
| `CashBoxDeposits` / `Withdrawals` | الصندوق النقدي |
| `vw_WalletLedgerUnified` | عرض قراءة فقط يدمج محفظة الشركة + تغذية المشترك + التحصيل |

## الهجرات

- مسار: `RadaTik/Migrations/`
- دليل التسمية: `docs/MIGRATIONS-GUIDE.md`
- آخر إضافة هيكلية: `AddBalanceRowVersionColumns`
