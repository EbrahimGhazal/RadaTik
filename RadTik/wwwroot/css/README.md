# RadTik CSS Structure
## بنية ملفات CSS

### الملفات الرئيسية:

1. **main.css** - الملف الرئيسي الذي يجمع جميع الملفات
2. **variables.css** - المتغيرات والألوان الأساسية
3. **base.css** - الأنماط الأساسية (Typography, Reset, etc.)
4. **components.css** - المكونات المشتركة (Buttons, Cards, Forms, etc.)
5. **layout.css** - أنماط التخطيط (Sidebar, Header, etc.)
6. **login.css** - أنماط صفحة تسجيل الدخول
7. **utilities.css** - فئات الأدوات المساعدة

### الاستخدام:

في ملفات Razor، استخدم:
```html
<link rel="stylesheet" href="~/css/main.css" asp-append-version="true" />
```

لصفحة تسجيل الدخول فقط:
```html
<link rel="stylesheet" href="~/css/main.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/login.css" asp-append-version="true" />
```
