using Microsoft.OpenApi;
using RaceDay.Api.Auth;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;

namespace RaceDay.Api.Swagger;

/// <summary>
/// Adds an "Access" line to every endpoint's description in the Swagger UI (public / any logged-in
/// user / a specific role) and documents the 401 and 403 responses of protected endpoints.
/// </summary>
public class SessionAuthOperationFilter : IOperationFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiOperation operation, OperationFilterContext context)
    {
        // Guard against nulls: context.MethodInfo or operation can be null for some generated operations.
        if (context?.MethodInfo == null || operation == null)
        {
            return;
        }

        var attributes = context.MethodInfo.GetCustomAttributes(true).OfType<SessionAuthorizeAttribute>().ToList();
        if (context.MethodInfo.DeclaringType != null)
        {
            attributes.AddRange(context.MethodInfo.DeclaringType.GetCustomAttributes(true).OfType<SessionAuthorizeAttribute>());
        }

        string access;
        if (attributes.Count == 0)
        {
            access = "**Access:** Public - no login required.";
        }
        else
        {
            var roles = attributes.Select(a => a.Roles).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            access = roles is null
                ? "**Access:** Any logged-in user (Organiser or Participant). Log in first via `POST /api/auth/login`."
                : $"**Access:** {roles} only. Log in first via `POST /api/auth/login`.";

            // Ensure Responses collection is initialized to avoid possible null dereference
            operation.Responses ??= new Microsoft.OpenApi.OpenApiResponses();

            operation.Responses.TryAdd("401", new Microsoft.OpenApi.OpenApiResponse { Description = "Not logged in (no valid session)." });
            operation.Responses.TryAdd("403", new Microsoft.OpenApi.OpenApiResponse { Description = "Logged in, but your role or ownership does not permit this action." });
        }

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? access
            : access + "\n\n" + operation.Description;
    }
}
