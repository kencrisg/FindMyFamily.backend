namespace FindMyFamily.Api.Common;

public record ErrorResponse(
    bool Error,
    string Code,
    string Message
);
