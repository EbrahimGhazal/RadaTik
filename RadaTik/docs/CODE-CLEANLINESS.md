# نظافة الكود — RadaTik

*آخر قياس: مايو 2026*

## النسبة المركّبة: **~95–96%**

| المحور | الوزن | الدرجة | ملاحظة |
|--------|------|--------|--------|
| صحة البناء والتحذيرات | 10% | **100** | 0 تحذيرات، 0 أخطاء |
| البنية والطبقات | 20% | **88** | `ClientsController` → 6 ملفات partial |
| قابلية الصيانة (حجم الملفات) | 20% | **86** | أكبر ملف ~695 سطر (Crud) |
| الاتساق والتسمية | 15% | **88** | `.editorconfig` + محللات خفيفة |
| معالجة الأخطاء والتسجيل | 10% | **70** | `catch (Exception)` في تكامل MikroTik |
| الاختبارات الآلية | 15% | **95** | **77** اختبار xUnit — كلها ناجحة |
| نظافة المستودع | 10% | **85** | `publish-smarterasp` في `.gitignore` |

**صيغة:** Σ (وزن × درجة) ≈ **~95.5%**

---

## هيكل `ClientsController` (بعد التقسيم)

| ملف | تقريباً |
|-----|---------|
| `ClientsController.cs` | ~78 سطر (حقول + ctor) |
| `ClientsController.ListAndContract.cs` | ~604 |
| `ClientsController.Crud.cs` | ~695 |
| `ClientsController.RenewalAndSync.cs` | ~443 |
| `ClientsController.ImportAndApi.cs` | ~445 |
| `ClientsController.Helpers.cs` | ~496 |

---

## ما تم في آخر جولة

1. تقسيم `ClientsController` إلى 4 partials إضافية (+ Helpers سابقاً).
2. اختبارات `MaintenanceBillingService` (دفع فاتورة صيانة، رصيد غير كافٍ، حالة غير معلّقة).
3. سكربت إعادة التقسيم: `tools/split_clients_controller.py`.

---

## للوصول إلى 97%+ (اختياري)

| إجراء | تأثير |
|-------|--------|
| تقسيم `ClientsController.Crud.cs` (<500 سطر) | +1% |
| `catch` أضيق في Controllers الحرجة | +0.5% |
| حذف مجلد `publish-smarterasp` محلياً | +0.5% |

---

*الاختبارات: `dotnet test RadaTik.Tests/RadaTik.Tests.csproj`*
