using ECards.Api.Data;
using ECards.Api.Models;
using ECards.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECards.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly ECardsDbContext _db;
    private readonly ITemplateService _templateService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ECardsDbContext db, ITemplateService templateService, ILogger<AdminController> logger)
    {
        _db = db;
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet("ecards")]
    public async Task<ActionResult<IEnumerable<object>>> GetECards([FromQuery] int take = 100)
    {
        try
        {
            var cards = await _db.ECards
                .Include(e => e.Sender)
                .OrderByDescending(e => e.CreatedDate)
                .Take(take)
                .Select(e => new
                {
                    e.Id,
                    e.RecipientName,
                    e.RecipientEmail,
                    e.Message,
                    e.PremadeArtId,
                    e.CustomArtPath,
                    e.ScheduledSendDate,
                    e.IsSent,
                    e.SentDate,
                    e.CreatedDate,
                    e.ViewCount,
                    Sender = new { e.Sender.Id, e.Sender.Name, e.Sender.Email }
                })
                .ToListAsync();

            return Ok(cards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ecards for admin");
            return StatusCode(500, new { message = "Failed to retrieve ecards" });
        }
    }

    [HttpGet("viewaudits")]
    public async Task<ActionResult<IEnumerable<ViewAudit>>> GetViewAudits([FromQuery] int take = 200)
    {
        try
        {
            var audits = await _db.ViewAudits
                .OrderByDescending(v => v.ViewedDate)
                .Take(take)
                .ToListAsync();
            return Ok(audits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching view audits");
            return StatusCode(500, new { message = "Failed to retrieve view audits" });
        }
    }

    // Template management
    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<PremadeTemplate>>> GetTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return Ok(templates);
    }

    [HttpPost("templates")]
    public async Task<ActionResult<PremadeTemplate>> CreateTemplate([FromBody] PremadeTemplate template)
    {
        if (template == null) return BadRequest();
        var created = await _templateService.CreateTemplateAsync(template);
        return CreatedAtAction(nameof(GetTemplates), new { id = created.Id }, created);
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] PremadeTemplate template)
    {
        var ok = await _templateService.UpdateTemplateAsync(id, template);
        if (!ok) return NotFound(new { message = "Template not found" });
        return NoContent();
    }

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var ok = await _templateService.DeleteTemplateAsync(id);
        if (!ok) return NotFound(new { message = "Template not found" });
        return NoContent();
    }

    [HttpPost("ecards/{id}/resend")]
    public async Task<IActionResult> ResendECard(string id)
    {
        if (!Guid.TryParse(id, out var cardId))
            return BadRequest(new { message = "Invalid card ID" });

        try
        {
            var card = await _db.ECards
                .Include(e => e.Sender)
                .FirstOrDefaultAsync(e => e.Id == cardId);

            if (card == null)
                return NotFound(new { message = "eCard not found" });

            // Get email service from DI
            var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
            
            // Send the email
            await emailService.SendECardNotificationAsync(card);
            
            // Update sent status
            card.IsSent = true;
            card.SentDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Admin resent ecard {CardId} to {Recipient}", card.Id, card.RecipientEmail);
            
            return Ok(new { message = "Email resent successfully", sentDate = card.SentDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending ecard {CardId}", id);
            return StatusCode(500, new { message = "Failed to resend email: " + ex.Message });
        }
    }

    [HttpDelete("ecards/{id}")]
    public async Task<IActionResult> DeleteECard(string id)
    {
        if (!Guid.TryParse(id, out var cardId))
            return BadRequest(new { message = "Invalid card ID" });

        try
        {
            var card = await _db.ECards
                .Include(e => e.ViewAudits)
                .FirstOrDefaultAsync(e => e.Id == cardId);

            if (card == null)
                return NotFound(new { message = "eCard not found" });

            // Delete custom art file if exists
            if (!string.IsNullOrEmpty(card.CustomArtPath))
            {
                try
                {
                    var fileStorage = HttpContext.RequestServices.GetRequiredService<IFileStorageService>();
                    await fileStorage.DeleteArtAsync(card.CustomArtPath);
                    _logger.LogInformation("Deleted custom art file: {FilePath}", card.CustomArtPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete custom art file: {FilePath}", card.CustomArtPath);
                    // Continue with deletion even if file deletion fails
                }
            }

            // Delete related view audits
            if (card.ViewAudits.Any())
            {
                _db.ViewAudits.RemoveRange(card.ViewAudits);
            }

            // Delete the card
            _db.ECards.Remove(card);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Admin deleted ecard {CardId}", cardId);
            
            return Ok(new { message = "eCard deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ecard {CardId}", id);
            return StatusCode(500, new { message = "Failed to delete eCard: " + ex.Message });
        }
    }
}
