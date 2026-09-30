using System.Net;

namespace FindMyFamily.Core.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(code, message, HttpStatusCode.NotFound)
    {
    }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message, string code = "BAD_REQUEST")
        : base(code, message, HttpStatusCode.BadRequest)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "No autorizado", string code = "UNAUTHORIZED")
        : base(code, message, HttpStatusCode.Unauthorized)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Acceso denegado", string code = "FORBIDDEN")
        : base(code, message, HttpStatusCode.Forbidden)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message, string code = "CONFLICT")
        : base(code, message, HttpStatusCode.Conflict)
    {
    }
}
