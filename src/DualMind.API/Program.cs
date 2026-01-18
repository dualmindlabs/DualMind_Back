using System;
using DualMind.API.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load .env
EnvConfig.Load();

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DualMind API", Version = "v1" });

    // Auth support in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new System.Collections.Generic.List<string>()
        }
    });
});

// Configure Supabase Settings
// Configure Supabase Settings
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? System.Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/');
var jwtSecret = builder.Configuration["Supabase:JwtSecret"] ?? System.Environment.GetEnvironmentVariable("JWT_SECRET");
var supabaseKey = builder.Configuration["Supabase:Key"] ?? System.Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? System.Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
var supabaseServiceKey = builder.Configuration["Supabase:ServiceKey"] ?? System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");

builder.Services.Configure<DualMind.API.Infrastructure.Configuration.SupabaseSettings>(options =>
{
    options.Url = supabaseUrl;
    options.Key = supabaseKey;
    options.ServiceKey = supabaseServiceKey;
    options.JwtSecret = jwtSecret;
});

// Configure JWT Authentication for Supabase
// Issuer: {SUPABASE_URL}/auth/v1 for authenticated user tokens
// Audience: "authenticated" for logged-in users
var supabaseIssuer = $"{supabaseUrl}/auth/v1";

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.UseSecurityTokenValidators = true; // FORCE Legacy Handler to support Signature Bypass
        // options.Authority = supabaseIssuer; // Disabled to prevent OIDC interference with Bypass
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseIssuer,
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Map 'sub' claim to NameIdentifier
            NameClaimType = "sub"
        };
        
        // If JWT_SECRET is provided, use it for HS256 validation (legacy/fallback)
        if (!string.IsNullOrEmpty(jwtSecret))
        {
            options.TokenValidationParameters.IssuerSigningKey = 
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtSecret));
        }
        else
        {
            // WARN: Development only fallback!
            System.Console.WriteLine("⚠️ WARNING: JWT Secret missing. Bypassing Signature & Audience Validation for Local Dev.");
            options.TokenValidationParameters.ValidateIssuerSigningKey = false;
            options.TokenValidationParameters.RequireSignedTokens = false;
            options.TokenValidationParameters.ValidateAudience = false;
            options.TokenValidationParameters.SignatureValidator = delegate (string token, Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
            {
                var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
                return jwt;
            };
        }
        
        // Event handlers for debugging
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst("sub")?.Value;
                logger.LogDebug("JWT validated for user {UserId}", userId);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Challenge: {Error}, {ErrorDescription}", context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Add HTTP Client for SupabaseService with configured headers
builder.Services.AddHttpClient<DualMind.API.Infrastructure.Data.ISupabaseService, DualMind.API.Infrastructure.Data.SupabaseService>((serviceProvider, client) =>
{
    var supabaseUrl = System.Environment.GetEnvironmentVariable("SUPABASE_URL")?.TrimEnd('/');
    var apiKey = System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") 
                ?? System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY")
                ?? System.Environment.GetEnvironmentVariable("SUPABASE_KEY");
    
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("apikey", apiKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
});

// Register Application Services

// Register AI Provider services with typed HttpClient (prevents socket exhaustion)
builder.Services.AddHttpClient<DualMind.API.AI.Providers.GroqService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddHttpClient<DualMind.API.AI.Providers.BytezService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300); // Higher timeout for Bytez
});

builder.Services.AddScoped<DualMind.API.AI.Gateway.IChatProviderFactory, DualMind.API.AI.Gateway.ChatProviderFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DualMind.API.Core.Services.IModelSelector, DualMind.API.Core.Services.ModelSelector>();

builder.Services.AddScoped<DualMind.API.Core.Services.IThreadsService, DualMind.API.Core.Services.ThreadsService>();
builder.Services.AddScoped<DualMind.API.Core.Services.IThreadMessagesService, DualMind.API.Core.Services.ThreadMessagesService>();
builder.Services.AddScoped<DualMind.API.Core.Services.IModelStatsService, DualMind.API.Core.Services.ModelStatsService>();
builder.Services.AddScoped<DualMind.API.Core.Services.ILeaderboardModelSelector, DualMind.API.Core.Services.LeaderboardModelSelector>();
builder.Services.AddScoped<DualMind.API.Core.Services.IComparisonLogger, DualMind.API.Core.Services.ComparisonLogger>();
builder.Services.AddScoped<DualMind.API.Core.Services.IMessageLogger, DualMind.API.Core.Services.MessageLogger>();
builder.Services.AddScoped<DualMind.API.Core.Services.IUserSyncService, DualMind.API.Core.Services.UserSyncService>();

// Register Admin Services
builder.Services.AddHttpClient<DualMind.API.Infrastructure.Data.IAdminSupabaseClient, DualMind.API.Infrastructure.Data.AdminSupabaseClient>();
builder.Services.AddScoped<DualMind.API.Core.Services.IProviderConfigService, DualMind.API.Core.Services.ProviderConfigService>();

// Allow CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

// Global Exception Handler with proper logging
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var exceptionHandler = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

        if (exceptionHandler?.Error != null)
        {
            logger.LogError(exceptionHandler.Error, 
                "Unhandled exception for {Method} {Path}", 
                context.Request.Method, 
                context.Request.Path);
        }

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 500,
            Title = "An unexpected error occurred",
            Detail = env.IsDevelopment() ? exceptionHandler?.Error?.Message : null
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

// Request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var correlationId = Guid.NewGuid().ToString("N")[..8];
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    logger.LogInformation("[{CorrelationId}] {Method} {Path} started", 
        correlationId, context.Request.Method, context.Request.Path);

    await next();

    stopwatch.Stop();
    logger.LogInformation("[{CorrelationId}] {Method} {Path} completed in {ElapsedMs}ms with {StatusCode}", 
        correlationId, context.Request.Method, context.Request.Path, 
        stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
});

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
