using System;
using System.IO;
using System.Text.Json;
using GameApp.Application.Hubs;
using GameApp.Application.SignalR;
using GameApp.Integration.Data;
using GameApp.Integration.Gallery;
using GameApp.Service.Options;
using GameApp.Service.Services;
using GameApp.Service.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Bind options
builder.Services.Configure<GalleryOptions>(builder.Configuration.GetSection("Gallery"));

// Read allowed origins early and store
var allowedOrigins = builder.Environment.IsDevelopment()
    ? new[] { "http://localhost:5173", "https://localhost:5173" }
    : builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
        ?? new[] { "https://co-opy.com" };

// Log the allowed origins for debugging
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Allowed Origins: {string.Join(", ", allowedOrigins)}");

// Environment-aware CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)); // Cache preflight requests
    });
});

// Controllers with camelCase
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();

// Only add Swagger in Development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
}

// Database path configuration
string dbPath;
if (builder.Environment.IsDevelopment())
{
    var contentRoot = builder.Environment.ContentRootPath;
    var dbFolder = Path.Combine(contentRoot, "DB_Data");
    Directory.CreateDirectory(dbFolder);
    dbPath = Path.Combine(dbFolder, "gameapp.db");
}
else
{
    // Production: use configured path or default to /var/app
    dbPath = builder.Configuration.GetConnectionString("Default") 
        ?? "/var/app/DB_Data/gameapp.db";
    
    // Ensure directory exists
    var dbDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }
}

// Override connection string with resolved path
builder.Configuration["ConnectionStrings:Default"] = $"Data Source={dbPath}";

// SignalR with production settings
builder.Services.AddSignalR(options =>
{
    if (!builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors = false;
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    }
});

// EF Core with the resolved path
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Default"));
    
    // Disable sensitive data logging in production
    if (!builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(false);
        options.EnableDetailedErrors(false);
    }
});

// Integration repositories
builder.Services.AddScoped<ILobbyRepository, EfLobbyRepository>();
builder.Services.AddScoped<IPlayerRepository, EfPlayerRepository>();
builder.Services.AddSingleton<IGalleryRepository, FileSystemGalleryRepository>();

// Service layer registrations
builder.Services.AddScoped<IGalleryService, GalleryService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();
builder.Services.AddSingleton<ILobbyCodeGenerator, RandomLobbyCodeGenerator>();
builder.Services.AddSingleton<GameApp.Service.Services.IDrawingStore, GameApp.Service.Services.InMemoryDrawingStore>();

// SignalR drawing relay adapter
builder.Services.AddSingleton<IDrawingRelay, SignalRDrawingRelay>();

// Health checks for monitoring
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Ensure images and drawings folders exist
var galleryOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GalleryOptions>>().Value;

var imagesRoot = Path.IsPathRooted(galleryOpts.ImagesRoot)
    ? galleryOpts.ImagesRoot
    : Path.Combine(app.Environment.ContentRootPath, galleryOpts.ImagesRoot);
Directory.CreateDirectory(imagesRoot);

var drawingsRoot = Path.IsPathRooted(galleryOpts.DrawingsRoot)
    ? galleryOpts.DrawingsRoot
    : Path.Combine(app.Environment.ContentRootPath, galleryOpts.DrawingsRoot);
Directory.CreateDirectory(drawingsRoot);

// Create database schema if missing
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production error handling
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve static files
app.UseStaticFiles();

// CRITICAL: Correct middleware order for CORS
app.UseRouting();

// Apply CORS policy - this must come AFTER UseRouting and BEFORE UseAuthorization
app.UseCors("AppCors");

// Custom middleware for additional static file CORS headers (if needed)
// This runs AFTER the main CORS middleware
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    
    // Log all incoming requests for debugging
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} from Origin: {context.Request.Headers["Origin"]}");
    
    // Additional CORS headers for static files if needed
    if (path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/drawings", StringComparison.OrdinalIgnoreCase))
    {
        var origin = context.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
        {
            // These should already be set by UseCors, but we can reinforce them
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                    context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    logger.LogInformation($"Added CORS headers for static file: {path}");
                }
                return Task.CompletedTask;
            });
        }
    }
    
    await next();
});

app.UseAuthorization();

// Health check endpoint with detailed response
app.MapHealthChecks("/health");

// Debug endpoint to check CORS configuration
app.MapGet("/api/debug/cors", (HttpContext context) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    return Results.Ok(new
    {
        environment = app.Environment.EnvironmentName,
        allowedOrigins = allowedOrigins,
        requestOrigin = origin,
        isAllowed = allowedOrigins.Contains(origin),
        timestamp = DateTime.UtcNow
    });
});

app.MapControllers();
app.MapHub<LobbyHub>("/hubs/lobby");

Console.WriteLine($"Application starting on environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Allowed origins configured: {string.Join(", ", allowedOrigins)}");

app.Run();