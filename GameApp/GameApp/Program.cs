﻿using System;
using System.IO;
using GameApp.Hubs;
using GameApp.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// CORS for local frontend dev servers (Vite default 5173)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "https://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR
builder.Services.AddSignalR();

// Application services
builder.Services.AddSingleton<GalleryService>();
builder.Services.AddSingleton<LobbyService>();

var app = builder.Build();

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