using ECards.Api.Data;
using ECards.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using System.Linq;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Bind options
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>()
                .LogWarning("No CORS origins configured. CORS will block all cross-origin requests.");
            allowedOrigins = Array.Empty<string>();
        }
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication (OpenID Connect / Keycloak example)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = bool.TryParse(builder.Configuration["Authentication:RequireHttpsMetadata"], out var r) ? r : true;

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                // Keycloak often places roles in the JWT payload under realm_access.roles
                try
                {
                    var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeader.Substring("Bearer ".Length).Trim();
                        var parts = token.Split('.');
                        if (parts.Length >= 2)
                        {
                            string payload = parts[1];
                            // pad Base64
                            switch (payload.Length % 4)
                            {
                                case 2: payload += "=="; break;
                                case 3: payload += "="; break;
                            }
                            payload = payload.Replace('-', '+').Replace('_', '/');
                            var bytes = Convert.FromBase64String(payload);
                            var json = Encoding.UTF8.GetString(bytes);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("realm_access", out var realmAccess) && realmAccess.ValueKind == JsonValueKind.Object && realmAccess.TryGetProperty("roles", out var rolesElem) && rolesElem.ValueKind == JsonValueKind.Array)
                            {
                                var id = ctx.Principal?.Identity as ClaimsIdentity;
                                if (id != null)
                                {
                                    foreach (var r in rolesElem.EnumerateArray())
                                    {
                                        var role = r.GetString();
                                        if (!string.IsNullOrEmpty(role)) id.AddClaim(new Claim(ClaimTypes.Role, role));
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore claims mapping errors
                }

                return Task.CompletedTask;
            }
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(new Version(10, 11, 0)); // MariaDB 10.11
builder.Services.AddDbContext<ECardsDbContext>(options =>
{
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
    {
        mySqlOptions.MigrationsAssembly("ECards.Api");
    });
    // Suppress model validation errors during migration - we'll validate post-migration instead
    options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Services
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Background services
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddHostedService<ScheduledSendingService>();

var app = builder.Build();

// Configure PathBase for reverse proxy scenarios (e.g., /api)
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
    app.Logger.LogInformation("Using PathBase: {PathBase}", pathBase);
}

// Auto-create database on startup BEFORE anything else runs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ECardsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Checking database...");
        
        // Apply EF Core migrations at startup if possible (safe no-op when none)
        try
        {
            // Note: Pre-migration model validation was removed as it prevented initial migrations
            // from running. The database will be verified after migrations are applied.
            
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
            
            logger.LogInformation("Applied migrations: {Count} - {Migrations}", 
                appliedMigrations.Count(), string.Join(", ", appliedMigrations));
            logger.LogInformation("Pending migrations: {Count} - {Migrations}", 
                pendingMigrations.Count(), string.Join(", ", pendingMigrations));
            
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("No pending migrations to apply.");
                // Still call Migrate to ensure database exists
                await db.Database.MigrateAsync();
            }
            logger.LogInformation("Database migrations applied (if any).");

            // Verify expected tables exist after migrations to avoid running with an
            // incomplete schema. This is a pragmatic, provider-agnostic check that
            // fails fast if key tables are missing.
            try
            {
                var conn = db.Database.GetDbConnection();
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('ECards','Senders','PremadeTemplates','ViewAudits');";
                    var result = await cmd.ExecuteScalarAsync();
                    var found = Convert.ToInt32(result ?? 0);
                    if (found < 4)
                    {
                        logger.LogError("Expected schema tables are missing after migrations (found {Found}). Refusing to start.", found);
                        throw new InvalidOperationException("Required database tables missing after migrations.");
                    }
                }
            }
            catch (Exception postCheckEx)
            {
                logger.LogError(postCheckEx, "Post-migration schema verification failed; refusing to start.");
                throw;
            }
        }
        catch (Exception mex)
        {
            logger.LogError(mex, "Applying migrations failed; refusing to start to avoid running with an inconsistent schema.");
            throw; // Stop the application if database can't be initialized
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
        throw; // Stop the application if database can't be initialized
    }
}

// Use CORS
app.UseCors("AllowFrontend");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
