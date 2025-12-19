using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using GameApp.Application.Models;
using Xunit;

namespace GameApp.Tests.Integration;

public class GalleryWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    public readonly string Root = Path.Combine(Path.GetTempPath(), "gallery_temp_" + Guid.NewGuid());
    public string WebRoot => Path.Combine(Root, "wwwroot");
    public string ImagesRoot => Path.Combine(WebRoot, "images");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(ImagesRoot);

        builder.UseEnvironment("Development"); // or a custom name
        builder.UseWebRoot(WebRoot);
        builder.UseContentRoot(Root);

        // Force the application to use our temp images root instead of appsettings.json
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Gallery:ImagesRoot"] = ImagesRoot
            };
            cfg.AddInMemoryCollection(overrides!);
        });
    }

    public new void Dispose()
    {
        base.Dispose();
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); } catch { }
    }
}

public class GalleryControllerIntegrationTests : IClassFixture<GalleryWebApplicationFactory>
{
    private readonly GalleryWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GalleryControllerIntegrationTests(GalleryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Empty_Then_Upload_Then_List_NotEmpty()
    {
        // Manual existence check + cleanup
        if (!Directory.Exists(_factory.ImagesRoot))
            Directory.CreateDirectory(_factory.ImagesRoot);

        var preExisting = Directory.EnumerateFiles(_factory.ImagesRoot).ToList();
        // Optional: write to test output if needed
        Assert.True(preExisting.Count == 0, $"Images directory not empty at start: {string.Join(", ", preExisting)}");

        foreach (var path in preExisting)
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        // 1. creating empty gallery
        var listResp1 = await _client.GetAsync("/gallery");
        Assert.Equal(HttpStatusCode.OK, listResp1.StatusCode);
        var list1 = await listResp1.Content.ReadFromJsonAsync<List<ImageResponse>>();
        Assert.NotNull(list1);
        Assert.Empty(list1!);

        // 2. random when empty returns error (0 images in gallery)
        var randomEmpty = await _client.GetAsync("/gallery/random");
        Assert.Equal(HttpStatusCode.NotFound, randomEmpty.StatusCode);

        // 3. upload an image (creates 1 image in gallery)
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "File", "test.jpg");

        var uploadResp = await _client.PostAsync("/gallery", content);
        Assert.Equal(HttpStatusCode.Created, uploadResp.StatusCode);
        var uploaded = await uploadResp.Content.ReadFromJsonAsync<ImageResponse>();
        Assert.NotNull(uploaded);
        Assert.EndsWith(".jpg", uploaded!.Id);

        // file saved physically
        var savedFilePath = Path.Combine(_factory.ImagesRoot, uploaded.Id!);
        Assert.True(File.Exists(savedFilePath), $"Expected saved file at {savedFilePath}");

        // 4. list now returns one
        var listResp2 = await _client.GetAsync("/gallery");
        Assert.Equal(HttpStatusCode.OK, listResp2.StatusCode);
        var list2 = await listResp2.Content.ReadFromJsonAsync<List<ImageResponse>>();
        Assert.NotNull(list2);
        Assert.Single(list2!);

        // 5. random now succeeds (finds the 1 image in gallery)
        var randomNow = await _client.GetAsync("/gallery/random");
        Assert.Equal(HttpStatusCode.OK, randomNow.StatusCode);
        var randomDto = await randomNow.Content.ReadFromJsonAsync<ImageResponse>();
        Assert.NotNull(randomDto);
    }
}