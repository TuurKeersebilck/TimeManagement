using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TimeManagementBackend.Config;
using TimeManagementBackend.Data;
using TimeManagementBackend.Middleware;
using TimeManagementBackend.Models;
using TimeManagementBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
LoadEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<AutoMapperProfile>());

// Configure DbContext
ConfigureDatabaseContext(builder);

// Configure Identity
ConfigureIdentity(builder);

// Configure JWT Authentication
ConfigureAuthentication(builder);

// Configure CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
        ?? (builder.Environment.IsDevelopment() ? "http://localhost:5173" : null))
        ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? throw new InvalidOperationException("CORS_ORIGINS environment variable must be set in production.");

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for HttpOnly cookie auth
    });
});

// Register application services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
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
app.UseCors("AllowFrontend");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Create default roles
await CreateDefaultRoles(app);

app.Run();

// Configure Database Context
void ConfigureDatabaseContext(WebApplicationBuilder builder)
{
    var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? (builder.Environment.IsDevelopment()
            ? "Data Source=timemanagement.db"
            : throw new InvalidOperationException("CONNECTION_STRING environment variable must be set in production."));

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
        
        Secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                 ?? builder.Configuration["JWT:Secret"]
                 ?? (builder.Environment.IsDevelopment()
                     ? "YourSuperSecretKeyWithAtLeast32Characters!"
                     : throw new InvalidOperationException("JWT_SECRET environment variable must be set in production.")),
        
        ExpiryInMinutes = int.TryParse(
            Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES") ?? 
            builder.Configuration["JWT:ExpiryInMinutes"] ?? 
            "60", out var minutes) ? minutes : 60
    };

    // Register the JWT config for dependency injection
    builder.Services.AddSingleton(jwtConfig);
                
    if (jwtConfig.Secret == "YourSuperSecretKeyWithAtLeast32Characters!")
    {
        Console.WriteLine("Warning: Using default JWT secret key. This should be changed in production.");
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
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidAudience = jwtConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Secret)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Read JWT from HttpOnly cookie; fall back to Authorization header
                if (context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                    context.Token = cookieToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var blacklist = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenBlacklistService>();
                var jti = context.Principal?.FindFirst(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (jti != null && blacklist.IsRevoked(jti))
                    context.Fail("Token has been revoked.");
                return Task.CompletedTask;
            }
        };
    });
}

// Create default roles
async Task CreateDefaultRoles(WebApplication app)
{
    using (var scope = app.Services.CreateScope())
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var roles = new[] { "Admin", "User" };
        
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Console.WriteLine($"Created role: {role}");
            }
        }
    }
}

// Helper method to load .env file if present
void LoadEnvironmentVariables()
{
    var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(envFile))
    {
        Console.WriteLine("No .env file found. Using configuration values or defaults.");
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
                Console.WriteLine($"Warning: Invalid line in .env file: {line}");
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
        Console.WriteLine(".env file loaded successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading .env file: {ex.Message}");
    }
}
