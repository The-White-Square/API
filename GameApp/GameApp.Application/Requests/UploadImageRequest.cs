namespace GameApp.Application.Requests;

public class UploadImageRequest
{
    // Property name "File" will appear in Swagger as the form field
    public IFormFile File { get; set; } = default!;
}
