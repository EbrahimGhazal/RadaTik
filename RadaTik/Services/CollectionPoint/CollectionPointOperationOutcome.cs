namespace RadaTik.Services.CollectionPoint;

public sealed class CollectionPointOperationOutcome
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public string RedirectAction { get; init; } = "Index";
    public object? RouteValues { get; init; }

    public static CollectionPointOperationOutcome Success(string message, string redirectAction = "Index", object? routeValues = null) =>
        new()
        {
            IsSuccess = true,
            SuccessMessage = message,
            RedirectAction = redirectAction,
            RouteValues = routeValues
        };

    public static CollectionPointOperationOutcome Fail(string message, string redirectAction = "Index", object? routeValues = null) =>
        new()
        {
            ErrorMessage = message,
            RedirectAction = redirectAction,
            RouteValues = routeValues
        };

    public static CollectionPointOperationOutcome NotFoundClient() =>
        new() { NotFound = true };
}
