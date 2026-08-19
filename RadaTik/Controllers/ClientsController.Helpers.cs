using Microsoft.AspNetCore.Mvc;

using RadaTik.Helpers;

using RadaTik.Services.Clients;

using RadaTik.Services.MikroTik;



namespace RadaTik.Controllers

{

    public partial class ClientsController : Controller

    {

        private void ApplyCreateFormViewData(ClientCreateFormViewData data)

        {

            ViewData["ReceiverId"] = data.ReceiverId;

            ViewData["MikroTikServerId"] = data.MikroTikServerId;

            ViewData["ProfileId"] = data.ProfileId;



            ClientCreatePricingViewData pricing = data.Pricing;

            ViewBag.ClientCreateChargeHasPricing = pricing.HasPricing;

            ViewBag.ClientCreateChargeAmount = pricing.ChargeAmount;

            ViewBag.ClientCreateSubscriberChargeAmount = pricing.SubscriberChargeAmount;

            ViewBag.ClientCreateUserChargeAmount = pricing.UserChargeAmount;

            ViewBag.ClientCreateChargeWalletBalance = pricing.ChargeWalletBalance;

            ViewBag.ClientCreateInitialPrice = pricing.InitialPrice;

            ViewBag.ClientCreateRenewalPrice = pricing.RenewalPrice;

            ViewBag.ClientCreateRenewalPeriodLabel = pricing.RenewalPeriodLabel;

            ViewBag.ClientCreateHasRenewalPricing = pricing.HasRenewalPricing;

        }



        private void ApplyEditFormViewData(ClientEditFormViewData data)

        {

            ViewData["ReceiverId"] = data.ReceiverId;

            ViewData["MikroTikServerId"] = data.MikroTikServerId;

            ViewData["ProfileId"] = data.ProfileId;

        }



        private void ApplyContractPrintViewData(ClientContractPrintViewData data)

        {

            ViewBag.ContractDate = data.ContractDate;

            ViewBag.ContractTitle = data.ContractTitle;

            ViewBag.ContractRecordNumber = data.RecordNumber;

            ViewBag.ContractLicenseNumber = data.LicenseNumber;

            ViewBag.ContractBodyHtml = data.BodyHtml;

        }



        private void ApplyContractTemplateSettingsViewData(ClientContractTemplateSettingsViewData data)

        {

            ViewBag.AvailableVariables = data.AvailableVariables;

            ViewBag.VariableSyntaxHint = data.VariableSyntaxHint;

            ViewBag.PreviewHtml = data.PreviewHtml;

            ViewBag.ContractTitle = data.ContractTitle;

            ViewBag.RecordNumber = data.RecordNumber;

            ViewBag.LicenseNumber = data.LicenseNumber;

            ViewBag.ContractBodyTemplate = data.ContractBodyTemplate;

            ViewBag.DefaultContractBodyTemplate = data.DefaultContractBodyTemplate;

        }



        private static string BuildFriendlyMikroTikErrorMessage(string prefix, Exception ex) =>
            MikroTikErrorFormatter.Format(prefix, ex);

        private static string BuildFriendlyMikroTikErrorMessage(string prefix, string? rawMessage) =>
            MikroTikErrorFormatter.Format(prefix, rawMessage);

        private void ApplyClientImportOutcome(ClientImportOutcome outcome)
        {
            if (outcome.Success)
            {
                TempData["Success"] = $"✅ {outcome.SuccessMessage}";
                if (!string.IsNullOrEmpty(outcome.Warnings))
                {
                    TempData["ImportWarnings"] = outcome.Warnings;
                }

                if (!string.IsNullOrEmpty(outcome.FailedUsersJson))
                {
                    TempData["ImportFailedUsersDetails"] = outcome.FailedUsersJson;
                }

                return;
            }

            if (outcome.Skipped)
            {
                TempData["Info"] = outcome.ErrorMessage ?? "تم تخطي السيرفر والمتابعة.";
                return;
            }

            string message = outcome.ErrorMessage ?? "فشل الاستيراد";
            TempData["Error"] = message.StartsWith('❌') || message.StartsWith('?') ? message : $"❌ {message}";
        }

        private IActionResult ApplyWalletTopUpOutcome(ClientWalletTopUpOutcome outcome, int clientId)
        {
            if (outcome.NotFound)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(outcome.ErrorMessage))
            {
                TempData["Error"] = outcome.ErrorMessage.StartsWith('?')
                    ? outcome.ErrorMessage
                    : outcome.ErrorMessage;
            }

            if (outcome.IsSuccess && !string.IsNullOrEmpty(outcome.SuccessMessage))
            {
                TempData["Success"] = outcome.SuccessMessage;
            }

            return RedirectToAction(nameof(Details), new { id = clientId });
        }

        private IActionResult ApplyClientOperationOutcome(

            ClientOperationOutcome outcome,

            string redirectAction,

            object? routeValues = null)

        {

            if (outcome.NotFound)

            {

                return NotFound();

            }



            if (!string.IsNullOrEmpty(outcome.ErrorMessage))
            {
                TempData["Error"] = outcome.ErrorMessage;
            }

            if (outcome.IsSuccess && !string.IsNullOrEmpty(outcome.SuccessMessage))
            {
                TempData["Success"] = outcome.SuccessMessage;
            }



            string? referer = Request.Headers.Referer.ToString();

            if (!string.IsNullOrWhiteSpace(referer) && redirectAction != nameof(Index))

            {

                return Redirect(referer);

            }



            return routeValues == null

                ? RedirectToAction(redirectAction)

                : RedirectToAction(redirectAction, routeValues);

        }

    }

}


