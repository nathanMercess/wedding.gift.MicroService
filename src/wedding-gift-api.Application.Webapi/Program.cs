using InfluencyMe.Framework.Crosscutting;
using InfluencyMe.Framework.Crosscutting.Extensions;
using InfluencyMe.Framework.Crosscutting.Models;
using InfluencyMe.Framework.Crosscutting.MVCFilters;
using InfluencyMe.Framework.Crosscutting.Security.Extensions;
using InfluencyMe.Framework.OpenTelemetry.Enums;
using InfluencyMe.Framework.OpenTelemetry.Extensions;
using wedding-gift-api.Crosscutting.Constants;
using wedding-gift-api.Infra.Implementations.DataContext;
using wedding-gift-api.Infra.Implementations.Extensions;
using wedding-gift-api.Services.Implementations.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

EnvironmentConfig envConfig = new EnvironmentConfig(ApplicationConstants.ENDPOINT_APPLICATION, builder.Environment);

builder.Configuration
    .SetBasePath(envConfig.ConfigDirectory)
    .AddJsonFile("hosting.json", optional: true, reloadOnChange: false)
    .AddJsonFile(envConfig.SettingsFile, optional: true, reloadOnChange: false)
    .AddJsonFile(envConfig.SettingsEnvironmentFile, optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

string formatedVersion = Assembly.GetEntryAssembly().GetName().Version.GetFormatedVersion();

builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
builder.Services.AddResponseCompression();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Dependency Injection
GatewayRoutes routes = builder.Services.ConfigurationAddSingleton<GatewayRoutes>(builder.Configuration);

builder.Services.AddDbContext<MicroServiceContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.EnableRetryOnFailure(1)));

builder.Services.AddOptions();

builder.Services.AddMvc(config =>
{
    config.Filters.Add<CustomExceptionFilter>();
    config.Filters.Add<OperationCancelledExceptionFilter>();
})
.AddJsonOptions(jsonOptions =>
{
    jsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    jsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddAuthorization(auth =>
{
    auth.AddPolicy("Bearer", new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build());
});

builder.Services.AddAuthenticationWithJwtBearer(builder.Configuration, builder.Environment);

builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = ApplicationConstants.APPLICATION_NAME, Version = formatedVersion });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.CustomSchemaIds(x => x.FullName);

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
    });
});

builder.Services.AddHttpContextAccessor();

// HttpClients
builder.Services.AddHttpClient(routes.HttpClientName)
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Context
builder.Services.AddMicroServiceContext();

// Unit of Work
builder.Services.AddUnitOfWork();

// Repositories
builder.Services.AddRepositories();

// Services
builder.Services.AddServices();

// OpenTelemetry
builder.Services.AddOpenTelemetry(
    builder.Configuration,
    ApplicationConstants.APPLICATION_NAME,
    formatedVersion,
    [OpenTelemetryExporter.GrafanaCloud]);

WebApplication app = builder.Build();

// Middlewares
app.UseResponseCompression();

app.UseCors();

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InfluencyMe Seed API");
});

app.UseCustomResponseMiddleware();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapOpenApi();

app.Run();