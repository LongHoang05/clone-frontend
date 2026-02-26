using Microsoft.AspNetCore.Mvc;
using FrontendClonerApi.Services;

namespace FrontendClonerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CloneController : ControllerBase
{
    private readonly IClonerService _clonerService;

    public CloneController(IClonerService clonerService)
    {
        _clonerService = clonerService;
    }

    public class CloneRequest
    {
        public string Url { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public bool DeepScan { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CloneRequest request)
    {
        if (string.IsNullOrEmpty(request.Url))
            return BadRequest("URL is required.");

        try
        {
            var zipPath = await _clonerService.CloneWebsiteAsync(request.Url, request.ConnectionId, request.DeepScan);
            var uri = new Uri(request.Url);
            var downloadFileName = $"{uri.Host}-clone.zip";
            var tempFileName = Path.GetFileNameWithoutExtension(zipPath); // just the GUID, no .zip
            
            return Ok(new { 
                downloadUrl = $"/api/clone/download/{tempFileName}.zip",
                fileName = downloadFileName
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred during cloning: {ex.Message}");
        }
    }

    // URL ends in .zip so Chrome always saves with .zip extension
    [HttpGet("download/{tempFileName}.zip")]
    public IActionResult Download(string tempFileName, [FromQuery] string? fileName = null)
    {
        // The actual file on disk has .zip extension
        var baseTempDir = Path.Combine(Path.GetTempPath(), "FrontendCloner");
        var tempFilePath = Path.Combine(baseTempDir, tempFileName + ".zip");
        if (!System.IO.File.Exists(tempFilePath))
        {
            tempFilePath = Path.Combine(baseTempDir, tempFileName);
            if (!System.IO.File.Exists(tempFilePath))
                return NotFound("File not found or expired.");
        }

        // Use the provided friendly fileName if given, otherwise fall back to UUID.zip
        var safeFileName = !string.IsNullOrWhiteSpace(fileName) ? fileName : (Path.GetFileNameWithoutExtension(tempFileName) + ".zip");
        if (!safeFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            safeFileName += ".zip";
            
        // Apply Content-Disposition header aggressively
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{safeFileName}\"");

        var bytes = System.IO.File.ReadAllBytes(tempFilePath);
        try { System.IO.File.Delete(tempFilePath); } catch { }

        // Use the explicit file name parameter in File() which also adds its own Content-Disposition
        return File(bytes, "application/zip", "frontend-clone.zip");
    }
}
