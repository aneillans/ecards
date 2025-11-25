using ECards.Api.Models;
using ECards.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECards.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(ITemplateService templateService, ILogger<TemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all available premade templates
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<PremadeTemplate>>> GetTemplates()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates");
            return StatusCode(500, new { message = "Failed to retrieve templates" });
        }
    }

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<PremadeTemplate>> GetTemplate(string id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound(new { message = "Template not found" });
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId}", id);
            return StatusCode(500, new { message = "Failed to retrieve template" });
        }
    }
    
    /// <summary>
    /// Get template image
    /// </summary>
    [HttpGet("{id}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplateImage(string id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null || string.IsNullOrEmpty(template.ImagePath))
            {
                return NotFound(new { message = "Template image not found" });
            }
            
            if (!System.IO.File.Exists(template.ImagePath))
            {
                _logger.LogWarning("Template image file not found: {ImagePath}", template.ImagePath);
                return NotFound(new { message = "Image file not found on disk" });
            }
            
            var contentType = GetContentType(template.ImagePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(template.ImagePath);
            
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template image {TemplateId}", id);
            return StatusCode(500, new { message = "Failed to retrieve template image" });
        }
    }
    
    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}
