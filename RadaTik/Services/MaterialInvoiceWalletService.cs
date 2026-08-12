using Microsoft.EntityFrameworkCore;

using RadaTik.Data;

using RadaTik.Helpers;

using RadaTik.Models;

using RadaTik.Models.Business;



namespace RadaTik.Services;



public sealed class MaterialInvoiceWalletResult

{

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public int? WalletTransactionId { get; init; }



    public static MaterialInvoiceWalletResult Ok(int txId) => new() { Success = true, WalletTransactionId = txId };

    public static MaterialInvoiceWalletResult Fail(string message) => new() { Success = false, ErrorMessage = message };

    public static MaterialInvoiceWalletResult Skipped() => new() { Success = true };

}



/// <summary>ربط اختياري بمحفظة الشركة عند تسجيل الدفع (ل.س.ج أو $).</summary>

public sealed class MaterialInvoiceWalletService(ApplicationDbContext context)

{

    private readonly ApplicationDbContext _context = context;



    public Task<MaterialInvoiceWalletResult> ApplyPurchasePaymentAsync(

      int companyNetworkId,

      int purchaseInvoiceId,

      decimal amount,

      string userId,

      CancellationToken ct = default) =>

      ApplyPurchasePaymentAsync(companyNetworkId, purchaseInvoiceId, amount, PricingCurrency.SYP_New, userId, ct);



    public async Task<MaterialInvoiceWalletResult> ApplyPurchasePaymentAsync(

      int companyNetworkId,

      int purchaseInvoiceId,

      decimal amount,

      PricingCurrency currency,

      string userId,

      CancellationToken ct = default)

    {

        if (amount <= 0m)

        {

            return MaterialInvoiceWalletResult.Skipped();

        }



        return await ApplySignedAsync(

          companyNetworkId,

          -amount,

          currency,

          NetworkWalletTransactionType.MaterialPurchasePayment,

          purchaseInvoiceId: purchaseInvoiceId,

          salesInvoiceId: null,

          userId,

          $"دفع فاتورة شراء مواد #{purchaseInvoiceId}",

          ct);

    }



    public Task<MaterialInvoiceWalletResult> RefundPurchasePaymentAsync(

      int companyNetworkId,

      int purchaseInvoiceId,

      decimal amount,

      string userId,

      CancellationToken ct = default) =>

      RefundPurchasePaymentAsync(companyNetworkId, purchaseInvoiceId, amount, PricingCurrency.SYP_New, userId, ct);



    public async Task<MaterialInvoiceWalletResult> RefundPurchasePaymentAsync(

      int companyNetworkId,

      int purchaseInvoiceId,

      decimal amount,

      PricingCurrency currency,

      string userId,

      CancellationToken ct = default)

    {

        if (amount <= 0m)

        {

            return MaterialInvoiceWalletResult.Skipped();

        }



        return await ApplySignedAsync(

          companyNetworkId,

          amount,

          currency,

          NetworkWalletTransactionType.MaterialPurchaseRefund,

          purchaseInvoiceId: purchaseInvoiceId,

          salesInvoiceId: null,

          userId,

          $"استرداد — إلغاء/تعديل فاتورة شراء #{purchaseInvoiceId}",

          ct);

    }



    public Task<MaterialInvoiceWalletResult> ApplySaleReceiptAsync(

      int companyNetworkId,

      int salesInvoiceId,

      decimal amount,

      string userId,

      CancellationToken ct = default) =>

      ApplySaleReceiptAsync(companyNetworkId, salesInvoiceId, amount, PricingCurrency.SYP_New, userId, ct);



    public async Task<MaterialInvoiceWalletResult> ApplySaleReceiptAsync(

      int companyNetworkId,

      int salesInvoiceId,

      decimal amount,

      PricingCurrency currency,

      string userId,

      CancellationToken ct = default)

    {

        if (amount <= 0m)

        {

            return MaterialInvoiceWalletResult.Skipped();

        }



        return await ApplySignedAsync(

          companyNetworkId,

          amount,

          currency,

          NetworkWalletTransactionType.MaterialSaleReceipt,

          purchaseInvoiceId: null,

          salesInvoiceId: salesInvoiceId,

          userId,

          $"تحصيل فاتورة بيع مواد #{salesInvoiceId}",

          ct);

    }



    public Task<MaterialInvoiceWalletResult> RefundSaleReceiptAsync(

      int companyNetworkId,

      int salesInvoiceId,

      decimal amount,

      string userId,

      CancellationToken ct = default) =>

      RefundSaleReceiptAsync(companyNetworkId, salesInvoiceId, amount, PricingCurrency.SYP_New, userId, ct);



    public async Task<MaterialInvoiceWalletResult> RefundSaleReceiptAsync(

      int companyNetworkId,

      int salesInvoiceId,

      decimal amount,

      PricingCurrency currency,

      string userId,

      CancellationToken ct = default)

    {

        if (amount <= 0m)

        {

            return MaterialInvoiceWalletResult.Skipped();

        }



        if (amount > 0m)

        {

            Network? network = await _context.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);

            if (network != null && !CompanyWalletHelper.HasSufficientBalance(network, currency, amount))

            {

                return MaterialInvoiceWalletResult.Fail(

                  CompanyWalletHelper.FormatInsufficientBalanceMessage(network, currency, amount));

            }

        }



        return await ApplySignedAsync(

          companyNetworkId,

          -amount,

          currency,

          NetworkWalletTransactionType.MaterialSaleRefund,

          purchaseInvoiceId: null,

          salesInvoiceId: salesInvoiceId,

          userId,

          $"عكس تحصيل — إلغاء/تعديل فاتورة بيع #{salesInvoiceId}",

          ct);

    }



    private async Task<MaterialInvoiceWalletResult> ApplySignedAsync(

      int companyNetworkId,

      decimal signedAmount,

      PricingCurrency currency,

      NetworkWalletTransactionType type,

      int? purchaseInvoiceId,

      int? salesInvoiceId,

      string userId,

      string notes,

      CancellationToken ct)

    {

        Network? network = await _context.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);

        if (network == null)

        {

            return MaterialInvoiceWalletResult.Fail("الشركة غير موجودة.");

        }



        decimal debitAmount = signedAmount < 0m ? -signedAmount : 0m;

        if (debitAmount > 0m && !CompanyWalletHelper.HasSufficientBalance(network, currency, debitAmount))

        {

            return MaterialInvoiceWalletResult.Fail(

              CompanyWalletHelper.FormatInsufficientBalanceMessage(network, currency, debitAmount));

        }



        decimal previous = CompanyWalletHelper.GetBalance(network, currency);

        CompanyWalletHelper.ApplyDelta(network, currency, signedAmount);

        decimal newBalance = CompanyWalletHelper.GetBalance(network, currency);



        NetworkWalletTransaction tx = new()

        {

            NetworkId = companyNetworkId,

            Type = type,

            Currency = currency,

            SignedAmount = signedAmount,

            PreviousBalance = previous,

            NewBalance = newBalance,

            MaterialPurchaseInvoiceId = purchaseInvoiceId,

            MaterialSalesInvoiceId = salesInvoiceId,

            CreatedByUserId = userId,

            CreatedAt = DateTime.Now,

            Notes = notes

        };

        _context.NetworkWalletTransactions.Add(tx);

        await _context.SaveChangesAsync(ct);

        return MaterialInvoiceWalletResult.Ok(tx.Id);

    }

}


