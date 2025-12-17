using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

using GameApp.Application.Hubs;
using GameApp.Service.Services; // IGalleryService, ILobbyService
using GameApp.Service.Utils;    // IDrawingRelay, ILobbyCodeGenerator
using GameApp.Integration.Data; // AppDbContext, EfLobbyRepository, EfPlayerRepository
using GameApp.Application.SignalR; // SignalRDrawingRelay
using GameApp.Integration.Gallery; // FileSystemGalleryRepository
using GameApp.Service.Options; // GalleryOptions

var builder = WebApplication.CreateBuilder(args);

// Bind options
builder.Services.Configure<GalleryOptions>(builder.Configuration.GetSection("Gallery"));

// CORS for local frontend dev servers (Vite default 5173)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Resolve absolute SQLite path into a folder under content root
var contentRoot = builder.Environment.ContentRootPath;
var dbFolder = Path.Combine(contentRoot, "DB_Data");
Directory.CreateDirectory(dbFolder);
var dbPath = Path.Combine(dbFolder, "gameapp.db");

// Override the connection string with the absolute path
builder.Configuration["ConnectionStrings:Default"] = $"Data Source={dbPath}";

// SignalR
builder.Services.AddSignalR();

// EF Core with the resolved path
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Integration repositories
builder.Services.AddScoped<ILobbyRepository, EfLobbyRepository>();
builder.Services.AddScoped<IPlayerRepository, EfPlayerRepository>();
builder.Services.AddSingleton<IGalleryRepository, FileSystemGalleryRepository>();

// Service layer registrations
builder.Services.AddScoped<IGalleryService, GalleryService>();
builder.Services.AddScoped<ILobbyService, LobbyService>();
builder.Services.AddSingleton<ILobbyCodeGenerator, RandomLobbyCodeGenerator>();
builder.Services.AddSingleton<GameApp.Service.Services.IDrawingStore, GameApp.Service.Services.InMemoryDrawingStore>();

// SignalR drawing relay adapter (now in Application)
builder.Services.AddSingleton<IDrawingRelay, SignalRDrawingRelay>();

var app = builder.Build();

// Ensure images folder exists (based on configured GalleryOptions)
var galleryOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GalleryOptions>>().Value;
var imagesRoot = Path.IsPathRooted(galleryOpts.ImagesRoot)
    ? galleryOpts.ImagesRoot
    : Path.Combine(app.Environment.ContentRootPath, galleryOpts.ImagesRoot);
Directory.CreateDirectory(imagesRoot);

// Ensure drawings folder exists (new)
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

app.UseHttpsRedirection();

// Add lightweight middleware so static image/drawing responses include CORS headers
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/drawings", StringComparison.OrdinalIgnoreCase))
    {
        // allow Vite dev server origin(s) to access static images/drawings
        context.Response.Headers["Access-Control-Allow-Origin"] = "http://localhost:5173";
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
    }
    await next();
});

// Serve static files from wwwroot (images are placed under wwwroot/images)
app.UseStaticFiles();

// Routing + CORS + Authorization pipeline
app.UseRouting();
app.UseCors("DevCors");
app.UseAuthorization();

app.MapControllers();
app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();

public partial class Program { }