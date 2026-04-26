using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RadTik.Models;

namespace RadTik.ViewModels.CompanyAdmin
{
    public sealed class CompanyWalletTopUpViewModel
    {
        public int SelectedNetworkId { get; set; }
        public string SelectedNetworkName { get; set; } = "";

        public int EffectiveCompanyNetworkId { get; set; }
        public string EffectiveCompanyNetworkName { get; set; } = "";

        public decimal CompanyBalance { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
        public decimal Amount { get; set; }

        [Display(Name = "طريقة الدفع/التعبئة")]
        public int? PaymentMethodId { get; set; }

        [Display(Name = "طريقة الدفع/التعبئة")]
        [StringLength(200)]
        public string? Method { get; set; }

        [Display(Name = "رقم المرجع/الإيصال")]
        [StringLength(200)]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "صورة الإيصال")]
        public IFormFile? ReceiptImage { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public sealed class CompanyWalletTransactionsViewModel
    {
        public int EffectiveCompanyNetworkId { get; set; }
        public string EffectiveCompanyNetworkName { get; set; } = "";
        public decimal CompanyBalance { get; set; }

        public List<NetworkWalletTransaction> Transactions { get; set; } = [];
    }
}

