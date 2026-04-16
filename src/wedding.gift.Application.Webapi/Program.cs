using wedding.gift.Crosscutting.Constants;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Extensions;
using wedding.gift.Services.Implementations.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

string formatedVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
builder.Services.AddResponseCompression();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Dependency Injection
string httpClientName = builder.Configuration["GatewayRoutes:HttpClientName"] ?? "Default";

builder.Services.AddDbContext<MicroServiceContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.EnableRetryOnFailure(1)));

builder.Services.AddOptions();

builder.Services.AddMvc(config =>
{
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

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["JwtSettings:Issuer"];
        options.Audience = builder.Configuration["JwtSettings:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

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
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});

builder.Services.AddHttpContextAccessor();

// HttpClients
builder.Services.AddHttpClient(httpClientName)
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Context
builder.Services.AddMicroServiceContext();

// Unit of Work
builder.Services.AddUnitOfWork();

// Repositories
builder.Services.AddRepositories();

// Services
builder.Services.AddServices();

WebApplication app = builder.Build();

// Middlewares
app.UseResponseCompression();

app.UseCors();

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wedding Gift API");
});

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapOpenApi();

app.Run();
