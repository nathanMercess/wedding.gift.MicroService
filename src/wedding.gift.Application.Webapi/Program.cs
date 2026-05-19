using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;
using wedding.gift.Services.Implementations.Security;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularLocal", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddControllers()
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

        return new BadRequestObjectResult(details);
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

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

await EnsureBootstrapAdminAsync(app.Services, builder.Configuration);

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

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

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors("AngularLocal");

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
        IsActive = true
    });

    await dbContext.SaveChangesAsync();
}
