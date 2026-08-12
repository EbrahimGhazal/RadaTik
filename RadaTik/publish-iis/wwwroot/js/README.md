# RadaTik JavaScript Structure
## بنية ملفات JavaScript

### الملفات المتوفرة:

1. **theme.js** - إدارة الوضع الداكن/الفاتح
2. **login.js** - وظائف صفحة تسجيل الدخول (التحقق من النماذج)
3. **layout.js** - وظائف التخطيط (Sidebar, Dropdowns, Network Selector)
4. **common.js** - وظائف مشتركة (Formatting, Toast, Select2, etc.)

### الاستخدام:

في `_Layout.cshtml`:
```html
<script src="~/js/common.js" asp-append-version="true"></script>
<script src="~/js/layout.js" asp-append-version="true"></script>
```

في `Login.cshtml`:
```html
<script src="~/js/theme.js" asp-append-version="true"></script>
<script src="~/js/login.js" asp-append-version="true"></script>
```

### الوظائف المتاحة:

#### CommonUtils
- `formatDate(date, includeTime)` - تنسيق التاريخ
- `formatNumber(number, decimals)` - تنسيق الأرقام
- `showToast(message, type, duration)` - عرض إشعار
- `confirm(message, callback)` - تأكيد
- `updatePendingRequestsCount(url)` - تحديث عدد الطلبات المعلقة
- `initSelect2()` - تهيئة Select2

#### ThemeManager
- `init()` - تهيئة إدارة الوضع
- `toggleTheme()` - تبديل الوضع

#### LayoutManager
- `setupSidebar()` - إعداد الشريط الجانبي
- `setupMobileMenu()` - إعداد القائمة المحمولة
- `setupDropdowns()` - إعداد القوائم المنسدلة
- `setupNetworkSelector()` - إعداد محدد الشبكة

#### LoginPage
- `setupFormValidation()` - إعداد التحقق من النماذج
- `validateInput(input)` - التحقق من حقل الإدخال
