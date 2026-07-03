using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Polly;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.Configure<GcsOptions>(builder.Configuration.GetSection(GcsOptions.SectionName));

JwtOptions jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
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
    });

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();

builder.Services
    .AddHttpClient<IMercadoPagoService, MercadoPagoService>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(
        3, retryAttempt => TimeSpan.FromMilliseconds(300 * Math.Pow(2, retryAttempt))));

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

builder.Services.AddRepositories();
builder.Services.AddServices();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    //await db.Database.MigrateAsync();
}

await EnsureBootstrapAdminAsync(app.Services, builder.Configuration);

app.UseGlobalExceptionHandler();
app.UseMiddleware<ApiResponseMiddleware>();
app.UseCors("AngularDev");
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseMiddleware<ApiRequestLoggingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

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
