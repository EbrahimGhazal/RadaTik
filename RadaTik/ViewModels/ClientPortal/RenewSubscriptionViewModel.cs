namespace RadaTik.ViewModels.ClientPortal
{
    public class RenewSubscriptionViewModel
    {
        public int ClientId { get; set; }
        public decimal WalletBalance { get; set; }
        public bool HasBlockingInvoices { get; set; }
        public int BlockingInvoicesCount { get; set; }
        public decimal BlockingInvoicesTotal { get; set; }
        public List<RenewSubscriptionItemViewModel> DueSubscriptions { get; set; } = new();
    }

    public class RenewSubscriptionItemViewModel
    {
        public string SubscriptionName { get; set; } = string.Empty;
        public DateTime? ExpirationDate { get; set; }
        public decimal BasePrice { get; set; }
        public decimal VatPercentage { get; set; }
        public decimal VatAmount { get; set; }
        public decimal AmountDue { get; set; }
        public bool IsExpired { get; set; }
        public bool IsExpiringSoon { get; set; }
        public bool CanRenewFromWallet { get; set; }
        public string SubscriptionStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public bool IsPrimaryInternetSubscription { get; set; }
    }
}
