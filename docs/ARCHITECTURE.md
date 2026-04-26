# 🏗️ الهندسة المعمارية - RadTik

## نظرة عامة

RadTik مبني على **ASP.NET Core MVC** باستخدام **Clean Architecture** principles مع فصل واضح للطبقات.

---

## الهيكل العام

```
RadTik/
├── Controllers/          # Presentation Layer
│   └── [Controllers]
├── Models/              # Data Models
│   └── [Entity Models]
├── Views/               # UI Layer
│   └── [Razor Views]
├── Services/            # Business Logic Layer
│   ├── MikroTikService.cs
│   └── ExpiredAccountsBackgroundService.cs
├── Data/                # Data Access Layer
│   └── ApplicationDbContext.cs
└── wwwroot/            # Static Files
```

---

## الطبقات (Layers)

### 1. Presentation Layer

**Location**: `Controllers/`, `Views/`

**المسؤوليات**:
- معالجة طلبات HTTP
- التحقق من الصلاحيات
- عرض البيانات للمستخدم
- إدارة النماذج (Forms)

**Controllers**:
- `AccountController`: المصادقة
- `AdminController`: إدارة المستخدمين
- `ClientsController`: إدارة العملاء
- `ProfileController`: إدارة البروفايلات
- `MikroTikServersController`: إدارة الخوادم
- `SectorController`: إدارة القطاعات
- `ReceiverController`: إدارة المستقبلات

---

### 2. Business Logic Layer

**Location**: `Services/`

**المسؤوليات**:
- منطق العمل الأساسي
- المزامنة مع MikroTik
- الخدمات الخلفية (Background Services)

**Services**:

#### MikroTikService

```csharp
public class MikroTikService
{
    // إدارة PPPoE Users
    Task<bool> AddPPPoEUser(Client client)
    Task<bool> UpdatePPPoEUser(Client client)
    Task<bool> DeletePPPoEUser(string username, int serverId)
    
    // إدارة Profiles
    Task<List<MikroTikProfileInfo>> GetProfilesFromMikroTik(int serverId)
    
    // تجديد الاشتراكات
    Task<bool> RenewPPPoESubscription(...)
    
    // التحقق من الحسابات المنتهية
    Task<ExpiredAccountsResult> CheckAndDisableExpiredAccounts()
}
```

#### ExpiredAccountsBackgroundService

```csharp
public class ExpiredAccountsBackgroundService : BackgroundService
{
    // فحص تلقائي كل 24 ساعة
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
}
```

---

### 3. Data Access Layer

**Location**: `Data/`, `Models/`

**المسؤوليات**:
- الوصول إلى قاعدة البيانات
- تعريف Entity Models
- Migrations

#### ApplicationDbContext

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Client> Clients { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<MikroTikServer> MikroTikServers { get; set; }
    // ...
}
```

#### Entity Models

- `ApplicationUser`: المستخدم (Identity)
- `Client`: العميل (PPPoE User)
- `Profile`: البروفايل
- `MikroTikServer`: خادم MikroTik
- `Receiver`: المستقبل
- `Sector`: القطاع

---

## التدفق (Flow)

### إضافة عميل جديد

```
User (Browser)
    ↓
ClientsController.Create (POST)
    ↓
Model Validation
    ↓
MikroTikService.AddPPPoEUser()
    ↓
    ├─→ MikroTik RouterOS API
    └─→ ApplicationDbContext (Save to DB)
    ↓
Success → Redirect to Index
```

### المزامنة مع MikroTik

```
ProfileController.SyncWithMikroTik()
    ↓
MikroTikService.GetProfilesFromMikroTik()
    ↓
tik4net Connection
    ↓
MikroTik RouterOS (/ppp/profile/print)
    ↓
Parse & Save to Database
    ↓
Return Synced Profiles
```

---

## أنماط التصميم (Design Patterns)

### 1. MVC Pattern

**Model**: `Models/`
**View**: `Views/`
**Controller**: `Controllers/`

### 2. Dependency Injection

```csharp
// في Program.cs
builder.Services.AddScoped<MikroTikService>();
builder.Services.AddHostedService<ExpiredAccountsBackgroundService>();
```

### 3. Repository Pattern (Implicit)

يستخدم Entity Framework Core كـ Repository:

```csharp
_context.Clients.Add(client);
await _context.SaveChangesAsync();
```

### 4. Service Pattern

الخدمات منفصلة عن Controllers:

```csharp
public class ClientsController
{
    private readonly MikroTikService _mikroTikService;
    
    public ClientsController(MikroTikService mikroTikService) { }
}
```

---

## قاعدة البيانات

### Schema

```
┌─────────────────┐
│ AspNetUsers     │ (Identity)
│ (ApplicationUser)│
└────────┬────────┘
         │
         │ 1:1 (optional)
         │
┌────────▼────────┐
│    Clients      │
└───┬──────┬──────┘
    │      │
    │      │ N:1
    │      │
    │ ┌────▼──────────┐
    │ │   Profile     │
    │ └───────┬───────┘
    │         │
    │         │ N:1
    │         │
    │ ┌───────▼────────┐
    │ │ MikroTikServer │
    │ └────────────────┘
    │
    │ N:1
    │
┌───▼────────┐
│  Receiver  │
└──────┬─────┘
       │
       │ N:1
       │
┌──────▼──────┐
│   Sector    │
└─────────────┘
```

### العلاقات

- **Client → Profile**: N:1 (كل عميل له بروفايل واحد)
- **Client → MikroTikServer**: N:1 (كل عميل على خادم واحد)
- **Client → Receiver**: N:1 (كل عميل في مستقبل واحد)
- **Receiver → Sector**: N:1 (كل مستقبل في قطاع واحد)
- **Profile → MikroTikServer**: N:1 (كل بروفايل على خادم واحد)
- **ApplicationUser → Client**: 1:1 (اختياري - للعملاء)

---

## الأمان (Security)

### 1. Authentication

- **ASP.NET Core Identity**
- Cookie-based Authentication
- Password Hashing (Identity)

### 2. Authorization

- **Role-Based Access Control (RBAC)**
- `[Authorize(Roles = "...")]` Attributes

### 3. Data Protection

- **CSRF Tokens**: في جميع النماذج
- **SQL Injection Protection**: Entity Framework Parameterized Queries
- **XSS Protection**: Razor Encoding

---

## المزامنة مع MikroTik

### Connection Flow

```
MikroTikService
    ↓
CreateConnectionWithRetry() [Retry up to 3 times]
    ↓
tik4net ConnectionFactory
    ↓
MikroTik RouterOS API (Port 8728)
    ↓
Execute Commands (/ppp/secret/...)
```

### Retry Logic

```csharp
for (attempt = 1 to 3) {
    try {
        connection = OpenConnection();
        testCommand = TestConnection();
        return connection;
    } catch {
        if (attempt < 3) {
            Sleep(exponential backoff);
        }
    }
}
```

---

## الخدمات الخلفية (Background Services)

### ExpiredAccountsBackgroundService

```csharp
public class ExpiredAccountsBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(...)
    {
        while (!cancelled) {
            // Check expired accounts
            // Disable in MikroTik
            // Update database
            
            await Task.Delay(24 hours);
        }
    }
}
```

**الإعداد**:
- Registered in `Program.cs`
- Runs automatically on application start
- Executes every 24 hours

---

## التطوير المستقبلي

### تحسينات مقترحة

1. **Caching Layer**
   - Redis Cache للملفات المتكررة
   - Cache MikroTik profiles

2. **API Layer**
   - RESTful API للموبايل
   - GraphQL (اختياري)

3. **Message Queue**
   - RabbitMQ أو Azure Service Bus
   - للعمليات غير المتزامنة

4. **Logging & Monitoring**
   - Application Insights
   - Structured Logging

5. **Unit Testing**
   - xUnit
   - Moq for mocking

---

## الأداء (Performance)

### التحسينات الحالية

1. **Lazy Loading**: EF Core (مفعل افتراضياً)
2. **Connection Pooling**: SQL Server
3. **Retry Logic**: للاتصال بـ MikroTik

### توصيات

1. **Async/Await**: جميع العمليات I/O
2. **Pagination**: للقوائم الكبيرة
3. **Caching**: للإحصائيات

---

## التوثيق

- **XML Comments**: في الكود (مستقبلاً)
- **API Documentation**: Markdown
- **Architecture Diagrams**: هذا الملف

---

**آخر تحديث**: يناير 2025
