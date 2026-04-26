# 📚 دليل التوثيق - RadTik ISP Management System

مرحباً بك في توثيق RadTik! هذا الدليل يساعدك على فهم واستخدام النظام بشكل كامل.

---

## 📖 محتويات التوثيق

### 1. [التوثيق الشامل (DOCUMENTATION.md)](DOCUMENTATION.md)
**الدليل الكامل للنظام**
- نظرة عامة على النظام
- الهيكل المعماري
- النماذج (Models)
- Controllers بالتفصيل
- الخدمات (Services)
- قاعدة البيانات
- المصادقة والتفويض
- المزامنة مع MikroTik
- الواجهة الأمامية

**💡 ابدأ من هنا إذا كنت تريد فهم شامل للنظام**

---

### 2. [دليل التثبيت (INSTALLATION.md)](INSTALLATION.md)
**خطوات التثبيت والإعداد الكاملة**
- متطلبات النظام
- خطوات التثبيت خطوة بخطوة
- إعداد قاعدة البيانات
- إعداد MikroTik
- إعدادات إضافية
- التحقق من التثبيت
- النسخ الاحتياطي
- النشر (Deployment)

**💡 اقرأ هذا إذا كنت تقوم بتثبيت النظام لأول مرة**

---

### 3. [توثيق API (API_DOCUMENTATION.md)](API_DOCUMENTATION.md)
**توثيق كامل لجميع Controllers والأساليب**
- AccountController
- AdminController
- ClientsController
- ProfileController
- MikroTikServersController
- SectorController
- ReceiverController
- Response Codes
- Error Handling

**💡 راجع هذا لفهم API والصلاحيات المطلوبة**

---

### 4. [الهندسة المعمارية (ARCHITECTURE.md)](ARCHITECTURE.md)
**شرح هيكل المشروع والتصميم**
- الهيكل العام
- الطبقات (Layers)
- التدفق (Flow)
- أنماط التصميم
- قاعدة البيانات (Schema)
- الأمان
- المزامنة مع MikroTik
- الخدمات الخلفية

**💡 راجع هذا لفهم البنية الداخلية للنظام**

---

### 5. [استكشاف الأخطاء (TROUBLESHOOTING.md)](TROUBLESHOOTING.md)
**حلول المشاكل الشائعة**
- مشاكل قاعدة البيانات
- مشاكل MikroTik
- مشاكل المصادقة
- مشاكل التثبيت
- مشاكل الخدمات الخلفية
- مشاكل الواجهة
- مشاكل الأداء
- Logging والاختبار

**💡 راجع هذا عند مواجهة مشاكل**

---

## 🚀 البدء السريع

### للمطورين الجدد

1. اقرأ [README.md](../README.md) - نظرة عامة
2. اتبع [INSTALLATION.md](INSTALLATION.md) - التثبيت
3. راجع [DOCUMENTATION.md](DOCUMENTATION.md) - فهم النظام

### للمستخدمين

1. اقرأ [INSTALLATION.md](INSTALLATION.md) - التثبيت
2. راجع [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - عند مواجهة مشاكل

### للمطورين المحترفين

1. راجع [ARCHITECTURE.md](ARCHITECTURE.md) - الهندسة المعمارية
2. راجع [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - API
3. راجع [DOCUMENTATION.md](DOCUMENTATION.md) - التفاصيل

---

## 📋 فهرس سريع

### الميزات الرئيسية
- [نظام المصادقة والتفويض](../README.md#-إدارة-المستخدمين-والصلاحيات)
- [إدارة العملاء](../README.md#-الميزات-الرئيسية)
- [المزامنة مع MikroTik](../README.md#-المزامنة-مع-mikrotik)
- [الوضع الليلي (Dark Mode)](../README.md#-dark-mode)

### التقنيات
- [ASP.NET Core MVC](ARCHITECTURE.md#الهيكل-العام)
- [Entity Framework Core](DOCUMENTATION.md#قاعدة-البيانات)
- [MikroTik API](DOCUMENTATION.md#المزامنة-مع-mikrotik)

### الصلاحيات
- [SystemAdministrator](API_DOCUMENTATION.md#admincontroller)
- [Employee](API_DOCUMENTATION.md#clientscontroller)
- [Client](API_DOCUMENTATION.md#clientscontroller)

---

## 🔍 البحث في التوثيق

### إذا كنت تبحث عن:

**كيفية تثبيت النظام؟**
→ [INSTALLATION.md](INSTALLATION.md)

**كيف يعمل النظام؟**
→ [DOCUMENTATION.md](DOCUMENTATION.md)

**ما هي API المتاحة؟**
→ [API_DOCUMENTATION.md](API_DOCUMENTATION.md)

**كيف يتم تصميم النظام؟**
→ [ARCHITECTURE.md](ARCHITECTURE.md)

**حل مشكلة معينة؟**
→ [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

---

## 📝 ملاحظات مهمة

1. **كلمة المرور الافتراضية**: `admin@123`
   - ⚠️ قم بتغييرها فوراً بعد التثبيت!

2. **MikroTik API**: يجب تفعيله على RouterOS
   - Port: 8728 (افتراضي)

3. **قاعدة البيانات**: استخدم SQL Server 2019 أو أحدث

4. **النسخ الاحتياطي**: قم بعمل نسخة احتياطية دورية

---

## 🤝 المساهمة

نرحب بالتحسينات على التوثيق!

إذا وجدت:
- ❌ خطأ في التوثيق
- 💡 فكرة لتحسين
- ➕ معلومة مفقودة

افتح Issue أو Pull Request.

---

## 📞 الدعم

للحصول على المساعدة:
1. راجع [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
2. افتح Issue جديد
3. راجع [README.md](../README.md)

---

**آخر تحديث**: يناير 2025  
**الإصدار**: 1.0.0
