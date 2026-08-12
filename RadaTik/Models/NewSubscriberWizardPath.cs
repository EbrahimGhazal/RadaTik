namespace RadaTik.Models;

/// <summary>
/// مسار معالج إضافة مشترك جديد.
/// </summary>
public enum NewSubscriberWizardPath
{
    /// <summary>من البرج / السيرفر مباشرة بدون لاقط.</summary>
    TowerDirect = 1,

    /// <summary>لاقط خاص جديد — إضافة لاقط ثم مشترك.</summary>
    PrivateNewReceiver = 2,

    /// <summary>لاقط مشترك — اختيار لاقط موجود (سيرفر + مرسل + اسم).</summary>
    SharedSelectReceiver = 3,

    /// <summary>اختيار لاقط من القائمة (يُحدَّد خاص/مشترك تلقائياً).</summary>
    ExistingReceiverFromList = 4
}
