using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Midnight.ApiServer.Core.Security;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>Title and bearer security scheme of the "application" OpenAPI document</summary>
public class ApplicationOpenApiTransformer : IOpenApiDocumentTransformer
{
    public const string SecuritySchemeName = "ApplicationKey";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Midnight Application API";
        document.Info.Description =
            "Automation api for billing systems and other integrations. Authenticate with an application key " +
            "(Admin → Application API) as \"Authorization: Bearer mda_{identifier}_{secret}\". Every key has an access level " +
            $"(None, Read, ReadWrite) per resource, missing access is answered with 403. Rate limit: {ApplicationKeyRateLimitInfo} requests per minute and key.";

        document.AddComponent<IOpenApiSecurityScheme>(SecuritySchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = ApplicationKeyAuthenticationHandler.KeyPrefix + "{identifier}_{secret}",
            Description = "Application key created under Admin → Application API"
        });

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SecuritySchemeName, document)] = []
        });

        return Task.CompletedTask;
    }

    private const string ApplicationKeyRateLimitInfo = "240 (configurable with Api.ApplicationRateLimitPerMinute)";
}
