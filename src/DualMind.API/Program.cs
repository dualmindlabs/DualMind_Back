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
builder.Services.Configure<DualMind.API.Infrastructure.Configuration.SupabaseSettings>(options =>
{
    options.Url = System.Environment.GetEnvironmentVariable("SUPABASE_URL");
    options.Key = System.Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? System.Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
    options.ServiceKey = System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? System.Environment.GetEnvironmentVariable("SUPABASE_SERVICE_KEY");
    options.JwtSecret = System.Environment.GetEnvironmentVariable("JWT_SECRET");
});

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
// Register Application Services
builder.Services.AddScoped<DualMind.API.AI.Providers.GroqService>();
builder.Services.AddScoped<DualMind.API.AI.Providers.BytezService>();
builder.Services.AddScoped<DualMind.API.AI.Gateway.IChatProviderFactory, DualMind.API.AI.Gateway.ChatProviderFactory>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DualMind.API.Core.Services.IModelSelector, DualMind.API.Core.Services.ModelSelector>();

// builder.Services.AddScoped<DualMind.API.Core.Services.ISupabaseService, ...> removed as it doesn't exist in Core.Services
// Let's stick to standard AddHttpClient. 

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

// Global Exception Handler
app.UseExceptionHandler("/error");
app.Map("/error", (Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature feature) =>
{
    var ex = feature?.Error;
    return Microsoft.AspNetCore.Http.Results.Problem(
        detail: ex?.Message,
        title: "An unexpected error occurred",
        statusCode: 500
    );
});

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Removed /health mapping to avoid conflict with HealthController
// app.MapGet("/health", ...);

app.Run();

public partial class Program { }
