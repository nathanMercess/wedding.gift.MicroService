using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Polly;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;
using wedding.gift.Services.Implementations.Security;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

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

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Erro de validação",
            Detail = "Verifique os campos enviados e tente novamente."
        };

        _ = QueueValidationNotificationAsync(
            context.HttpContext.RequestServices,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value ?? string.Empty,
            context.ModelState);

        return new BadRequestObjectResult(details);
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.Configure<GcsOptions>(builder.Configuration.GetSection(GcsOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);

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
    });

builder.Services.AddAuthorization();

builder.Services
    .AddHttpClient<IMercadoPagoService, MercadoPagoService>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(
        3, retryAttempt => TimeSpan.FromMilliseconds(300 * Math.Pow(2, retryAttempt))));

builder.Services.AddScoped<wedding.gift.Infra.Contracts.IPaymentRepository, wedding.gift.Infra.Implementations.Repositories.PaymentRepository>();
builder.Services.AddScoped<wedding.gift.Services.Contracts.IPaymentService, wedding.gift.Services.Implementations.PaymentService>();

builder.Services.AddSingleton<wedding.gift.Services.Contracts.IBackgroundTaskQueue, wedding.gift.Application.Webapi.Infrastructure.BackgroundTaskQueue>();
builder.Services.AddHostedService<wedding.gift.Application.Webapi.Infrastructure.QueuedHostedService>();

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

builder.Services.AddServices();

WebApplication app = builder.Build();

bool runMigrations = builder.Configuration.GetValue<bool>("Database:RunMigrations");

if (runMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health/db", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return canConnect
        ? Results.Ok(new { database = "ok" })
        : Results.Problem("Database connection failed");
});

await EnsureBootstrapAdminAsync(app.Services, builder.Configuration);

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var correlationId = context.TraceIdentifier;
        var queue = context.RequestServices.GetRequiredService<IBackgroundTaskQueue>();

        if (exception is AppException appEx)
        {
            logger.LogWarning(exception, "Erro de negócio {Status} em {Path}. CorrelationId={CorrelationId}",
                appEx.StatusCode, context.Request.Path, correlationId);
        }
        else if (exception is not null)
        {
            logger.LogError(exception, "Erro não tratado em {Path}. CorrelationId={CorrelationId}",
                context.Request.Path, correlationId);
        }

        if (exception is not null)
        {
            var subject = $"[wedding.gift] {exception.GetType().Name}: {exception.Message}";
            var body = $"CorrelationId: {correlationId}\nPath: {context.Request.Path}\n" +
                       $"Method: {context.Request.Method}\nTime: {DateTime.UtcNow:u}\n\n{exception}";

            await queue.EnqueueAsync(async (sp, ct) =>
            {
                var emailSvc = sp.GetRequiredService<IEmailService>();
                await emailSvc.SendErrorNotificationAsync(subject, body, ct);
            });
        }

        var problem = exception switch
        {
            AppException appException => new ProblemDetails
            {
                Status = appException.StatusCode,
                Title = appException.Title,
                Detail = appException.Message
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno no servidor",
                Detail = "Ocorreu um erro inesperado."
            }
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors("AngularDev");
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task EnsureBootstrapAdminAsync(IServiceProvider services, IConfiguration configuration)
{
    var options = configuration.GetSection(BootstrapAdminOptions.SectionName).Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions();

    if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
    {
        return;
    }

    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var normalizedEmail = options.Email.Trim().ToLowerInvariant();
    var exists = await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail);

    if (exists)
    {
        return;
    }

    var (hash, salt) = PasswordHasher.HashPassword(options.Password);

    dbContext.Users.Add(new User
    {
        Id = Guid.NewGuid(),
        Name = string.IsNullOrWhiteSpace(options.Name) ? "Administrador" : options.Name.Trim(),
        Email = options.Email.Trim(),
        NormalizedEmail = normalizedEmail,
        PasswordHash = hash,
        PasswordSalt = salt,
        Role = UserRoles.Admin,
        IsActive = true,
        IsEmailConfirmed = true
    });

    await dbContext.SaveChangesAsync();
}

static async Task QueueValidationNotificationAsync(
    IServiceProvider services,
    string method,
    string path,
    ModelStateDictionary modelState)
{
    var queue = services.GetRequiredService<IBackgroundTaskQueue>();
    var errors = modelState
        .Where(x => x.Value?.Errors.Count > 0)
        .SelectMany(x => x.Value!.Errors.Select(error => $"{x.Key}: {error.ErrorMessage}"))
        .ToArray();

    var body = $"""
        Erro de validação recebido.

        Path: {path}
        Method: {method}
        Time: {DateTime.UtcNow:u}

        Erros:
        {string.Join(Environment.NewLine, errors)}
        """;

    await queue.EnqueueAsync(async (sp, ct) =>
    {
        var emailSvc = sp.GetRequiredService<IEmailService>();
        await emailSvc.SendErrorNotificationAsync("[wedding.gift] Erro de validação", body, ct);
    });
}
