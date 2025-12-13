using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

using GameApp.Application.Hubs;
using GameApp.Service;
using GameApp.Service.Services; // IGalleryService, ILobbyService, ILobbyCodeGenerator
using GameApp.Service.Utils;    // IDrawingRelay, ILobbyCodeGenerator
using GameApp.Integration.Data; // AppDbContext
using GameApp.Integration.SignalR; // SignalRDrawingRelay

var builder = WebApplication.CreateBuilder(args);

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

// SignalR
builder.Services.AddSignalR();

// EF Core (SQLite file in app root)
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=gameapp.db"));

// Application services
builder.Services.AddSingleton<IGalleryService, GalleryService>();
builder.Services.AddSingleton<ILobbyService, LobbyService>();
builder.Services.AddSingleton<ILobbyCodeGenerator, RandomLobbyCodeGenerator>();

// Integration adapters
builder.Services.AddSingleton<IDrawingRelay, SignalRDrawingRelay>();

var app = builder.Build();

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

// Serve static files from wwwroot (images are placed under wwwroot/images)
app.UseStaticFiles();

// Routing + CORS + Authorization pipeline
app.UseRouting();
app.UseCors("DevCors");
app.UseAuthorization();

app.MapControllers();

// Map the lobby SignalR hub
app.MapHub<LobbyHub>("/hubs/lobby");

// make sure images folder exists so GalleryService / static files works
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
var imagesRoot = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
Directory.CreateDirectory(imagesRoot);

app.Run();
public partial class Program { } // for usage in integration tests.