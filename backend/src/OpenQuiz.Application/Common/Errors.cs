namespace OpenQuiz.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(string errorCode, string message, int statusCode = 400) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

public static class Errors
{
    public static AppException NotFound(string what) => new("not_found", $"{what} not found.", 404);
    public static AppException Forbidden(string msg = "Forbidden.") => new("forbidden", msg, 403);
    public static AppException Unauthorized(string msg = "Unauthorized.") => new("unauthorized", msg, 401);
    public static AppException Conflict(string msg) => new("conflict", msg, 409);
    public static AppException Validation(string msg) => new("validation", msg, 400);
    public static AppException ServiceUnavailable(string msg) => new("service_unavailable", msg, 503);
}
