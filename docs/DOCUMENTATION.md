# 📘 التوثيق الشامل - RadTik ISP Management System

## فهرس المحتويات

1. [نظرة عامة](#نظرة-عامة)
2. [الإعداد والتثبيت](#الإعداد-والتثبيت)
3. [الهيكل المعماري](#الهيكل-المعماري)
4. [النماذج (Models)](#النماذج-models)
5. [Controllers](#controllers)
6. [الخدمات (Services)](#الخدمات-services)
7. [قاعدة البيانات](#قاعدة-البيانات)
8. [المصادقة والتفويض](#المصادقة-والتفويض)
9. [المزامنة مع MikroTik](#المزامنة-مع-mikrotik)
10. [الواجهة الأمامية](#الواجهة-الأمامية)

---

## نظرة عامة

RadTik هو نظام إدارة شامل لمزودي خدمة الإنترنت مبني على ASP.NET Core MVC. يوفر النظام إدارة متكاملة للعملاء، الشبكة، والمزامنة مع أجهزة MikroTik RouterOS.

### الأهداف الرئيسية

1. **إدارة شاملة للعملاء**: إضافة، تعديل، وحذف عملاء PPPoE
2. **المزامنة مع MikroTik**: مزامنة تلقائية مع أجهزة MikroTik RouterOS
3. **إدارة الصلاحيات**: نظام أدوار متقدم مع التحكم الكامل
4. **الإحصائيات**: عرض إحصائيات في الوقت الفعلي

---

## الإعداد والتثبيت

### المتطلبات الأساسية

1. **.NET 9.0 SDK**
   ```bash
   dotnet --version
   # يجب أن يكون 9.0 أو أحدث
   ```

2. **SQL Server**
   - SQL Server 2019 أو أحدث
   - أو SQL Server Express

3. **MikroTik RouterOS**
   - تفعيل API على MikroTik
   - معرفة Host, Port, Username, Password

### خطوات التثبيت

#### 1. استنساخ المشروع
```bash
git clone <repository-url>
cd RadTik
```

#### 2. تحديث appsettings.json

```json
{
  "ConnectionStrings": {
    "MyDBConnection": "Server=localhost;Database=RadTikDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

#### 3. إنشاء قاعدة البيانات

```bash
# التأكد من وجود Entity Framework Tools
dotnet tool install --global dotnet-ef

# إنشاء Migration (إذا لم يكن موجوداً)
dotnet ef migrations add InitialCreate

# تطبيق Migrations على قاعدة البيانات
dotnet ef database update
```

#### 4. تشغيل المشروع

```bash
dotnet run
```

أو من Visual Studio:
- اضغط F5

#### 5. تسجيل الدخول الأولي

- **URL**: `https://localhost:5001` أو `http://localhost:5000`
- **اسم المستخدم**: `admin`
- **كلمة المرور**: `admin@123`

> ⚠️ **تحذير**: قم بتغيير كلمة المرور الافتراضية فوراً!

---

## الهيكل المعماري

### نمط التصميم

النظام يستخدم **MVC (Model-View-Controller)** Pattern:

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Controllers │  ← منطق التطبيق
└──────┬──────┘
       │
       ├──────────┬──────────┐
       ▼          ▼          ▼
┌─────────┐ ┌─────────┐ ┌─────────┐
│ Models  │ │ Services│ │   Data  │
│ (Data)  │ │ (Logic) │ │ (DB)    │
└─────────┘ └─────────┘ └─────────┘
       │
       ▼
┌─────────────┐
│    Views    │  ← واجهة المستخدم
└─────────────┘
```

### طبقات النظام

1. **Presentation Layer** (Controllers + Views)
   - معالجة طلبات HTTP
   - عرض الواجهة للمستخدم

2. **Business Logic Layer** (Services)
   - منطق العمل
   - مزامنة MikroTik
   - الخدمات الخلفية

3. **Data Access Layer** (Models + DbContext)
   - الوصول إلى قاعدة البيانات
   - Entity Framework Core

---

## النماذج (Models)

### ApplicationUser

نموذج المستخدم المخصص الموروث من `IdentityUser`:

```csharp
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public int? ClientId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public bool IsActive { get; set; }
    
    [ForeignKey("ClientId")]
    public virtual Client? Client { get; set; }
}
```

**الخصائص**:
- `FullName`: الاسم الكامل للمستخدم
- `ClientId`: ربط المستخدم بعميل (للعملاء)
- `IsActive`: حالة تفعيل/تعطيل المستخدم

### Client

نموذج العميل (مشترك PPPoE):

```csharp
public class Client
{
    public int Id { get; set; }
    public string Name { get; set; }              // الاسم
    public string? SID { get; set; }              // الرقم الوطني
    public string UserName { get; set; }          // اسم المستخدم PPPoE
    public string Password { get; set; }          // كلمة المرور
    public string? PhoneNumber { get; set; }      // رقم الهاتف
    public string? Address { get; set; }          // العنوان (IP)
    public bool IsActive { get; set; }            // حالة التفعيل
    public DateTime? AccountExpirationDate { get; set; }  // تاريخ الانتهاء
    // ... المزيد
}
```

**العلاقات**:
- `ReceiverId` → `Receiver`
- `MikroTikServerId` → `MikroTikServer`
- `ProfileId` → `Profile`

### Profile

نموذج البروفايل (Profile):

```csharp
public class Profile
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal DownloadSpeed { get; set; }
    public decimal? UploadSpeed { get; set; }
    public bool IsActive { get; set; }
    public bool IsSyncedWithMikroTik { get; set; }
    public int MikroTikServerId { get; set; }
    
    [ForeignKey("MikroTikServerId")]
    public virtual MikroTikServer MikroTikServer { get; set; }
}
```

### MikroTikServer

نموذج خادم MikroTik:

```csharp
public class MikroTikServer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string User { get; set; }
    public string Pass { get; set; }
    public bool IsActive { get; set; }
}
```

---

## Controllers

### AccountController

إدارة المصادقة والحسابات:

**الأساليب**:
- `Login()` - تسجيل الدخول
- `Logout()` - تسجيل الخروج
- `Register()` - تسجيل مستخدم جديد (للمدير فقط)
- `Profile()` - عرض الملف الشخصي

**الصلاحيات**:
- `Login`: عام
- `Register`: SystemAdministrator فقط

### AdminController

إدارة المستخدمين والصلاحيات:

**الأساليب**:
- `Index()` - قائمة المستخدمين
- `Create()` - إنشاء مستخدم جديد
- `Edit()` - تعديل مستخدم
- `Delete()` - تعطيل مستخدم
- `Details()` - تفاصيل مستخدم

**الصلاحيات**: SystemAdministrator فقط

### ClientsController

إدارة العملاء:

**الأساليب**:
- `Index()` - قائمة العملاء
- `Create()` - إضافة عميل جديد
- `Edit()` - تعديل عميل
- `Delete()` - حذف عميل (مدير فقط)
- `Details()` - تفاصيل عميل
- `ToggleStatus()` - تفعيل/تجميد
- `Freeze()` / `Unfreeze()` - تجميد/تفعيل
- `SyncWithMikroTik()` - مزامنة مع MikroTik
- `RenewSubscription()` - تجديد الاشتراك
- `CheckExpiredAccounts()` - فحص الحسابات المنتهية

**الصلاحيات**:
- `Index`: جميع المستخدمين (العميل يرى بياناته فقط)
- `Create`, `Edit`: SystemAdministrator, Employee
- `Delete`: SystemAdministrator فقط

### ProfileController

إدارة البروفايلات:

**الأساليب**:
- `Index()` - قائمة البروفايلات
- `Create()` - إضافة بروفايل
- `Edit()` - تعديل بروفايل
- `Delete()` - حذف بروفايل
- `SyncWithMikroTik()` - مزامنة مع MikroTik

**الصلاحيات**: SystemAdministrator فقط

---

## الخدمات (Services)

### MikroTikService

الخدمة الرئيسية للتعامل مع MikroTik:

#### الدوال الرئيسية:

**1. إدارة المستخدمين**:
```csharp
Task<bool> AddPPPoEUser(Client client)
Task<bool> UpdatePPPoEUser(Client client)
Task<bool> DeletePPPoEUser(string username, int serverId)
Task<bool> DisablePPPoEUser(string username, int serverId)
Task<bool> EnablePPPoEUser(string username, int serverId)
```

**2. إدارة البروفايلات**:
```csharp
Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId)
Task<bool> SyncProfilesWithMikroTik(int serverId)
```

**3. تجديد الاشتراكات**:
```csharp
Task<bool> RenewPPPoESubscription(string username, int serverId, DateTime? newExpirationDate)
Task<bool> RenewSubscriptionTo8thNextMonth(string username, int serverId)
```

**4. التحقق من الحسابات المنتهية**:
```csharp
Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts()
Task<bool> DisableExpiredAccount(string username, int serverId)
```

**5. معلومات الاتصال**:
```csharp
Task<PPPoEUserInfo> GetPPPoEUserInfo(string username, int serverId)
Task<List<Client>> GetActivePPPoEUsers(int serverId)
Task<bool> DisconnectActivePPPoEUser(string username, int serverId)
Task<bool> TestConnection(int serverId)
```

#### معالجة الأخطاء:

- **Retry Logic**: محاولة الاتصال حتى 3 مرات
- **Exponential Backoff**: تأخير متزايد (1s, 2s, 4s)
- **Connection Testing**: اختبار الاتصال قبل الاستخدام

### ExpiredAccountsBackgroundService

خدمة خلفية للتحقق من الحسابات المنتهية:

**الميزات**:
- فحص تلقائي كل 24 ساعة
- تعطيل تلقائي للحسابات المنتهية
- تسجيل مفصل للعمليات

**الإعداد**:
```csharp
// في Program.cs
builder.Services.AddHostedService<ExpiredAccountsBackgroundService>();
```

---

## قاعدة البيانات

### Entity Framework Core

النظام يستخدم **Code First** Approach:

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<MikroTikServer> MikroTikServers { get; set; }
    public DbSet<Receiver> Receivers { get; set; }
    public DbSet<Sector> Sectors { get; set; }
    // ...
}
```

### الجداول الرئيسية:

1. **AspNetUsers** - المستخدمون (Identity)
2. **AspNetRoles** - الأدوار
3. **Clients** - العملاء
4. **Profiles** - البروفايلات
5. **MikroTikServers** - خوادم MikroTik
6. **Receivers** - المستقبلات
7. **Sectors** - القطاعات

### Migrations

```bash
# إنشاء Migration جديد
dotnet ef migrations add MigrationName

# تطبيق Migrations
dotnet ef database update

# الرجوع عن Migration
dotnet ef database update PreviousMigrationName
```

---

## المصادقة والتفويض

### ASP.NET Core Identity

النظام يستخدم Identity للمصادقة:

**الإعداد**:
```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    // ...
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

### الأدوار (Roles)

1. **SystemAdministrator**
   - صلاحيات كاملة
   - إدارة جميع المستخدمين والبيانات

2. **Employee**
   - إضافة وتعديل العملاء والمستقبلات
   - لا يمكن الحذف

3. **Client**
   - عرض بياناته فقط
   - لا يمكن التعديل

### التحكم بالوصول

```csharp
[Authorize]  // يحتاج تسجيل دخول
[Authorize(Roles = "SystemAdministrator")]  // للمدير فقط
[Authorize(Roles = "SystemAdministrator,Employee")]  // للمدير والموظف
```

---

## المزامنة مع MikroTik

### الاتصال

يستخدم النظام **tik4net** library:

```csharp
var connection = ConnectionFactory.OpenConnection(
    TikConnectionType.Api,
    host,
    port,
    username,
    password
);
```

### المزامنة التلقائية

1. **إضافة عميل**: يتم إنشاؤه في MikroTik تلقائياً
2. **تعديل عميل**: يتم تحديثه في MikroTik
3. **تجميد/تفعيل**: يتم تطبيقه في MikroTik
4. **حذف عميل**: يتم حذفه من MikroTik

### Retry Logic

```csharp
private ITikConnection CreateConnectionWithRetry(MikroTikServer server, int maxRetries = 3)
{
    // محاولة الاتصال حتى 3 مرات
    // مع Exponential Backoff
}
```

---

## الواجهة الأمامية

### التقنيات

- **Bootstrap 5**: Framework CSS
- **Font Awesome 6**: الأيقونات
- **jQuery**: JavaScript Library
- **DataTables**: جداول تفاعلية
- **Leaflet.js**: الخرائط

### Dark Mode

النظام يدعم الوضع الليلي:

```javascript
// حفظ التفضيلات في LocalStorage
localStorage.setItem('theme', 'dark');

// تطبيق الوضع
html.setAttribute('data-theme', 'dark');
```

### التصميم المتجاوب

- دعم جميع أحجام الشاشات
- Mobile-First Approach
- تحسينات خاصة للأجهزة المحمولة

---

## الأمان

### حماية البيانات

1. **كلمات المرور**: مشفرة باستخدام Identity
2. **CSRF Protection**: Token في جميع النماذج
3. **SQL Injection**: محمي بواسطة Entity Framework
4. **XSS Protection**: Encoding تلقائي في Razor

### أفضل الممارسات

- استخدام HTTPS في الإنتاج
- تحديث كلمات المرور الافتراضية
- تقييد الوصول إلى قاعدة البيانات
- تسجيل العمليات الحساسة

---

## الدعم والمساعدة

للحصول على المساعدة:
1. راجع [دليل التثبيت](INSTALLATION.md)
2. راجع [توثيق API](API_DOCUMENTATION.md)
3. افتح Issue جديد

---

**آخر تحديث**: يناير 2025
