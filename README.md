# RadaTik - نظام إدارة مزود خدمة الإنترنت (ISP Management System)

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

## 📋 نظرة عامة

**RadaTik** هو نظام شامل لإدارة مزود خدمة الإنترنت (ISP) مبني على ASP.NET Core MVC. يوفر النظام إدارة متكاملة للعملاء، القطاعات، المستقبلات، خوادم MikroTik، والبروفايلات مع مزامنة مباشرة مع أجهزة MikroTik RouterOS.

## ✨ الميزات الرئيسية

### 🔐 إدارة المستخدمين والصلاحيات
- **نظام أدوار متقدم**:
  - **SystemAdministrator**: صلاحيات كاملة على النظام
  - **Employee**: إضافة وتعديل العملاء والمستقبلات
  - **Client**: عرض بياناته الشخصية فقط
- إدارة شاملة للمستخدمين (CRUD)
- ربط المستخدمين بحسابات العملاء

### 👥 إدارة العملاء
- إدارة كاملة لعملاء PPPoE
- مزامنة مباشرة مع MikroTik RouterOS
- إدارة انتهاء صلاحية الحسابات
- تجديد الاشتراكات (يدوي أو تلقائي حتى 8 من كل شهر)
- تجميد وتفعيل الحسابات
- عرض الإحصائيات في الوقت الفعلي

### 📡 إدارة الشبكة
- إدارة القطاعات (Sectors)
- إدارة المستقبلات (Receivers)
- إدارة خوادم MikroTik المتعددة
- إدارة البروفايلات مع مزامنة مع MikroTik

### ⏰ الميزات التلقائية
- **خدمة خلفية للتحقق من الحسابات المنتهية**: فحص تلقائي كل 24 ساعة
- تعطيل تلقائي للحسابات المنتهية
- إشعارات للحسابات القريبة من الانتهاء

### 🎨 واجهة المستخدم
- تصميم حديث ومتجاوب (Responsive)
- دعم **Dark Mode** (الوضع الليلي)
- تصميم مناسب لمزودي خدمة الإنترنت
- واجهة سهلة الاستخدام (User-Friendly)

### 🔄 المزامنة مع MikroTik
- إضافة/تحديث/حذف مستخدمي PPPoE في MikroTik
- جلب البروفايلات تلقائياً من MikroTik
- مزامنة البيانات في اتجاهين
- معالجة الأخطاء مع إعادة المحاولة التلقائية (Retry Logic)

## 🛠️ التقنيات المستخدمة

### Backend
- **ASP.NET Core 9.0** (MVC Pattern)
- **Entity Framework Core 9.0** (Code First)
- **SQL Server** (قاعدة البيانات)
- **ASP.NET Core Identity** (المصادقة والتفويض)

### Frontend
- **Bootstrap 5**
- **Font Awesome 6**
- **jQuery**
- **DataTables**
- **Leaflet.js** (الخرائط)

### Third-Party Libraries
- **tik4net 3.5.0** (الاتصال بـ MikroTik RouterOS API)

## 📦 متطلبات النظام

- **.NET 9.0 SDK** أو أحدث
- **SQL Server 2019** أو أحدث (أو SQL Server Express)
- **MikroTik RouterOS** مع تفعيل API
- **متصفح حديث** (Chrome, Firefox, Edge, Safari)

## 🚀 البدء السريع

### 1. استنساخ المشروع
```bash
git clone <repository-url>
cd RadaTik
```

### 2. تحديث سلسلة الاتصال
قم بتحديث `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "MyDBConnection": "Server=YOUR_SERVER;Database=RadaTikDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. إنشاء قاعدة البيانات
```bash
dotnet ef database update
```

### 4. تشغيل المشروع
```bash
dotnet run
```

### 5. تهيئة حساب مدير النظام (Bootstrap)
قبل أول تشغيل، عرّف متغير البيئة التالي:

- `RADATIK_BOOTSTRAP_ADMIN_PASSWORD`

مثال (PowerShell):

```powershell
$env:RADATIK_BOOTSTRAP_ADMIN_PASSWORD = "StrongPasswordHere!"
```

عند التشغيل الأول سيتم إنشاء المستخدم `admin` تلقائياً بكلمة المرور الموجودة في متغير البيئة.

## 📚 التوثيق

للمزيد من التفاصيل، راجع ملفات التوثيق التالية:

- **[دليل التثبيت](docs/INSTALLATION.md)** - دليل تفصيلي للتثبيت والإعداد
- **[التوثيق الشامل](docs/DOCUMENTATION.md)** - توثيق شامل لجميع الميزات
- **[توثيق API](docs/API_DOCUMENTATION.md)** - توثيق Controllers والأساليب
- **[هيكل المشروع](docs/ARCHITECTURE.md)** - شرح هيكل المشروع والهندسة المعمارية

## 📁 هيكل المشروع

```
RadaTik/
├── Controllers/          # Controllers MVC
│   ├── AccountController.cs
│   ├── AdminController.cs
│   ├── ClientsController.cs
│   ├── ProfileController.cs
│   └── ...
├── Models/              # نماذج البيانات
│   ├── ApplicationUser.cs
│   ├── Client.cs
│   ├── Profile.cs
│   └── ...
├── Views/               # صفحات Razor
│   ├── Account/
│   ├── Admin/
│   ├── Clients/
│   └── ...
├── Services/            # الخدمات
│   ├── MikroTikService.cs
│   └── ExpiredAccountsBackgroundService.cs
├── Data/                # قاعدة البيانات
│   └── ApplicationDbContext.cs
└── wwwroot/            # الملفات الثابتة
```

## 🔒 الصلاحيات والأدوار

### SystemAdministrator (مدير النظام)
- ✅ إدارة المستخدمين والصلاحيات
- ✅ إدارة القطاعات والمستقبلات
- ✅ إدارة خوادم MikroTik والبروفايلات
- ✅ حذف وتعديل جميع البيانات
- ✅ الوصول إلى جميع الواجهات

### Employee (موظف)
- ✅ إضافة وتعديل العملاء والمستقبلات
- ✅ عرض القطاعات والمستقبلات
- ❌ حذف البيانات
- ❌ الوصول إلى MikroTik مباشرة
- ❌ إدارة المستخدمين

### Client (عميل)
- ✅ عرض بيانات حسابه فقط
- ✅ عرض حالة حسابه في قاعدة البيانات
- ❌ تعديل أو حذف أي بيانات
- ❌ الوصول إلى MikroTik

## 🌙 Dark Mode

النظام يدعم الوضع الليلي (Dark Mode):
- زر تبديل في شريط التنقل العلوي
- حفظ التفضيلات في LocalStorage
- دعم تفضيلات النظام التلقائية

## 🔄 المزامنة مع MikroTik

النظام يدعم:
- إضافة مستخدمي PPPoE تلقائياً
- تحديث بيانات المستخدمين
- تجميد/تفعيل الحسابات
- جلب البروفايلات تلقائياً
- Retry Logic معالجة أخطاء الاتصال

## 📊 الإحصائيات

النظام يعرض:
- عدد العملاء (إجمالي/نشط/معطل)
- السرعات الإجمالية والمتوسطات
- العملاء المتصلين فعلياً (PPP Active)
- إحصائيات مفصلة لكل قطاع

## 🐛 معالجة الأخطاء

- Retry Logic للاتصال بـ MikroTik (حتى 3 محاولات)
- Exponential Backoff للإعادة
- تسجيل مفصل للأخطاء
- رسائل خطأ واضحة للمستخدم

## 📝 الملاحظات

- تاريخ انتهاء الصلاحية يتم حفظه في قاعدة البيانات
- النظام يتحقق تلقائياً من الحسابات المنتهية كل 24 ساعة
- المزامنة مع MikroTik تتم فورياً عند التغييرات

## 👥 المساهمة

نرحب بالمساهمات! يرجى:
1. Fork المشروع
2. إنشاء فرع للميزة (`git checkout -b feature/AmazingFeature`)
3. Commit التغييرات (`git commit -m 'Add some AmazingFeature'`)
4. Push إلى الفرع (`git push origin feature/AmazingFeature`)
5. فتح Pull Request

## 📄 الترخيص

هذا المشروع مرخص تحت [MIT License](LICENSE).

## 📞 الدعم

للحصول على المساعدة والدعم:
- افتح [Issue](https://github.com/your-repo/issues) جديد
- راجع [التوثيق الشامل](docs/DOCUMENTATION.md)

## 🙏 شكر وتقدير

- [tik4net](https://github.com/danikf/tik4net) - مكتبة MikroTik API
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet) - إطار العمل
- [Bootstrap](https://getbootstrap.com/) - إطار CSS

---

**تم التطوير بواسطة**: فريق RadaTik  
**آخر تحديث**: يناير 2025
