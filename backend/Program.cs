using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using TimeManagementBackend.Config;
using TimeManagementBackend.Data;
using TimeManagementBackend.Middleware;
using TimeManagementBackend.Models;
using TimeManagementBackend.Services;

// Bootstrap logger for startup (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // Load environment variables
    LoadEnvironmentVariables();

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Register AutoMapper
    builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

    // Configure DbContext
    ConfigureDatabaseContext(builder);

    // Configure Identity
    ConfigureIdentity(builder);

    // Configure JWT Authentication
    ConfigureAuthentication(builder);

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Register application services
    builder.Services.AddScoped<ITimeLogService, TimeLogService>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    builder.Services.AddScoped<IVacationService, VacationService>();

    var app = builder.Build();

    // Use global exception handling middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("AllowAll");

    // Add authentication and authorization middleware
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Create default roles
    await CreateDefaultRoles(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Configure Database Context
void ConfigureDatabaseContext(WebApplicationBuilder builder)
{
    var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ??
                           builder.Configuration.GetConnectionString("DefaultConnection") ??
                           "Data Source=timemanagement.db";

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
            // Use MySQL/MariaDB
            options.UseMySql(connectionString,
                new MySqlServerVersion(new Version(11, 0, 0))
            );
    });
}


// Configure Identity Services
void ConfigureIdentity(WebApplicationBuilder builder)
{
    builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

    // Register JwtService
    builder.Services.AddScoped<JwtService>();
}

// Configure JWT Authentication
void ConfigureAuthentication(WebApplicationBuilder builder)
{
    // Get JWT settings from environment variables
    var jwtConfig = new JwtConfig
    {
        Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ??
                 builder.Configuration["JWT:Issuer"] ??
                 "TimeManagementAPI",

        Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ??
                   builder.Configuration["JWT:Audience"] ??
                   "TimeManagementAPI",

        Secret = Environment.GetEnvironmentVariable("JWT_SECRET") ??
                 builder.Configuration["JWT:Secret"] ??
                 "YourSuperSecretKeyWithAtLeast32Characters!",

        ExpiryInMinutes = int.TryParse(
            Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES") ??
            builder.Configuration["JWT:ExpiryInMinutes"] ??
            "60", out var minutes) ? minutes : 60
    };

    // Register the JWT config for dependency injection
    builder.Services.AddSingleton(jwtConfig);

    if (jwtConfig.Secret == "YourSuperSecretKeyWithAtLeast32Characters!")
    {
        Log.Warning("Using default JWT secret key. This should be changed in production.");
    }

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidAudience = jwtConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Secret)),
            ClockSkew = TimeSpan.Zero // Removes the default 5 minute tolerance for token expiration
        };
    });
}

// Create default roles
async Task CreateDefaultRoles(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var roles = new[] { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            logger.LogInformation("Created role: {Role}", role);
        }
    }
}

// Helper method to load .env file if present
void LoadEnvironmentVariables()
{
    var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(envFile))
    {
        Log.Information("No .env file found. Using configuration values or defaults.");
        return;
    }

    try
    {
        foreach (var line in File.ReadAllLines(envFile))
        {
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                Log.Warning("Invalid line in .env file: {Line}", line);
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            // Remove quotes if they exist
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                value = value.Substring(1, value.Length - 2);
            }

            // Don't override existing environment variables
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
        Log.Information(".env file loaded successfully.");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error loading .env file");
    }
}
