# 🚀 دليل التثبيت والإعداد - RadTik

## متطلبات النظام

### المتطلبات الأساسية

1. **.NET 9.0 SDK أو أحدث**
   - تحميل من: https://dotnet.microsoft.com/download
   - التحقق من التثبيت: `dotnet --version`

2. **SQL Server**
   - SQL Server 2019 أو أحدث
   - أو SQL Server Express (مجاني)
   - تحميل من: https://www.microsoft.com/sql-server/sql-server-downloads

3. **Visual Studio 2022 أو Visual Studio Code** (اختياري)
   - Visual Studio: https://visualstudio.microsoft.com/
   - VS Code: https://code.visualstudio.com/

4. **Git** (اختياري)
   - تحميل من: https://git-scm.com/

### متطلبات MikroTik

- **RouterOS** مع تفعيل API
- معرفة معلومات الاتصال:
  - Host (IP Address)
  - Port (عادة 8728)
  - Username
  - Password

---

## خطوات التثبيت

### 1. استنساخ المشروع

```bash
git clone <repository-url>
cd RadTik
```

أو قم بتحميل الملفات مباشرة.

### 2. إعداد قاعدة البيانات

#### أ. إنشاء قاعدة البيانات

افتح **SQL Server Management Studio (SSMS)** وأنشئ قاعدة بيانات جديدة:

```sql
CREATE DATABASE RadTikDB;
```

أو استخدم Command Line:

```bash
sqlcmd -S localhost -Q "CREATE DATABASE RadTikDB"
```

#### ب. تحديث Connection String

افتح ملف `appsettings.json` وحدّث سلسلة الاتصال:

```json
{
  "ConnectionStrings": {
    "MyDBConnection": "Server=localhost;Database=RadTikDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**للاستخدام مع SQL Server Authentication**:
```json
"MyDBConnection": "Server=localhost;Database=RadTikDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
```

### 3. تثبيت Entity Framework Tools

```bash
dotnet tool install --global dotnet-ef
```

إذا كانت مثبتة مسبقاً، قم بتحديثها:
```bash
dotnet tool update --global dotnet-ef
```

### 4. إنشاء وتطبيق Migrations

```bash
# الانتقال إلى مجلد المشروع
cd RadTik

# تطبيق Migrations الموجودة
dotnet ef database update
```

إذا احتجت إنشاء Migration جديد:
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

### 5. إعداد MikroTik

#### أ. تفعيل API على MikroTik

1. افتح **Winbox** أو **WebFig**
2. اذهب إلى: **IP → Services**
3. تأكد من تفعيل **API** (Port: 8728)

#### ب. إنشاء مستخدم API

1. اذهب إلى: **System → Users**
2. أضف مستخدم جديد:
   - **Username**: اسم المستخدم
   - **Password**: كلمة المرور
   - **Group**: `full` (أو أنشئ مجموعة مخصصة)

#### ج. إضافة خادم MikroTik في النظام

1. سجل الدخول إلى RadTik
2. اذهب إلى: **الإدارة → خوادم المايكروتك**
3. اضغط **إضافة خادم جديد**
4. أدخل المعلومات:
   - **الاسم**: اسم وصفي
   - **Host**: IP Address
   - **Port**: 8728 (أو المنفذ المخصص)
   - **User**: اسم المستخدم
   - **Pass**: كلمة المرور
5. اضغط **اختبار الاتصال** للتأكد
6. احفظ

### 6. تشغيل المشروع

#### من Command Line:

```bash
dotnet run
```

#### من Visual Studio:

1. افتح `RadTik.sln`
2. اضغط **F5** أو **Ctrl+F5**

#### من Visual Studio Code:

1. افتح مجلد المشروع
2. اضغط **F5**
3. اختر **.NET Core**

### 7. تسجيل الدخول الأولي

افتح المتصفح على:
- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`

**بيانات الدخول الافتراضية**:
- **اسم المستخدم**: `admin`
- **كلمة المرور**: `admin@123`

> ⚠️ **مهم جداً**: قم بتغيير كلمة المرور فوراً بعد أول تسجيل دخول!

---

## إعدادات إضافية

### 1. تغيير كلمة مرور المدير

1. سجل الدخول كـ `admin`
2. اذهب إلى: **اسم المستخدم (أعلى اليمين) → الملف الشخصي**
3. أو اذهب إلى: **الإدارة → إدارة المستخدمين**
4. عدّل كلمة المرور

### 2. إنشاء مستخدمين جدد

1. سجل الدخول كمدير
2. اذهب إلى: **الإدارة → إدارة المستخدمين**
3. اضغط **إضافة مستخدم جديد**
4. املأ البيانات واختر الدور

### 3. إضافة قطاع

1. اذهب إلى: **القطاعات**
2. اضغط **إضافة قطاع جديد**
3. أدخل اسم القطاع

### 4. إضافة مستقبل

1. اذهب إلى: **المستقبلات**
2. اضغط **إضافة مستقبل جديد**
3. أدخل المعلومات واختر القطاع

### 5. مزامنة البروفايلات

1. اذهب إلى: **الإدارة → البروفايلات**
2. اضغط **مزامنة مع المايكروتك** (للخادم المحدد)
3. سيتم جلب جميع البروفايلات تلقائياً

---

## التحقق من التثبيت

### 1. التحقق من قاعدة البيانات

```bash
# التحقق من الاتصال
dotnet ef dbcontext info
```

### 2. التحقق من MikroTik

1. اذهب إلى: **الإدارة → خوادم المايكروتك**
2. اضغط **اختبار الاتصال** لكل خادم

### 3. التحقق من الخدمات الخلفية

افتح **Logs** للتأكد من أن:
- `ExpiredAccountsBackgroundService` يعمل
- لا توجد أخطاء في الاتصال

---

## استكشاف الأخطاء

### مشكلة: لا يمكن الاتصال بقاعدة البيانات

**الحلول**:
1. تأكد من تشغيل SQL Server
2. تحقق من Connection String
3. تأكد من أن قاعدة البيانات موجودة
4. تحقق من أذونات المستخدم

```bash
# اختبار الاتصال
sqlcmd -S localhost -d RadTikDB -Q "SELECT 1"
```

### مشكلة: خطأ في Migrations

**الحلول**:
```bash
# حذف جميع Migrations وإعادة إنشائها (⚠️ سيحذف البيانات)
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### مشكلة: لا يمكن الاتصال بـ MikroTik

**الحلول**:
1. تأكد من تفعيل API على MikroTik
2. تحقق من Firewall
3. تحقق من بيانات الاتصال (Host, Port, Username, Password)
4. جرّب الاتصال من Winbox بنفس البيانات

### مشكلة: الصفحة لا تعمل

**الحلول**:
1. تحقق من Ports (5000, 5001)
2. تحقق من Logs في Console
3. تأكد من تثبيت جميع NuGet Packages:
   ```bash
   dotnet restore
   ```

---

## الترقية (Upgrade)

### ترقية قاعدة البيانات

```bash
dotnet ef database update
```

### ترقية NuGet Packages

```bash
dotnet restore
dotnet list package --outdated
```

---

## النسخ الاحتياطي (Backup)

### نسخ احتياطي لقاعدة البيانات

```sql
BACKUP DATABASE RadTikDB 
TO DISK = 'C:\Backup\RadTikDB.bak'
WITH FORMAT, INIT, NAME = 'RadTik Full Backup';
```

### استعادة من النسخة الاحتياطية

```sql
RESTORE DATABASE RadTikDB 
FROM DISK = 'C:\Backup\RadTikDB.bak'
WITH REPLACE;
```

---

## النشر (Deployment)

### نشر على IIS

1. أنشئ Publish Profile:
   ```bash
   dotnet publish -c Release -o C:\Publish
   ```

2. في IIS:
   - إنشاء Application Pool (.NET 9.0)
   - إنشاء Website
   - تحديث Connection String في `appsettings.Production.json`

### نشر على Linux

```bash
dotnet publish -c Release -o /var/www/radtik
```

---

## الدعم

إذا واجهت مشاكل:
1. راجع [التوثيق الشامل](DOCUMENTATION.md)
2. راجع [API Documentation](API_DOCUMENTATION.md)
3. افتح Issue جديد

---

**آخر تحديث**: يناير 2025
