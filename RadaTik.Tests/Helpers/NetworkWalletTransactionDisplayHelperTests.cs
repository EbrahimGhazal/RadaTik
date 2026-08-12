using RadaTik.Helpers;

using RadaTik.Models;

using Xunit;



namespace RadaTik.Tests.Helpers;



public sealed class NetworkWalletTransactionDisplayHelperTests

{

    [Fact]

    public void TypeDetails_ServiceCharge_NetworkCreation_IsClearArabic()

    {

        var tx = new NetworkWalletTransaction

        {

            Type = NetworkWalletTransactionType.ServiceCharge,

            Notes = "إنشاء شبكة إضافية: فرع حلب (Networks / OneTime / PerNetwork)"

        };



        string details = NetworkWalletTransactionDisplayHelper.TypeDetails(tx);
        Assert.StartsWith("خصم رسوم إنشاء شبكة فرعية", details);
        Assert.Contains("فرع حلب", details);

        Assert.Contains("فرع حلب", NetworkWalletTransactionDisplayHelper.ReferenceAndNotes(tx));

    }



    [Fact]

    public void ReferenceAndNotes_TopUp_UsesLinkedRequestId()

    {

        var tx = new NetworkWalletTransaction

        {

            Type = NetworkWalletTransactionType.TopUp,

            NetworkTopUpRequestId = 42,

            Notes = "موافقة تغذية رصيد (طلب #42)"

        };



        Assert.Equal("طلب تغذية رقم 42", NetworkWalletTransactionDisplayHelper.ReferenceAndNotes(tx));

    }



    [Fact]

    public void TypeDetails_MaterialPurchase_HasLabelAndInvoiceReference()

    {

        var tx = new NetworkWalletTransaction

        {

            Type = NetworkWalletTransactionType.MaterialPurchasePayment,

            MaterialPurchaseInvoiceId = 9,

            Notes = "دفع فاتورة شراء مواد #9"

        };



        Assert.Equal("دفع مواد", NetworkWalletTransactionDisplayHelper.TypeLabel(tx.Type));

        Assert.Equal("دفع فاتورة شراء مواد من رصيد المحفظة", NetworkWalletTransactionDisplayHelper.TypeDetails(tx));

        Assert.Contains("فاتورة شراء مواد رقم 9", NetworkWalletTransactionDisplayHelper.ReferenceAndNotes(tx));

    }



    [Fact]

    public void TypeDetails_ServiceCharge_FeatureSubscription_IncludesItemAndSubscriptionId()

    {

        var tx = new NetworkWalletTransaction

        {

            Type = NetworkWalletTransactionType.ServiceCharge,

            NetworkServiceSubscriptionId = 9,

            Notes = "خصم عنصر جديد: Users / OneTime / PerUser / U:557d425b-6ad1-43c9-ab38-d7e855b4193d"

        };



        string reason = NetworkWalletTransactionDisplayHelper.TypeDetails(tx);

        Assert.Contains("إدارة الموظفين", reason);

        Assert.DoesNotContain("Users / Daily", reason);

        Assert.Contains("اشتراك خدمة رقم 9", reason);

        Assert.Equal("اشتراك خدمة رقم 9", NetworkWalletTransactionDisplayHelper.ReferenceAndNotes(tx));

    }

}


