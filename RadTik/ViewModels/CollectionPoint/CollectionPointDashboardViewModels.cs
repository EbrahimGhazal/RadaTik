using System.ComponentModel.DataAnnotations;
using RadTik.Models;

namespace RadTik.ViewModels.CollectionPoint
{
    public class CollectionPointDashboardViewModel
    {
        public string? Query { get; set; }
        public int? NetworkId { get; set; }
        public string? NetworkName { get; set; }
        /// <summary>اسم الشركة (الشبكة الرئيسية إن وُجدت)</summary>
        public string? CompanyName { get; set; }
        public decimal AccountBalance { get; set; }
        public List<Client> Clients { get; set; } = [];
        public List<PaymentTransaction> RecentTransactions { get; set; } = [];
        /// <summary>قائمة الشبكات المتاحة لاختيار واحدة (لنقطة التحصيل)</summary>
        public List<RadTik.Models.Network>? AvailableNetworks { get; set; }
        /// <summary>جميع الشبكات النشطة (للعرض ككروت)</summary>
        public List<NetworkCardItem> Networks { get; set; } = [];
    }

    /// <summary>عنصر كرت شبكة للواجهة الرئيسية</summary>
    public class NetworkCardItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? LogoPath { get; set; }
        public string? Phone { get; set; }
    }

    /// <summary>نتيجة بحث مشترك لتسديد الفاتورة</summary>
    public class ClientSearchResultItem
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string SubscriberNumber { get; set; } = "";
        public string NetworkName { get; set; } = "";
        public string? PhoneNumber { get; set; }
    public string ProfileName { get; set; } = "";
    public decimal BasePrice { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal TotalPrice { get; set; }
    public int PendingMonths { get; set; }
    public decimal TotalAmountDue { get; set; }
        public decimal ProfileDownloadSpeed { get; set; }
        public string ProfileDownloadSpeedDisplay { get; set; } = "";
    }

    /// <summary>صفحة تفاصيل العميل من نقطة التحصيل: الشبكة، الباقات، الباقة الحالية، المبلغ الواجب، تسديد</summary>
    public class ClientDetailsForCollectionPointViewModel
    {
        public int ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientUserName { get; set; }
        public string? PhoneNumber { get; set; }
    public string? ResidenceAddress { get; set; }
    public decimal ClientBalance { get; set; }
        public int NetworkId { get; set; }
        public string? NetworkName { get; set; }
        public string? CompanyName { get; set; }
        public int CurrentProfileId { get; set; }
        public string? CurrentProfileName { get; set; }
        public decimal CurrentProfilePrice { get; set; }
    public decimal CurrentBasePrice { get; set; }
    public decimal CurrentCommissionAmount { get; set; }
        /// <summary>أسعار الباقات المحددة من مدير الشبكة (البروفايلات المتاحة لهذه الشبكة)</summary>
        public List<ProfilePriceItem> ProfilePrices { get; set; } = [];
        /// <summary>المبلغ الواجب دفعه (عادة سعر الباقة الحالية)</summary>
        public decimal AmountDue { get; set; }
        public decimal CollectionPointBalance { get; set; }
        public DateTime? AccountExpirationDate { get; set; }
    }

    public class ProfilePriceItem
    {
        public int ProfileId { get; set; }
        public string Name { get; set; } = null!;
    public string SpeedDisplay { get; set; } = "";
    public string DataLimitDisplay { get; set; } = "";
        public decimal Price { get; set; }
    public decimal CommissionAmount { get; set; }
        public decimal PriceWithVAT { get; set; }
    }

    public class ReceivePaymentViewModel
    {
        [Required]
        public int ClientId { get; set; }

        public string? ClientName { get; set; }
        public string? ClientUserName { get; set; }

        [Display(Name = "رصيد العميل الحالي")]
        public decimal CurrentClientBalance { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ المستلم")]
        [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
        public decimal Amount { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }
}

