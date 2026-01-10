using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace Hostr.Api.Controllers;

[ApiController]
[Route("g")]
public class RedirectController : ControllerBase
{
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(ILogger<RedirectController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Decodes redirect token and redirects to tenant subdomain
    /// </summary>
    /// <param name="token">Base64-encoded JSON: {"t":"tenant-slug","p":"path"}</param>
    [HttpGet("{token}")]
    public IActionResult RedirectToTenant(string token)
    {
        try
        {
            // Decode Base64 token
            var jsonBytes = Convert.FromBase64String(token);
            var json = Encoding.UTF8.GetString(jsonBytes);

            // Parse JSON
            var data = JsonSerializer.Deserialize<RedirectData>(json);

            if (data == null || string.IsNullOrEmpty(data.Tenant) || string.IsNullOrEmpty(data.Path))
            {
                _logger.LogWarning("Invalid redirect token: {Token}", token);
                return BadRequest("Invalid redirect link");
            }

            // Build target URL
            var targetUrl = $"https://{data.Tenant}.staybot.co.za/{data.Path}";

            _logger.LogInformation("Redirecting to {Url} from token", targetUrl);

            // 302 Redirect
            return Redirect(targetUrl);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid Base64 token: {Token}", token);
            return BadRequest("Invalid redirect link format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing redirect token: {Token}", token);
            return StatusCode(500, "Error processing redirect");
        }
    }
}

public class RedirectData
{
    [System.Text.Json.Serialization.JsonPropertyName("t")]
    public string Tenant { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("p")]
    public string Path { get; set; } = string.Empty;
}
