using GameApp.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LobbyService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Show detailed exceptions while developing
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Serve /wwwroot
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

// Make sure that wwwroot/images exists
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
var imagesRoot = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
Directory.CreateDirectory(imagesRoot);

app.Run();
