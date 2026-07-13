using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Polly;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using wedding.gift.Application.Webapi.Infrastructure;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Extensions;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;
using wedding.gift.Services.Implementations.Security;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection é obrigatória.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

CorsOptions corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });

    options.AddPolicy("AngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4300")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new RouteConvention("api"));
    })
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });

builder.Services.Configure<ApiBehaviorOptions>(options => options.UseValidationApiResponse());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Jwt:Issuer é obrigatório.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Jwt:Audience é obrigatório.")
    .Validate(x => x.SigningKey?.Length >= 32, "Jwt:SigningKey deve ter ao menos 32 caracteres.")
    .Validate(x => x.AccessTokenExpirationMinutes is > 0 and <= 1440, "Expiração JWT inválida.")
    .ValidateOnStart();
OptionsBuilder<SmtpOptions> smtpOptionsBuilder = builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName));

if (builder.Environment.IsProduction())
{
    smtpOptionsBuilder
        .Validate(x => !string.IsNullOrWhiteSpace(x.Host), "Smtp:Host é obrigatório em produção.")
        .Validate(x => !string.IsNullOrWhiteSpace(x.FromEmail), "Smtp:FromEmail é obrigatório em produção.")
        .ValidateOnStart();
}
builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Api:BaseUrl inválida.")
    .ValidateOnStart();
builder.Services.AddOptions<GcsOptions>()
    .Bind(builder.Configuration.GetSection(GcsOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.BucketName), "Gcs:BucketName é obrigatório.")
    .ValidateOnStart();
builder.Services.AddOptions<MercadoPagoOptions>()
    .Bind(builder.Configuration.GetSection(MercadoPagoOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwtOptions.SigningKey?.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey deve ter ao menos 32 caracteres.");
byte[] signingKey = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                string? userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                      context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                if (!Guid.TryParse(userIdValue, out Guid userId))
                {
                    context.Fail("Token inválido.");
                    return;
                }

                IUserRepository repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                User? user = await repository.GetByIdAsync(userId, context.HttpContext.RequestAborted);

                if (user is null || !user.IsActive)
                {
                    context.Fail("Usuário inativo.");
                    return;
                }

                string? tokenRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

                if (!string.Equals(tokenRole, user.Role, StringComparison.Ordinal))
                    context.Fail("Permissões do usuário foram alteradas.");
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 3;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(_ => StorageClient.Create());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 200,
            QueueLimit = 0
        }));
    options.AddPolicy("auth", context => CreateRateLimitPartition(context, 10, TimeSpan.FromMinutes(1)));
    options.AddPolicy("payment", context => CreateRateLimitPartition(context, 120, TimeSpan.FromMinutes(5)));
    options.AddPolicy("payment-polling", context => CreatePaymentPollingRateLimitPartition(context, 120, TimeSpan.FromMinutes(5)));
    options.AddPolicy("public-write", context => CreateRateLimitPartition(context, 20, TimeSpan.FromMinutes(5)));
    options.AddPolicy("webhook", context => CreateRateLimitPartition(context, 120, TimeSpan.FromMinutes(1)));
    options.AddPolicy("order-lookup", context => CreateRateLimitPartition(context, 10, TimeSpan.FromMinutes(15)));
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services
    .AddHttpClient<IMercadoPagoService, MercadoPagoService>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(
        3, retryAttempt => TimeSpan.FromMilliseconds(300 * Math.Pow(2, retryAttempt))))
    .AddPolicyHandler(Policy.HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddSingleton<wedding.gift.Services.Contracts.IBackgroundTaskQueue, wedding.gift.Application.Webapi.Infrastructure.BackgroundTaskQueue>();
builder.Services.AddHostedService<wedding.gift.Application.Webapi.Infrastructure.QueuedHostedService>();
builder.Services.AddHostedService<ApiRequestLogCleanupHostedService>();
builder.Services.AddHostedService<PaymentReconciliationHostedService>();
builder.Services.AddHostedService<EmailOutboxHostedService>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "wedding.gift API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});

builder.Services.AddRepositories();
builder.Services.AddServices();

WebApplication app = builder.Build();

await ApplyMigrationsAsync(app.Services);
await EnsureBootstrapAdminAsync(app.Services, builder.Configuration);

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseGlobalExceptionHandler();
app.UseMiddleware<ApiResponseMiddleware>();
app.UseMiddleware<CacheInvalidationMiddleware>();
app.UseCors("AllowedOrigins");
app.UseRateLimiter();
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseMiddleware<ApiRequestLoggingMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new() { Predicate = registration => registration.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new() { Predicate = registration => registration.Tags.Contains("ready") });
app.MapControllers();

app.Run();

static async Task ApplyMigrationsAsync(IServiceProvider services)
{
    using IServiceScope scope = services.CreateScope();
    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (dbContext.Database.IsRelational())
        await dbContext.Database.MigrateAsync();
}

static async Task EnsureBootstrapAdminAsync(IServiceProvider services, IConfiguration configuration)
{
    BootstrapAdminOptions options = configuration.GetSection(BootstrapAdminOptions.SectionName).Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions();

    if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        return;

    using IServiceScope scope = services.CreateScope();
    IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

    string normalizedEmail = options.Email.Trim().ToLowerInvariant();
    string role = GetBootstrapAdminRole(options.Role);
    bool exists = await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, CancellationToken.None);

    if (exists)
        return;

    (string hash, string salt) = PasswordHasher.HashPassword(options.Password);

    User user = User.Create(
        string.IsNullOrWhiteSpace(options.Name) ? "Administrador" : options.Name.Trim(),
        options.Email,
        normalizedEmail,
        hash,
        salt,
        role,
        true,
        null,
        null);

    await userRepository.AddAsync(user, CancellationToken.None);
    await userRepository.SaveChangesAsync(CancellationToken.None);
}

static string GetBootstrapAdminRole(string role)
{
    if (string.IsNullOrWhiteSpace(role))
        return UserRoles.Admin;

    string normalizedRole = role.Trim();

    if (string.Equals(normalizedRole, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        return UserRoles.Admin;

    if (string.Equals(normalizedRole, UserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        return UserRoles.SuperAdmin;

    throw new BadRequestException(ErrorCodes.INVALID_BOOTSTRAP_ADMIN_ROLE);
}

static RateLimitPartition<string> CreateRateLimitPartition(
    HttpContext context,
    int permitLimit,
    TimeSpan window)
{
    string partitionKey = RateLimitPartitionKeyBuilder.ClientAddress(context);

    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = permitLimit,
        QueueLimit = 0,
        Window = window
    });
}

static RateLimitPartition<string> CreatePaymentPollingRateLimitPartition(
    HttpContext context,
    int permitLimit,
    TimeSpan window)
{
    string partitionKey = RateLimitPartitionKeyBuilder.PaymentPolling(context);

    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = permitLimit,
        QueueLimit = 0,
        Window = window
    });
}

public partial class Program
{
}
