using RadaTik.ViewModels.CollectionPoint;

namespace RadaTik.Services.CollectionPoint;

public sealed record ReceivePaymentCommand(
    int ClientId,
    decimal Amount,
    decimal? ExchangeRate,
    string? Notes,
    string UserId,
    int NetworkId);

public sealed class ReceivePaymentOutcome
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public bool ReturnView { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public string? RedirectSearchQuery { get; init; }
    public ReceivePaymentViewModel? ViewModel { get; init; }

    public static ReceivePaymentOutcome Success(string message, string redirectSearchQuery) =>
        new()
        {
            IsSuccess = true,
            SuccessMessage = message,
            RedirectSearchQuery = redirectSearchQuery
        };

    public static ReceivePaymentOutcome NotFoundClient() =>
        new() { NotFound = true };

    public static ReceivePaymentOutcome ViewError(string message, ReceivePaymentViewModel model) =>
        new()
        {
            ReturnView = true,
            ErrorMessage = message,
            ViewModel = model
        };
}

public interface ICollectionPointReceivePaymentService
{
    Task<ReceivePaymentOutcome> ProcessAsync(ReceivePaymentCommand command, CancellationToken ct = default);
}
