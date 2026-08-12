namespace RadaTik.Data;

/// <summary>نطاق الشبكات المتاحة للطلب الحالي (عزل بيانات متعدد المستأجرين).</summary>
public interface ICurrentNetworkScope
{
    bool IsFilterActive { get; }

    /// <summary>عند true لا يُطبَّق فلتر الشبكة (مدير نظام أو خلفية).</summary>
    bool BypassAllNetworks { get; }

    IReadOnlyList<int> AccessibleNetworkIds { get; }

    void SetScope(bool isFilterActive, bool bypassAllNetworks, IReadOnlyList<int> accessibleNetworkIds);

    void Reset();
}
