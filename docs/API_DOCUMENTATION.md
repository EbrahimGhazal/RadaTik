

# 📚 توثيق API - RadaTik Controllers

## فهرس

1. [AccountController](#accountcontroller)
2. [AdminController](#admincontroller)
3. [ClientsController](#clientscontroller)
4. [ProfileController](#profilecontroller)
5. [MikroTikServersController](#mikrotikserverscontroller)
6. [SectorController](#sectorcontroller)
7. [ReceiverController](#receivercontroller)

---

## AccountController

إدارة المصادقة والحسابات الشخصية.

### Login (GET/POST)

**URL**: `/Account/Login`

**Method**: GET, POST

**الصلاحيات**: عام (غير محتاج تسجيل دخول)

**GET Parameters**:
- `returnUrl` (string, optional): URL للرجوع بعد تسجيل الدخول

**POST Model**:
```csharp
public class LoginViewModel
{
    [Required] string UserName;
    [Required] string Password;
    bool RememberMe;
}
```

**Response**: Redirect to Home or returnUrl

---

### Logout (POST)

**URL**: `/Account/Logout`

**Method**: POST

**الصلاحيات**: مسجل دخول

**Response**: Redirect to Login

---

### Register (GET/POST)

**URL**: `/Account/Register`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator فقط

**POST Model**:
```csharp
public class RegisterViewModel
{
    [Required] string UserName;
    [Required] string Email;
    [Required] string Password;
    [Required] string ConfirmPassword;
    string? FullName;
    string? PhoneNumber;
    [Required] string Role;
    int? ClientId;
}
```

**Response**: Redirect to Index or returnUrl

---

### Profile (GET)

**URL**: `/Account/Profile`

**Method**: GET

**الصلاحيات**: مسجل دخول

**Response**: View with user profile data

---

## AdminController

إدارة المستخدمين والصلاحيات (CRUD كامل).

### Index

**URL**: `/Admin`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Response**: List<UserManagementViewModel>

---

### Create (GET/POST)

**URL**: `/Admin/Create`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**POST Model**:
```csharp
public class RegisterViewModel
{
    [Required] string UserName;
    [Required] string Email;
    [Required] string Password;
    string? FullName;
    string? PhoneNumber;
    [Required] List<string> Roles;  // يمكن اختيار أكثر من دور
}
```

**Response**: Redirect to Index on success

---

### Edit (GET/POST)

**URL**: `/Admin/Edit/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (string) - User ID

**POST Model**:
```csharp
public class EditUserViewModel
{
    string Id;
    string UserName;  // readonly
    string Email;
    string? FullName;
    string? PhoneNumber;
    bool IsActive;
    List<string>? SelectedRoles;
    int? ClientId;
}
```

**Response**: Redirect to Index on success

---

### Details

**URL**: `/Admin/Details/{id}`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (string) - User ID

**Response**: UserDetailsViewModel

---

### Delete (GET/POST)

**URL**: `/Admin/Delete/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (string) - User ID

**Note**: يعطل المستخدم ولا يحذفه نهائياً

---

## ClientsController

إدارة العملاء (مشتركي PPPoE).

### Index

**URL**: `/Clients`

**Method**: GET

**الصلاحيات**: مسجل دخول
- **SystemAdministrator, Employee**: يرى جميع العملاء
- **Client**: يرى بياناته فقط

**Response**: List<Client> with statistics

---

### Create (GET/POST)

**URL**: `/Clients/Create`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator, Employee

**POST Model**: Client

**Response**: Redirect to Index on success

**ملاحظات**:
- يتم إنشاء العميل في MikroTik تلقائياً
- يتم ربطه بالبروفايل المحدد

---

### Edit (GET/POST)

**URL**: `/Clients/Edit/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator, Employee

**Parameters**: `id` (int) - Client ID

**POST Model**: Client

**Response**: Redirect to Details on success

---

### Details

**URL**: `/Clients/Details/{id}`

**Method**: GET

**الصلاحيات**: مسجل دخول
- **Client**: يرى بياناته فقط (بدون معلومات MikroTik)

**Parameters**: `id` (int) - Client ID

**Response**: Client with MikroTik info (إذا كان مدير/موظف)

---

### Delete (GET/POST)

**URL**: `/Clients/Delete/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator فقط

**Parameters**: `id` (int) - Client ID

**ملاحظات**:
- يحذف العميل من قاعدة البيانات
- يحذفه من MikroTik تلقائياً

---

### ToggleStatus

**URL**: `/Clients/ToggleStatus`

**Method**: POST

**الصلاحيات**: SystemAdministrator فقط

**Parameters**: `id` (int) - Client ID

**Response**: Redirect to Index

---

### Freeze / Unfreeze

**URL**: `/Clients/Freeze`, `/Clients/Unfreeze`

**Method**: POST

**الصلاحيات**: SystemAdministrator فقط

**Parameters**: `id` (int) - Client ID

**ملاحظات**:
- `Freeze`: يعطل الحساب ويقطع الاتصال النشط
- `Unfreeze`: يفعل الحساب

---

### SyncWithMikroTik

**URL**: `/Clients/SyncWithMikroTik`

**Method**: GET

**الصلاحيات**: SystemAdministrator فقط

**Parameters**: `id` (int) - Client ID

**Response**: Redirect to Details with updated data

---

### RenewSubscription (GET/POST)

**URL**: `/Clients/RenewSubscription/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator فقط

**Parameters**: `id` (int) - Client ID

**POST Model**:
```csharp
public class RenewSubscriptionViewModel
{
    int ClientId;
    DateTime? NewExpirationDate;  // إذا كان null، يجدد حتى 8 من الشهر التالي
    bool RenewTo8thNextMonth;
}
```

---

### CheckExpiredAccounts

**URL**: `/Clients/CheckExpiredAccounts`

**Method**: POST

**الصلاحيات**: SystemAdministrator فقط

**Response**: Redirect to ExpiredAccounts with results

---

### ExpiredAccounts

**URL**: `/Clients/ExpiredAccounts`

**Method**: GET

**الصلاحيات**: SystemAdministrator فقط

**Response**: List of expired clients with statistics

---

## ProfileController

إدارة البروفايلات (Profiles).

### Index

**URL**: `/Profile`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Response**: List<Profile> with statistics

---

### Create (GET/POST)

**URL**: `/Profile/Create`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**POST Model**: Profile

**Response**: Redirect to Index

---

### Edit (GET/POST)

**URL**: `/Profile/Edit/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (int) - Profile ID

**POST Model**: Profile

---

### Delete (GET/POST)

**URL**: `/Profile/Delete/{id}`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (int) - Profile ID

---

### SyncWithMikroTik

**URL**: `/Profile/SyncWithMikroTik`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Parameters**: `serverId` (int) - MikroTik Server ID

**Response**: Redirect to Index with synced profiles

**ملاحظات**:
- يجلب جميع البروفايلات من MikroTik
- ينشئها في قاعدة البيانات إذا لم تكن موجودة

---

## MikroTikServersController

إدارة خوادم MikroTik.

### Index

**URL**: `/MikroTikServers`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Response**: List<MikroTikServer>

---

### Create (GET/POST)

**URL**: `/MikroTikServers/Create`

**Method**: GET, POST

**الصلاحيات**: SystemAdministrator

**POST Model**:
```csharp
public class MikroTikServer
{
    [Required] string Name;
    [Required] string Host;
    [Required] int Port;  // عادة 8728
    [Required] string User;
    [Required] string Pass;
    bool IsActive;
}
```

---

### TestConnection

**URL**: `/MikroTikServers/TestConnection/{id}`

**Method**: GET

**الصلاحيات**: SystemAdministrator

**Parameters**: `id` (int) - Server ID

**Response**: JSON with connection status

---

## SectorController

إدارة القطاعات.

### Index

**URL**: `/Sector`

**Method**: GET

**الصلاحيات**: SystemAdministrator, Employee

---

### Create / Edit / Delete

نفس النمط كما في Controllers الأخرى.

**الصلاحيات**: SystemAdministrator فقط

---

## ReceiverController

إدارة المستقبلات.

### Index

**URL**: `/Receiver`

**Method**: GET

**الصلاحيات**: SystemAdministrator, Employee

---

### Create / Edit

**الصلاحيات**: SystemAdministrator, Employee

### Delete

**الصلاحيات**: SystemAdministrator فقط

---

## Response Codes

- **200 OK**: نجحت العملية
- **302 Redirect**: إعادة توجيه
- **400 Bad Request**: بيانات غير صحيحة
- **401 Unauthorized**: غير مسجل دخول
- **403 Forbidden**: لا يوجد صلاحية
- **404 Not Found**: غير موجود
- **500 Internal Server Error**: خطأ في الخادم

---

## Error Handling

جميع Controllers تستخدم:
- **Try-Catch** blocks
- **ModelState Validation**
- **TempData** للرسائل
- **Logging** للأخطاء

---

**آخر تحديث**: يناير 2025
