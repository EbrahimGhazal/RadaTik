# سكريبت تصفير وإعادة إنشاء قاعدة البيانات
# تصفير جميع البيانات: يحذف قاعدة البيانات الحالية ثم يعيد إنشاءها من الهجرات
# لضمان نظافة البيانات وسهولة الإدارة قبل البدء في العمليات
# Reset Database Script - Clear all data and recreate from migrations

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "تصفير وإعادة إنشاء قاعدة البيانات" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# الانتقال إلى مجلد المشروع
$projectPath = ".\RadaTik"
if (-not (Test-Path $projectPath)) {
    Write-Host "✗ خطأ: لم يتم العثور على مجلد المشروع" -ForegroundColor Red
    exit 1
}

Set-Location $projectPath

# حذف قاعدة البيانات باستخدام dotnet ef
Write-Host "جاري حذف قاعدة البيانات..." -ForegroundColor Yellow
try {
    $result = dotnet ef database drop --force 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ تم حذف قاعدة البيانات" -ForegroundColor Green
    } else {
        # قد تكون قاعدة البيانات غير موجودة
        if ($result -match "Cannot open database" -or $result -match "does not exist") {
            Write-Host "⚠ قاعدة البيانات غير موجودة بالفعل" -ForegroundColor Yellow
        } else {
            Write-Host "⚠ تحذير: $result" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "⚠ تحذير: قد تكون قاعدة البيانات غير موجودة" -ForegroundColor Yellow
}

Write-Host ""

# إنشاء قاعدة البيانات من جديد
Write-Host "جاري إنشاء قاعدة البيانات من جديد..." -ForegroundColor Yellow
try {
    dotnet ef database update
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ تم إنشاء قاعدة البيانات بنجاح" -ForegroundColor Green
    } else {
        Write-Host "✗ فشل في إنشاء قاعدة البيانات" -ForegroundColor Red
        Set-Location ".."
        exit 1
    }
} catch {
    Write-Host "✗ خطأ في إنشاء قاعدة البيانات: $($_.Exception.Message)" -ForegroundColor Red
    Set-Location ".."
    exit 1
}

Write-Host ""

# العودة إلى المجلد الرئيسي
Set-Location ".."

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "تم إعادة إنشاء قاعدة البيانات بنجاح!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "بيانات تسجيل الدخول:" -ForegroundColor Yellow
Write-Host "  اسم المستخدم: admin" -ForegroundColor White
Write-Host "  كلمة المرور: 123456" -ForegroundColor White
Write-Host ""
Write-Host "يمكنك الآن تشغيل المشروع باستخدام: dotnet run" -ForegroundColor Cyan
Write-Host ""
