@echo off
chcp 65001 >nul
echo ========================================
echo إعادة إنشاء قاعدة البيانات
echo ========================================
echo.

cd RadaTik

echo جاري حذف قاعدة البيانات...
dotnet ef database drop --force
if %ERRORLEVEL% NEQ 0 (
    echo تحذير: قد تكون قاعدة البيانات غير موجودة
)
echo.

echo جاري إنشاء قاعدة البيانات من جديد...
dotnet ef database update
if %ERRORLEVEL% NEQ 0 (
    echo خطأ: فشل في إنشاء قاعدة البيانات
    cd ..
    pause
    exit /b 1
)

cd ..

echo.
echo ========================================
echo تم إعادة إنشاء قاعدة البيانات بنجاح!
echo ========================================
echo.
echo بيانات تسجيل الدخول:
echo   اسم المستخدم: admin
echo   كلمة المرور: 123456
echo.
echo يمكنك الآن تشغيل المشروع
echo.
pause
