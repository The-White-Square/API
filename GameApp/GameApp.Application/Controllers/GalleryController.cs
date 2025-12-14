using Microsoft.AspNetCore.Mvc;
using GameApp.Service.Services;
using GameApp.Application.Requests;
using GameApp.Application.Models;

namespace GameApp.Application.Controllers;

[ApiController]
[Route("gallery")]
public class GalleryController : ControllerBase
{
    private readonly IGalleryService _gallery;

    public GalleryController(IGalleryService gallery)
    {
        _gallery = gallery;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ImageResponse>> List()
    {
        var dtos = _gallery.ListImages();
        var results = dtos.Select(d => new ImageResponse { Id = d.Id, Url = d.Url }).ToList();
        return Ok(results);
    }

    [HttpGet("random")]
    public ActionResult<ImageResponse> GetRandomImage()
    {
        var dto = _gallery.GetRandomImage();
        if (dto is null) return NotFound("No images found.");
        var result = new ImageResponse { Id = dto.Id, Url = dto.Url };
        return Ok(result);
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)] // 20 MB
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageResponse>> Upload([FromForm] UploadImageRequest request)
    {
        try
        {
            var file = request.File;
            var dto = await _gallery.SaveImageAsync(
                file.OpenReadStream(),
                file.FileName,
                file.Length,
                HttpContext.RequestAborted
            );
            var result = new ImageResponse { Id = dto.Id, Url = dto.Url };
            return Created(result.Url, result);
        }
        catch (ArgumentException ae)
        {
            return BadRequest(ae.Message);
        }
        catch (InvalidOperationException ioe)
        {
            return BadRequest(ioe.Message);
        }
    }
}
