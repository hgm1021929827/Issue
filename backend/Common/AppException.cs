namespace Issue.Api.Common;

public class AppException(int statusCode, string message, object? data = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public new object? Data { get; } = data;
}
