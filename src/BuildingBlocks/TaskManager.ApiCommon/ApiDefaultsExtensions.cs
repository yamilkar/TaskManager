using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.SimpleSystemsManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;
using TaskManager.SharedKernel;
using TaskManager.SharedKernel.Security;

namespace TaskManager.ApiCommon;

public static class ApiDefaultsExtensions
{
    public const string CorsPolicyName = "web";
    public const string ReadyTag = "ready";

    public static WebApplicationBuilder AddTaskManagerApiDefaults(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        builder.Configuration.AddFromSsm(
            () => new AmazonSimpleSystemsManagementClient(), SsmConfiguration.JwtSigningKeyParameterNameKey, "Jwt:SigningKey");

        builder.Host.UseSerilog((context, _, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                o.InvalidModelStateResponseFactory = context =>
                {
                    var details = new ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "One or more validation errors occurred.",
                    };
                    details.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                    return new BadRequestObjectResult(details)
                    {
                        ContentTypes = { "application/problem+json" },
                    };
                };
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .Validate(o =>
            {
                try
                {
                    o.EnsureValid();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }, "Jwt:SigningKey must be at least 32 bytes.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => JwtBearerSetup.Configure(bearer, jwt.Value));
        services.AddAuthorization();

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy(CorsPolicyName, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins);
            }

            policy.AllowAnyHeader().AllowAnyMethod();
        }));

        services.AddOpenApi();
        services.AddHealthChecks();
        services.AddSingleton<IAppMetrics, EmfAppMetrics>();
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

        return builder;
    }

    /// <param name="beforeEndpoints">
    /// Optional hook that runs after authentication/authorization and before endpoints are mapped,
    /// e.g. to add <c>UseRateLimiter()</c>.
    /// </param>
    public static WebApplication UseTaskManagerApiDefaults(this WebApplication app, Action<WebApplication>? beforeEndpoints = null)
    {
        app.UseConfiguredForwardedHeaders(app.Configuration);
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.UseCors(CorsPolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        beforeEndpoints?.Invoke(app);

        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
