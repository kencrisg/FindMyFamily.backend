using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FindMyFamily.Api.Filters;

public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true
            || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        var hasAllowAnonymous = context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true
            || context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        if (hasAuthorize && !hasAllowAnonymous)
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "No autorizado (Falta o token JWT inválido)" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Acceso prohibido (Rol insuficiente)" });

            operation.Security ??= new List<OpenApiSecurityRequirement>();

            var schemeRef = new OpenApiSecuritySchemeReference("Bearer", context.Document);
            var req = new OpenApiSecurityRequirement
            {
                [schemeRef] = new List<string>()
            };

            operation.Security.Add(req);
        }
    }
}
