using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Nexus.API.Middlewares;
using Nexus.API.Services;
using Nexus.Application;
using Nexus.Application.Common.Interfaces;
using Nexus.Infrastructure;
using Nexus.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Clean Architecture Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add API Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<Nexus.API.Filters.ValidationFilter>();
});
builder.Services.AddEndpointsApiExplorer();

// 3. Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// 4. Configure JWT Authentication
var secretKey = builder.Configuration["JwtSettings:SecretKey"];
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
    {
        throw new InvalidOperationException("JwtSettings:SecretKey is required and must be at least 32 characters (256 bits) in production.");
    }

    var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (defaultConnection != null && defaultConnection.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("InMemory database provider is strictly prohibited in Production environment. A valid SQL Server connection string is required.");
    }
}
else
{
    if (string.IsNullOrWhiteSpace(secretKey))
    {
        secretKey = "Nexus_Super_Secret_Key_For_Development_32_Bytes_Long!";
    }
}

var issuer = builder.Configuration["JwtSettings:Issuer"] ?? "NexusAPI";
var audience = builder.Configuration["JwtSettings:Audience"] ?? "NexusDesktopClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = false; // Claims-based auth: raw token retention in AuthenticationProperties is disabled for security and memory efficiency
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NEXUS API",
        Version = "v1",
        Description = "Modular API for NEXUS — AI Knowledge & Learning Workspace"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
});

// 6. CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("NexusCorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

var app = builder.Build();

// 7. Database Migration on Startup (Configurable)
var applyMigrations = builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup", builder.Environment.IsDevelopment());
if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "A critical error occurred while migrating the database. Application startup aborted.");
        throw;
    }
}

// 8. Configure HTTP Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NEXUS API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at root
    });
}

app.UseCors("NexusCorsPolicy");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// For integration tests
public partial class Program { }
