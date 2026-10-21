using System.Net;

namespace FindMyFamily.Core.Exceptions;

public abstract class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Code { get; }

    protected AppException(string code, string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}
