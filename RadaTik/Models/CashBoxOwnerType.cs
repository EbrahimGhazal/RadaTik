namespace RadaTik.Models
{
    /// <summary>نوع مالك الصندوق النقدي</summary>
    public enum CashBoxOwnerType
    {
        /// <summary>نقطة التحصيل (مرتبط بـ CollectionPointAccount.Id)</summary>
        CollectionPoint = 1,
        /// <summary>الشبكة/الشركة (مرتبط بـ Network.Id)</summary>
        Network = 2,
        /// <summary>مدير النظام (OwnerId = 0)</summary>
        SystemAdmin = 3
    }
}
