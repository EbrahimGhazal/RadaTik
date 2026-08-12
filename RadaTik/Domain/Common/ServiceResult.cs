namespace RadaTik.Domain.Common;

/// <summary>نتيجة عملية تطبيقية موحّدة (تغليف + تجريد للأخطاء).</summary>
public class ServiceResult
{
    public bool IsSuccess { get; protected init; }
    public string? ErrorMessage { get; protected init; }

    public static ServiceResult Ok() => new() { IsSuccess = true };

    public static ServiceResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };

    public static ServiceResult<T> Ok<T>(T value) => ServiceResult<T>.Ok(value);

    public static ServiceResult<T> Fail<T>(string message) => ServiceResult<T>.Fail(message);
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Ok(T value) =>
        new() { IsSuccess = true, Value = value };

    public new static ServiceResult<T> Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
