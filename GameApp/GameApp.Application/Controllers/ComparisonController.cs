using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using GameApp.Service;
using GameApp.Service.Services;
using Microsoft.AspNetCore.Http;

namespace GameApp.Application.Controllers
{
    [ApiController]
    [Route("comparison")]
    public class ComparisonController : ControllerBase
    {
        private readonly IGalleryService _gallery;
        private readonly ILogger<ComparisonController> _logger;
        private readonly string _comparisonServiceUrl;
        private readonly IWebHostEnvironment _env;

        public ComparisonController(IGalleryService gallery, ILogger<ComparisonController> logger, IWebHostEnvironment env)
        {
            _gallery = gallery;
            _logger = logger;
            _env = env;
            _comparisonServiceUrl = Environment.GetEnvironmentVariable("COMPARISON_SERVICE_URL")
                ?? "http://localhost:5000/compare";
        }

        public class ComparisonRequest
        {
            public string? ImageIdA { get; set; }
            public string? ImageIdB { get; set; }
            public string? ImagePathA { get; set; }
            public string? ImagePathB { get; set; }
        }

        public class ComparisonResult
        {
            public double? Score { get; set; }
            public string? Message { get; set; }
            public string? DiffImageUrl { get; set; }
            public object? Raw { get; set; }
        }

        private static bool LooksLikeImage(byte[]? data)
        {
            if (data == null || data.Length < 4) return false;

            if (data.Length >= 8 &&
                data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) // PNG
                return true;
            if (data[0] == 0xFF && data[1] == 0xD8) // JPEG
                return true;
            if (data.Length >= 4 &&
                data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'8') // GIF
                return true;
            if (data[0] == 0x42 && data[1] == 0x4D) // BMP
                return true;
            if (data.Length >= 4)
            {
                if (data[0] == (byte)'I' && data[1] == (byte)'I' && data[2] == 0x2A && data[3] == 0x00) return true;
                if (data[0] == (byte)'M' && data[1] == (byte)'M' && data[2] == 0x00 && data[3] == 0x2A) return true;
            }
            if (data.Length >= 12 &&
                data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' &&
                data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P') // WebP
                return true;

            return false;
        }

        private static bool IsImageExtension(string? ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            switch (ext.ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg": case ".webp":
                case ".bmp": case ".gif": case ".tif": case ".tiff":
                    return true;
                default: return false;
            }
        }

        // helper: save forwarded bytes for debugging and return hex prefix
        private async Task<(string filePath, string hexPrefix)> SaveDebugForwardAsync(string prefix, byte[] bytes, CancellationToken ct)
        {
            try
            {
                var debugDir = Path.Combine(_env.ContentRootPath ?? ".", "debug", "forwarded");
                Directory.CreateDirectory(debugDir);
                var fname = Path.Combine(debugDir, $"{prefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.bin");
                await System.IO.File.WriteAllBytesAsync(fname, bytes, ct);
                var hex = BitConverter.ToString(bytes.Take(Math.Min(16, bytes.Length)).ToArray()).Replace("-", " ");
                return (fname, hex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save debug forwarded file {Prefix}", prefix);
                return ("<failed>", "<no-hex>");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Compare([FromBody] ComparisonRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
                return BadRequest("Request body is required.");

            try
            {
                using var httpClient = new System.Net.Http.HttpClient();

                async Task<(byte[]? bytes, string? reason)> ResolveToBytesAsync(string? imagePath, string? imageId)
                {
                    if (!string.IsNullOrEmpty(imageId))
                    {
                        var filePath = _gallery.GetImageFilePath(imageId);
                        if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                        {
                            var bytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
                            if (!LooksLikeImage(bytes))
                            {
                                var ext = Path.GetExtension(filePath);
                                if (IsImageExtension(ext))
                                {
                                    _logger.LogInformation("Gallery file {Path} accepted by extension {Ext}.", filePath, ext);
                                    return (bytes, $"Resolved from gallery id '{imageId}' -> '{filePath}' (accepted by extension {ext})");
                                }
                                return (null, $"Gallery id '{imageId}' -> '{filePath}' but bytes do not look like an image (size={bytes.Length})");
                            }
                            return (bytes, $"Resolved from gallery id '{imageId}' -> '{filePath}'");
                        }
                        return (null, $"Gallery id '{imageId}' did not resolve to an existing file (GetImageFilePath returned '{filePath}')");
                    }

                    if (string.IsNullOrEmpty(imagePath))
                        return (null, "No imagePath or imageId provided");

                    if (imagePath.StartsWith("/"))
                    {
                        var relative = imagePath.TrimStart('/');
                        var candidate = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(candidate))
                        {
                            var bytes = await System.IO.File.ReadAllBytesAsync(candidate, cancellationToken);
                            if (!LooksLikeImage(bytes))
                            {
                                var ext = System.IO.Path.GetExtension(candidate);
                                if (IsImageExtension(ext))
                                {
                                    _logger.LogInformation("Server-relative file {Path} accepted by extension {Ext}.", candidate, ext);
                                    return (bytes, $"Resolved server-relative path '{imagePath}' -> '{candidate}' (accepted by extension {ext})");
                                }
                                return (null, $"Server-relative path '{imagePath}' mapped to '{candidate}' but bytes do not look like an image (size={bytes.Length})");
                            }
                            return (bytes, $"Resolved server-relative path '{imagePath}' -> '{candidate}'");
                        }
                        return (null, $"Server-relative path '{imagePath}' not found at '{candidate}'");
                    }

                    if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        try
                        {
                            var resp = await httpClient.GetAsync(uri, cancellationToken);
                            if (!resp.IsSuccessStatusCode)
                                return (null, $"HTTP GET {imagePath} returned {resp.StatusCode}");

                            var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                            var mediaType = resp.Content?.Headers?.ContentType?.MediaType;

                            if (mediaType != null)
                            {
                                if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !LooksLikeImage(bytes))
                                    return (null, $"Downloaded content from {imagePath} has content-type {mediaType} and bytes do not look like an image");
                            }
                            else if (!LooksLikeImage(bytes))
                            {
                                return (null, $"Downloaded bytes from {imagePath} do not look like an image (size={bytes.Length})");
                            }

                            return (bytes, $"Downloaded from URL {imagePath} (size={bytes.Length})");
                        }
                        catch (Exception ex)
                        {
                            return (null, $"Exception downloading {imagePath}: {ex.Message}");
                        }
                    }

                    if (System.IO.Path.IsPathRooted(imagePath))
                    {
                        if (System.IO.File.Exists(imagePath))
                        {
                            var bytes = await System.IO.File.ReadAllBytesAsync(imagePath, cancellationToken);
                            if (!LooksLikeImage(bytes))
                            {
                                var ext = System.IO.Path.GetExtension(imagePath);
                                if (IsImageExtension(ext))
                                {
                                    _logger.LogInformation("Filesystem path {Path} accepted by extension {Ext}.", imagePath, ext);
                                    return (bytes, $"Resolved filesystem path '{imagePath}' (accepted by extension {ext})");
                                }
                                return (null, $"Filesystem path '{imagePath}' exists but bytes do not look like an image (size={bytes.Length})");
                            }
                            return (bytes, $"Resolved filesystem path '{imagePath}'");
                        }
                        return (null, $"Filesystem path '{imagePath}' not found");
                    }

                    if (Uri.TryCreate("http://" + imagePath, UriKind.Absolute, out var maybeUri))
                    {
                        try
                        {
                            var bytes = await httpClient.GetByteArrayAsync(maybeUri, cancellationToken);
                            if (!LooksLikeImage(bytes))
                                return (null, $"Best-effort download from {maybeUri} did not yield image bytes");
                            return (bytes, $"Best-effort downloaded from {maybeUri}");
                        }
                        catch (Exception ex)
                        {
                            return (null, $"Best-effort download failed for {maybeUri}: {ex.Message}");
                        }
                    }

                    return (null, "Unknown imagePath format; not resolvable");
                }

                var (bytesA, reasonA) = await ResolveToBytesAsync(request.ImagePathA, request.ImageIdA);
                var (bytesB, reasonB) = await ResolveToBytesAsync(request.ImagePathB, request.ImageIdB);

                if (bytesA is null || bytesB is null)
                {
                    _logger.LogWarning("Image resolution failed. A: {AReason}; B: {BReason}", reasonA, reasonB);
                    return BadRequest(new
                    {
                        error = "Both images must be provided and resolvable to image bytes",
                        details = new
                        {
                            imageA = new { inputPath = request.ImagePathA, inputId = request.ImageIdA, reason = reasonA },
                            imageB = new { inputPath = request.ImagePathB, inputId = request.ImageIdB, reason = reasonB }
                        }
                    });
                }

                // --- DEBUG: save forwarded bytes to disk and log hex prefix ---
                var (savedA, hexA) = await SaveDebugForwardAsync("forward_fileA", bytesA, cancellationToken);
                var (savedB, hexB) = await SaveDebugForwardAsync("forward_fileB", bytesB, cancellationToken);
                _logger.LogInformation("Forwarding to comparison service: fileA saved={SavedA} size={SizeA} hex={HexA}; fileB saved={SavedB} size={SizeB} hex={HexB}",
                    savedA, bytesA.Length, hexA, savedB, bytesB.Length, hexB);
                // --- end debug ---

                using var content = new System.Net.Http.MultipartFormDataContent();
                var byteContentA = new System.Net.Http.ByteArrayContent(bytesA);
                byteContentA.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentA, "fileA", Path.GetFileName(savedA) ?? "fileA.bin");

                var byteContentB = new System.Net.Http.ByteArrayContent(bytesB);
                byteContentB.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentB, "fileB", Path.GetFileName(savedB) ?? "fileB.bin");

                using var response = await httpClient.PostAsync(_comparisonServiceUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Comparison service returned {Status}: {Body}", response.StatusCode, responseBody);
                    return StatusCode(502, new { error = "Comparison service error", details = responseBody });
                }

                var parsed = JsonSerializer.Deserialize<JsonElement>(responseBody);

                double score = 0;
                if (parsed.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetDouble(out var scoreVal))
                {
                    score = scoreVal;
                }

                return Ok(new
                {
                    score = Math.Round(score, 1) / 100,
                    message = "Congratulations"
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while calling comparison service");
                return StatusCode(500, new { error = "Internal error", details = ex.Message });
            }
        }

        // New: accept multipart/form-data with two files and forward to comparison service.
        [HttpPost("upload")]
        [RequestSizeLimit(20_000_000)] // 20 MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CompareMultipart([FromForm] IFormFile? fileA, [FromForm] IFormFile? fileB, CancellationToken cancellationToken)
        {
            if (fileA is null || fileB is null)
                return BadRequest("form-data must include 'fileA' and 'fileB'");

            try
            {
                using var msA = new MemoryStream();
                await fileA.CopyToAsync(msA, cancellationToken);
                var bytesA = msA.ToArray();

                using var msB = new MemoryStream();
                await fileB.CopyToAsync(msB, cancellationToken);
                var bytesB = msB.ToArray();

                if (bytesA.Length == 0 || bytesB.Length == 0)
                    return BadRequest("One or both uploaded files are empty.");

                using var httpClient = new System.Net.Http.HttpClient();
                using var content = new System.Net.Http.MultipartFormDataContent();

                var byteContentA = new System.Net.Http.ByteArrayContent(bytesA);
                byteContentA.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentA, "fileA", fileA.FileName ?? "fileA.png");

                var byteContentB = new System.Net.Http.ByteArrayContent(bytesB);
                byteContentB.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContentB, "fileB", fileB.FileName ?? "fileB.png");

                using var response = await httpClient.PostAsync(_comparisonServiceUrl, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Comparison service returned {Status}: {Body}", response.StatusCode, responseBody);
                    return StatusCode(502, new { error = "Comparison service error", details = responseBody });
                }

                try
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(responseBody);

                    double score = 0;
                    if (parsed.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetDouble(out var scoreVal))
                    {
                        score = scoreVal;
                    }

                    return Ok(new
                    {
                        score = Math.Round(score, 1)/ 100,
                        message = "Congratulations!"
                    });
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
                _logger.LogError(ex, "Error while forwarding multipart comparison request");
                return StatusCode(500, new { error = "Internal error", details = ex.Message });
            }
        }
    }
}