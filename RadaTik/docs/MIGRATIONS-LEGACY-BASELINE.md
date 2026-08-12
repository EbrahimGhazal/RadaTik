# الهجرات التاريخية وخط الأساس (Baseline)

## الوضع الحالي

المشروع يحتوي على سلسلة هجرات EF متراكمة منذ التطوير المبكر، بما فيها أسماء تجريبية (`test*`, `Repair*`). **لا يُنصح بحذفها** من مستودعات الإنتاج التي طبّقتها بالفعل.

## خط الأساس الموصى به عند إطلاق إنتاج مستقر

1. أخذ نسخة احتياطية كاملة من قاعدة البيانات.
2. على بيئة جديدة فقط: إنشاء baseline واحد:
   ```bash
   dotnet ef migrations add Baseline_Production --project RadaTik
   ```
   ثم أرشفة مجلد `Migrations/` القديم في `docs/migrations-archive/` (مرجع تاريخي فقط).
3. تسجيل الهجرات في `__EFMigrationsHistory` يدوياً أو عبر `dotnet ef database update` على البيئة الجديدة.

## الهجرات الحديثة (معيارية)

| الملف | الغرض |
|-------|--------|
| `20260524120000_AddBalanceRowVersionColumns` | RowVersion للأرصدة |
| `20260521130000_DatabaseDesignCompletion` | محفظة مدير النظام + عرض الدفتر الموحّد |

راجع `MIGRATIONS-GUIDE.md` لقواعد التسمية الجديدة.
