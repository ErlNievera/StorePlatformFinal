using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Store.Observability.Api;

/// <summary>
/// Controller, URL-segment API versioning and per-version Swagger setup shared by every service.
/// Controllers use <c>[ApiVersion("1.0")]</c> and <c>[Route("api/v{version:apiVersion}/...")]</c>.
/// </summary>
public static class StoreApiExtensions
{
    /// <summary>
    /// Adds controllers (enums as strings), API versioning read from the URL segment
    /// (default 1.0, reported in <c>api-supported-versions</c>), the versioned API explorer and a
    /// Swagger document per version with a JWT bearer security scheme.
    /// </summary>
    public static IServiceCollection AddStoreApi(this IServiceCollection services, string title, string description)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddSingleton(new StoreApiInfo(title, description));
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureVersionedSwagger>();
        services.AddSwaggerGen();
        return services;
    }

    /// <summary>Serves <c>/swagger</c> with one document per API version.</summary>
    public static WebApplication UseStoreSwagger(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in app.DescribeApiVersions())
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
            }
        });
        return app;
    }

    private sealed record StoreApiInfo(string Title, string Description);

    private sealed class ConfigureVersionedSwagger(IApiVersionDescriptionProvider provider, StoreApiInfo info)
        : IConfigureOptions<SwaggerGenOptions>
    {
        public void Configure(SwaggerGenOptions options)
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerDoc(description.GroupName, new OpenApiInfo
                {
                    Title = info.Title,
                    Version = description.ApiVersion.ToString(),
                    Description = description.IsDeprecated ? $"{info.Description} This version is deprecated." : info.Description,
                });
            }

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT from POST /identity/v1/auth/login",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });
        }
    }
}
