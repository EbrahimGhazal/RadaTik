# دليل هجرات EF Core

## تسمية الملفات (إنتاج)

```
YYYYMMDDHHMMSS_<وصف_واضح_بالإنجليزية>.cs
```

أمثلة صحيحة:

- `AddBalanceRowVersionColumns`
- `AddMultiCurrencyCompanyWalletPhase2`

تجنّب: `test20260520`, `test1`, `Repair*` إلا لترقعة لمرة واحدة موثّقة.

## إنشاء هجرة

```bash
dotnet ef migrations add <Name> --project RadaTik/RadaTik.csproj
dotnet ef database update --project RadaTik/RadaTik.csproj
```

## سكربتات SQL مضمّنة

- `Migrations/Sql/AddCompanyBusinessModuleTables.sql`
- `Migrations/Sql/AddSubscriberInstallationModuleTables.sql`

## حساب مدير النظام الافتراضي

بعد إنشاء الجداول، تُنشأ في قاعدة البيانات الإجراءات:

- `dbo.usp_EnsureDefaultSystemAdministrator` — ينشئ/يزامن المستخدم `admin` ودور `SystemAdministrator`.

يُستدعى تلقائياً عند تشغيل التطبيق (بعد `Migrate`)، أو يدوياً:

```bash
dotnet run --project RadaTik/RadaTik.csproj -- --ensure-default-admin-sql
dotnet run --project RadaTik/RadaTik.csproj -- --ensure-default-admin-sql --reset-password
```

المصدر: `Data/Sql/usp_EnsureDefaultSystemAdministrator.sql` — الافتراضيات في `SystemAdminBootstrapDefaults` (`admin` / `admin@123`).

## قبل الإنتاج

1. مراجعة `ApplicationDbContextModelSnapshot.cs`
2. اختبار `dotnet test` على `RadaTik.Tests`
3. نسخ احتياطي لقاعدة البيانات
