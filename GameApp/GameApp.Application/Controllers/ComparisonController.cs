using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using GameApp.Service.Services;
namespace GameApp.Controllers
{
    [ApiController]
    [Route("comparison")]
    public class ComparisonController : ControllerBase
    {
        private readonly IGalleryService _gallery;
        private readonly ILogger<ComparisonController> _logger;
        private readonly string _comparisonServiceUrl;

        public ComparisonController(IGalleryService gallery, ILogger<ComparisonController> logger)
        {
            _gallery = gallery;
            _logger = logger;
            _comparisonServiceUrl = Environment.GetEnvironmentVariable("COMPARISON_SERVICE_URL")
                ?? "http://localhost:5000/compare";
        }

        public class ComparisonRequest
        {
            // Provide either image IDs (from gallery) OR concrete paths.
            public string? ImageIdA { get; set; }
            public string? ImageIdB { get; set; }
            public string? ImagePathA { get; set; }
            public string? ImagePathB { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Compare([FromBody] ComparisonRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
                return BadRequest("Request body is required.");

            string? pathA = request.ImagePathA;
            string? pathB = request.ImagePathB;

            if (string.IsNullOrEmpty(pathA) && !string.IsNullOrEmpty(request.ImageIdA))
                pathA = _gallery.GetImageFilePath(request.ImageIdA);

            if (string.IsNullOrEmpty(pathB) && !string.IsNullOrEmpty(request.ImageIdB))
                pathB = _gallery.GetImageFilePath(request.ImageIdB);

            if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
                return BadRequest("Both image paths or image IDs must be provided and resolvable.");

            if (!System.IO.File.Exists(pathA) || !System.IO.File.Exists(pathB))
                return BadRequest("One or both image paths do not exist on disk.");

            try
            {
                using var httpClient = new System.Net.Http.HttpClient();

                using var content = new System.Net.Http.MultipartFormDataContent();

                var bytesA = await System.IO.File.ReadAllBytesAsync(pathA, cancellationToken);
                var bytesB = await System.IO.File.ReadAllBytesAsync(pathB, cancellationToken);

                var byteContentA = new System.Net.Http.ByteArrayContent(bytesA);
                byteContentA.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentA, "fileA", System.IO.Path.GetFileName(pathA));

                var byteContentB = new System.Net.Http.ByteArrayContent(bytesB);
                byteContentB.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentB, "fileB", System.IO.Path.GetFileName(pathB));

                using var response = await httpClient.PostAsync(_comparisonServiceUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Comparison service returned {Status}: {Body}", response.StatusCode, responseBody);
                    return StatusCode(502, new { error = "Comparison service error", details = responseBody });
                }

                try
                {
                    var parsed = JsonSerializer.Deserialize<object>(responseBody);
                    return Ok(parsed ?? new { });
                }
                catch (JsonException)
                {
                    _logger.LogError("Comparison service returned invalid JSON: {Body}", responseBody);
                    return StatusCode(502, new { error = "Invalid JSON from comparison service", raw = responseBody });
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499); // client closed request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling comparison service");
                return StatusCode(500, new { error = "Internal error", details = ex.Message });
            }
        }
    }
}