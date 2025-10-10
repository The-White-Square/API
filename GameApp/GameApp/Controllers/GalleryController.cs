using Microsoft.AspNetCore.Mvc;

namespace GameApp.Controllers;

[ApiController]
[Route("gallery")]
public class GalleryController : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private readonly IWebHostEnvironment _env;
    private readonly string _imagesRoot;

    public GalleryController(IWebHostEnvironment env)
    {
        _env = env;
        _imagesRoot = EnsureImagesRoot(_env);
    }

    [HttpGet]
    public ActionResult<IEnumerable<ImageDto>> List()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new ImageDto(
                Id: Path.GetFileName(f),
                Url: $"/images/{Path.GetFileName(f)}",
                Bytes: new FileInfo(f).Length
            ))
            .OrderBy(i => i.Id)
            .ToList();

        return Ok(files);
    }

    [HttpGet("random")]
    public ActionResult<ImageDto> GetRandomImage()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
            return NotFound("No images found. Upload one first.");

        var pick = files[Random.Shared.Next(files.Count)];
        var dto = new ImageDto(
            Id: Path.GetFileName(pick),
            Url: $"/images/{Path.GetFileName(pick)}",
            Bytes: new FileInfo(pick).Length
        );

        return Ok(dto);
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)] // 20 MB
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageDto>> Upload([FromForm] UploadImageRequest request)
    {
        var file = request.File;

        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return BadRequest("Only .jpg, .jpeg, .png, .gif, .webp are allowed.");

        var safeName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var savePath = Path.Combine(_imagesRoot, safeName);

        await using (var fs = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(fs);
        }

        var dto = new ImageDto(
            Id: safeName,
            Url: $"/images/{safeName}",
            Bytes: file.Length
        );

        return Created(dto.Url, dto);
    }

    private static string EnsureImagesRoot(IWebHostEnvironment env)
    {
        var root = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
        Directory.CreateDirectory(root);
        return root;
    }
}
