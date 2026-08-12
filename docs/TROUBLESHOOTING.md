# 🔧 استكشاف الأخطاء وحلها - RadaTik

## مشاكل شائعة وحلولها

### 1. مشاكل قاعدة البيانات

#### ❌ خطأ: Cannot connect to SQL Server

**الأعراض**:
```
SqlException: A network-related or instance-specific error occurred
```

**الحلول**:
1. تأكد من تشغيل SQL Server:
   ```bash
   # Windows
   services.msc → SQL Server (MSSQLSERVER)
   
   # أو
   net start MSSQLSERVER
   ```

2. تحقق من Connection String:
   ```json
   "Server=localhost;Database=RadaTikDB;Trusted_Connection=True;TrustServerCertificate=True;"
   ```

3. تحقق من Firewall (Port 1433)

4. جرّب SQL Server Authentication:
   ```json
   "Server=localhost;Database=RadaTikDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
   ```

---

#### ❌ خطأ: Database does not exist

**الحل**:
```sql
CREATE DATABASE RadaTikDB;
```

أو:
```bash
dotnet ef database update
```

---

#### ❌ خطأ: Migration failed

**الحلول**:

1. **حذف Migration وإعادة إنشائها** (⚠️ سيحذف البيانات):
   ```bash
   dotnet ef migrations remove
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

2. **تحديث Migration يدوياً**:
   - افتح ملف Migration
   - عدّل الكود
   - `dotnet ef database update`

---

### 2. مشاكل MikroTik

#### ❌ خطأ: Unable to read data from transport connection

**الأعراض**:
```
An existing connection was forcibly closed by the remote host
```

**الحلول**:

1. **تفعيل API على MikroTik**:
   ```
   /ip service enable api
   /ip service print
   ```

2. **التحقق من Firewall**:
   - تأكد من فتح Port 8728
   - في MikroTik: `/ip firewall filter print`

3. **التحقق من بيانات الاتصال**:
   - Host (IP Address)
   - Port (عادة 8728)
   - Username
   - Password

4. **اختبار الاتصال من Winbox**:
   - جرب الاتصال بنفس البيانات
   - إذا نجح، المشكلة في الكود

5. **التحقق من Retry Logic**:
   - النظام يحاول 3 مرات تلقائياً
   - انتظر قليلاً قبل إعادة المحاولة

---

#### ❌ خطأ: Invalid username or password

**الحلول**:
1. تحقق من بيانات الاتصال في **إدارة → خوادم المايكروتك**
2. تأكد من أن المستخدم لديه صلاحيات API
3. جرّب إنشاء مستخدم جديد على MikroTik:
   ```
   /user add name=radatik password=yourpassword group=full
   ```

---

#### ❌ خطأ: Profile not found

**الحلول**:
1. اذهب إلى **الإدارة → البروفايلات**
2. اضغط **مزامنة مع المايكروتك**
3. تأكد من وجود البروفايل في MikroTik:
   ```
   /ppp profile print
   ```

---

### 3. مشاكل المصادقة

#### ❌ لا يمكن تسجيل الدخول

**الحلول**:
1. تحقق من بيانات الدخول:
   - **افتراضي**: `admin` / `admin@123`

2. إذا نسيت كلمة المرور:
   - حذف قاعدة البيانات وإعادة إنشائها:
     ```bash
     dotnet ef database drop
     dotnet ef database update
     ```

3. أو عدّل كلمة المرور في قاعدة البيانات:
   ```sql
   -- Hash لكلمة المرور الجديدة
   -- (استخدم Identity Password Hasher)
   ```

---

#### ❌ Access Denied (403)

**الحلول**:
1. تحقق من الدور (Role):
   - تأكد من أن المستخدم لديه الدور المطلوب
   - في **إدارة المستخدمين**، تحقق من الأدوار

2. تأكد من `[Authorize]` Attribute:
   - بعض الصفحات تحتاج صلاحيات خاصة
   - راجع [API Documentation](API_DOCUMENTATION.md)

---

### 4. مشاكل التثبيت

#### ❌ dotnet ef not found

**الحل**:
```bash
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef
```

---

#### ❌ NuGet packages not found

**الحل**:
```bash
dotnet restore
dotnet build
```

---

#### ❌ Port already in use

**الأعراض**:
```
System.Net.Sockets.SocketException: Address already in use
```

**الحلول**:
1. **تغيير Port في `launchSettings.json`**:
   ```json
   "applicationUrl": "https://localhost:5001;http://localhost:5000"
   ```

2. **أو إيقاف التطبيق الذي يستخدم Port**:
   ```bash
   # Windows
   netstat -ano | findstr :5000
   taskkill /PID <PID> /F
   ```

---

### 5. مشاكل الخدمات الخلفية

#### ❌ ExpiredAccountsBackgroundService لا يعمل

**الحلول**:
1. **تحقق من Logs**:
   - افتح Console/Output
   - ابحث عن `ExpiredAccountsBackgroundService`

2. **تحقق من التسجيل**:
   - في `Program.cs`:
     ```csharp
     builder.Services.AddHostedService<ExpiredAccountsBackgroundService>();
     ```

3. **اختبار يدوي**:
   - اذهب إلى **العملاء → فحص الحسابات المنتهية**

---

### 6. مشاكل الواجهة

#### ❌ Dark Mode لا يعمل

**الحلول**:
1. **تحقق من JavaScript**:
   - افتح Console (F12)
   - ابحث عن أخطاء JavaScript

2. **تحقق من LocalStorage**:
   ```javascript
   localStorage.getItem('theme')
   ```

3. **Clear Cache**:
   - اضغط `Ctrl+Shift+R` (Hard Refresh)

---

#### ❌ الصفحة لا تعمل (404)

**الحلول**:
1. **تحقق من Route**:
   - راجع `Program.cs`:
     ```csharp
     pattern: "{controller=Account}/{action=Login}/{id?}"
     ```

2. **تحقق من Controller/Action**:
   - تأكد من وجود Controller و Action

---

### 7. مشاكل الأداء

#### ❌ بطء في التحميل

**الحلول**:
1. **Lazy Loading**:
   - تأكد من استخدام `.Include()` عند الحاجة

2. **Pagination**:
   - استخدم pagination للقوائم الكبيرة

3. **Caching**:
   - Cache الإحصائيات المكلفة

---

#### ❌ Timeout في MikroTik

**الحلول**:
1. **زيادة Timeout**:
   - في `MikroTikService`, أضف timeout settings

2. **Retry Logic**:
   - النظام يحاول 3 مرات تلقائياً

---

## Logging

### عرض Logs

1. **في Console** (Development):
   - Logs تظهر تلقائياً في Console

2. **في Production**:
   - راجع `appsettings.json`:
     ```json
     "Logging": {
       "LogLevel": {
         "Default": "Information"
       }
     }
     ```

### تسجيل مخصص

```csharp
_logger.LogInformation("Message");
_logger.LogWarning("Warning");
_logger.LogError(exception, "Error");
```

---

## الاختبار (Testing)

### اختبار قاعدة البيانات

```bash
# التحقق من الاتصال
dotnet ef dbcontext info

# عرض Migrations
dotnet ef migrations list
```

### اختبار MikroTik

1. **من النظام**:
   - **إدارة → خوادم المايكروتك → اختبار الاتصال**

2. **من Winbox**:
   - جرب الاتصال بنفس البيانات

3. **من Command Line** (إذا كان tik4net موجود):
   ```bash
   # جرب الاتصال مباشرة
   ```

---

## الدعم الفني

إذا لم تجد الحل:

1. **راجع التوثيق**:
   - [التوثيق الشامل](DOCUMENTATION.md)
   - [API Documentation](API_DOCUMENTATION.md)

2. **افتح Issue**:
   - وصف المشكلة
   - خطوات إعادة الإنتاج
   - Logs (إن وجدت)
   - معلومات النظام (.NET version, SQL Server version, etc.)

3. **Check Logs**:
   - افتح Console/Output
   - ابحث عن أخطاء

---

## Checklist للتحقق

قبل طلب المساعدة، تأكد من:

- [ ] .NET 9.0 SDK مثبت
- [ ] SQL Server يعمل
- [ ] قاعدة البيانات موجودة
- [ ] Connection String صحيح
- [ ] Migrations مطبقة (`dotnet ef database update`)
- [ ] MikroTik API مفعل
- [ ] بيانات MikroTik صحيحة
- [ ] Ports غير مستخدمة (5000, 5001)
- [ ] Firewall يسمح بالاتصال

---

**آخر تحديث**: يناير 2025
