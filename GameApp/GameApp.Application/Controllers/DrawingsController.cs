using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GameApp.Service.Options;

namespace GameApp.Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DrawingsController : ControllerBase
{
    private readonly string _drawingsRoot;

    public DrawingsController(IOptions<GalleryOptions> options, IWebHostEnvironment env)
    {
        var root = options.Value.DrawingsRoot ?? "wwwroot/drawings";
        _drawingsRoot = Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
        Directory.CreateDirectory(_drawingsRoot);
    }

    [HttpPost]
    public async Task<IActionResult> PostDrawing([FromForm] IFormFile file)
    {
        if (file is null)
            return BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PNG files are allowed.");

        var safeName = $"{Guid.NewGuid()}.png";
        var savePath = Path.Combine(_drawingsRoot, safeName);

        await using (var fs = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(fs);
        }

        var url = $"/drawings/{safeName}";
        return Created(url, new { fileName = safeName, url });
    }

    // GET api/drawings/latest
    // Returns 204 NoContent when none exist, otherwise 200 { url: "/drawings/{file}" }
    [HttpGet("latest")]
    public IActionResult GetLatest()
    {
        if (!Directory.Exists(_drawingsRoot))
            return NoContent();

        var file = Directory.EnumerateFiles(_drawingsRoot)
            .Where(f => string.Equals(Path.GetExtension(f), ".png", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        if (file is null) return NoContent();

        var fileName = Path.GetFileName(file);
        var url = $"/drawings/{fileName}";
        return Ok(new { url });
    }
}