using Microsoft.AspNetCore.Mvc;
using GameApp.Service;

namespace GameApp.Controllers;

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
    public ActionResult<IEnumerable<ImageDto>> List() => Ok(_gallery.ListImages());

    [HttpGet("random")]
    public ActionResult<ImageDto> GetRandomImage()
    {
        var dto = _gallery.GetRandomImage();
        if (dto is null) return NotFound("No images found.");
        return Ok(dto);
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)] // 20 MB
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ImageDto>> Upload([FromForm] UploadImageRequest request)
    {
        try
        {
            var dto = await _gallery.SaveImageAsync(request.File);
            return Created(dto.Url, dto);
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
