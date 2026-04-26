-- إضافة عمود العنوان لجدول مستخدمي النظام (نقاط التحصيل)
-- Add Address column to AspNetUsers for collection points display
-- شغّل هذا السكربت على قاعدة البيانات إذا ظهر خطأ: Invalid column name 'Address'

-- التحقق من عدم وجود العمود قبل الإضافة (SQL Server)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.AspNetUsers') 
    AND name = 'Address'
)
BEGIN
    ALTER TABLE dbo.AspNetUsers 
    ADD Address nvarchar(500) NULL;
    PRINT 'تم إضافة عمود Address بنجاح.';
END
ELSE
BEGIN
    PRINT 'عمود Address موجود مسبقاً.';
END
GO
