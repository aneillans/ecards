using ECards.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECards.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ECardsController : ControllerBase
{
    private readonly Data.ECardsDbContext _context;
    private readonly Services.IFileStorageService _fileStorage;
    private readonly ILogger<ECardsController> _logger;
    private readonly IConfiguration _configuration;
    
    public ECardsController(
        Data.ECardsDbContext context, 
        Services.IFileStorageService fileStorage,
        ILogger<ECardsController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
        _configuration = configuration;
    }
    
    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<object> GetConfig()
    {
        var appName = _configuration["AppName"];
        _logger.LogInformation("AppName from config: {AppName}", appName ?? "NULL");
        return new
        {
            appName = appName ?? "eCards"
        };
    }
    
    [HttpGet("my-cards")]
    [Authorize]
    public async Task<ActionResult<List<object>>> GetMyCards([FromQuery] string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Email is required");
        }
        
        // Verify the requesting user matches the email parameter
        var userEmail = User.FindFirst("email")?.Value ?? 
                       User.FindFirst("preferred_username")?.Value;
        
        if (!email.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }
        
        var sender = await _context.Senders
            .Include(s => s.ECards)
            .FirstOrDefaultAsync(s => s.Email == email);
        
        if (sender == null)
        {
            return Ok(new List<object>());
        }
        
        var cards = sender.ECards
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new
            {
                c.Id,
                c.RecipientName,
                c.RecipientEmail,
                c.Message,
                c.CreatedDate,
                c.ScheduledSendDate,
                c.IsSent,
                c.SentDate,
                c.FirstViewedDate,
                c.ViewCount,
                c.ExpiryDate
            })
            .ToList();
        
        return Ok(cards);
    }
    
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ECard>> CreateECard([FromForm] CreateECardRequest request)
    {
        // Find or create sender
        var sender = await _context.Senders
            .FirstOrDefaultAsync(s => s.Email == request.SenderEmail);
        
        if (sender == null)
        {
            sender = new Sender
            {
                Id = Guid.NewGuid(),
                Name = request.SenderName,
                Email = request.SenderEmail,
                CreatedDate = DateTime.UtcNow
            };
            _context.Senders.Add(sender);
        }
        
        var ecard = new ECard
        {
            Id = Guid.NewGuid(),
            RecipientName = request.RecipientName,
            RecipientEmail = request.RecipientEmail,
            Message = request.Message,
            SenderId = sender.Id,
            CreatedDate = DateTime.UtcNow,
            ScheduledSendDate = request.ScheduledSendDate,
            PremadeArtId = request.PremadeArtId,
            IsSent = false,
            ViewCount = 0
        };
        
        // Handle custom art upload
        if (request.CustomArt != null)
        {
            var artPath = await _fileStorage.SaveCustomArtAsync(
                request.CustomArt.OpenReadStream(), 
                request.CustomArt.FileName);
            ecard.CustomArtPath = artPath;
        }
        
        // Calculate expiry date
        if (request.ScheduledSendDate.HasValue)
        {
            // 30 days from creation if not sent yet
            ecard.ExpiryDate = ecard.CreatedDate.AddDays(30);
        }
        else
        {
            // No schedule means send ASAP - set schedule to now so background service picks it up
            ecard.ScheduledSendDate = DateTime.UtcNow;
            ecard.ExpiryDate = ecard.CreatedDate.AddDays(14);
        }
        
        _context.ECards.Add(ecard);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created ecard {CardId}", ecard.Id);
        
        // Return a simplified DTO to avoid circular reference issues
        var result = new
        {
            ecard.Id,
            ecard.RecipientName,
            ecard.RecipientEmail,
            ecard.Message,
            ecard.CreatedDate,
            ecard.ScheduledSendDate,
            ecard.IsSent,
            ecard.SentDate,
            ecard.CustomArtPath,
            ecard.PremadeArtId,
            ecard.ExpiryDate
        };
        
        return CreatedAtAction(nameof(GetECard), new { id = ecard.Id }, result);
    }
    
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult> GetECard(Guid id)
    {
        var ecard = await _context.ECards
            .Include(e => e.Sender)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (ecard == null)
        {
            return NotFound();
        }
        
        // Return DTO to avoid circular reference
        var result = new
        {
            ecard.Id,
            ecard.RecipientName,
            ecard.RecipientEmail,
            ecard.Message,
            ecard.CreatedDate,
            ecard.ScheduledSendDate,
            ecard.IsSent,
            ecard.SentDate,
            ecard.CustomArtPath,
            ecard.PremadeArtId,
            ecard.ExpiryDate,
            ecard.ViewCount,
            ecard.FirstViewedDate,
            Sender = new
            {
                ecard.Sender.Name,
                ecard.Sender.Email
            }
        };
        
        return Ok(result);
    }
    
    [HttpGet("{id}/view")]
    public async Task<IActionResult> ViewECard(Guid id)
    {
        var ecard = await _context.ECards
            .Include(e => e.Sender)
            .FirstOrDefaultAsync(e => e.Id == id);
        
        if (ecard == null)
        {
            return NotFound("ECard not found");
        }
        
        // Record view audit
        var audit = new ViewAudit
        {
            Id = Guid.NewGuid(),
            ECardId = ecard.Id,
            ViewedDate = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };
        _context.ViewAudits.Add(audit);
        
        // Update view count
        ecard.ViewCount++;
        
        // Update first viewed date and expiry
        if (!ecard.FirstViewedDate.HasValue)
        {
            ecard.FirstViewedDate = DateTime.UtcNow;
            // Update expiry to 14 days from first view
            ecard.ExpiryDate = ecard.FirstViewedDate.Value.AddDays(14);
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("ECard {CardId} viewed (total views: {ViewCount})", ecard.Id, ecard.ViewCount);
        
        var appName = _configuration["AppName"] ?? "eCards";
        
        // Return HTML page with the ecard
        var artPath = ecard.CustomArtPath ?? ecard.PremadeArtId;
        var artHtml = string.IsNullOrEmpty(artPath) ? "" : $"<div class=\"art\">🎨 Art: {artPath}</div>";
        
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>eCard from {ecard.Sender.Name} - {appName}</title>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }}
        .card {{ background: white; max-width: 600px; margin: 0 auto; padding: 30px; border-radius: 10px; box-shadow: 0 10px 30px rgba(0,0,0,0.2); }}
        .art {{ max-width: 100%; height: auto; margin: 20px 0; border-radius: 5px; }}
        .message {{ font-size: 18px; margin: 20px 0; white-space: pre-wrap; }}
        .from {{ margin-top: 30px; font-style: italic; color: #666; }}
        .footer {{ margin-top: 30px; font-size: 12px; color: #999; }}
    </style>
</head>
<body>
    <div class=""card"">
        <h1>🎉 You've received an eCard! 🎉</h1>
        {artHtml}
        <div class=""message"">{ecard.Message}</div>
        <div class=""from"">From: {ecard.Sender.Name} ({ecard.Sender.Email})</div>
        <div class=""from"">To: {ecard.RecipientName}</div>
    </div>
</body>
</html>";
        
        return Content(html, "text/html");
    }
    
    [HttpGet("{id}/art")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCardArt(Guid id)
    {
        var ecard = await _context.ECards.FindAsync(id);
        
        if (ecard == null)
        {
            return NotFound();
        }
        
        if (string.IsNullOrEmpty(ecard.CustomArtPath))
        {
            return NotFound("No custom art for this card");
        }
        
        try
        {
            var stream = await _fileStorage.GetArtAsync(ecard.CustomArtPath);
            var contentType = GetContentType(ecard.CustomArtPath);
            
            return File(stream, contentType);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Art file not found");
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
    
    [HttpGet("premade-art")]
    [Authorize]
    public ActionResult<List<string>> GetPremadeArt()
    {
        return _fileStorage.GetPremadeArtIds();
    }

    [HttpPost("{id}/resend")]
    [Authorize]
    public async Task<IActionResult> ResendECard(string id, [FromQuery] string senderEmail)
    {
        if (string.IsNullOrEmpty(senderEmail))
            return BadRequest("Sender email is required");

        if (!Guid.TryParse(id, out var cardId))
            return BadRequest("Invalid card ID");

        try
        {
            var card = await _context.ECards
                .Include(e => e.Sender)
                .FirstOrDefaultAsync(e => e.Id == cardId);

            if (card == null)
                return NotFound("eCard not found");

            // Verify the sender owns this card
            if (!card.Sender.Email.Equals(senderEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            // Get email service from DI
            var emailService = HttpContext.RequestServices.GetRequiredService<Services.IEmailService>();
            
            // Send the email
            await emailService.SendECardNotificationAsync(card);
            
            // Update sent status
            card.IsSent = true;
            card.SentDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("User resent ecard {CardId} to {Recipient}", card.Id, card.RecipientEmail);
            
            return Ok(new { message = "Email resent successfully", sentDate = card.SentDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending ecard {CardId}", id);
            return StatusCode(500, new { message = "Failed to resend email: " + ex.Message });
        }
    }
}

public class CreateECardRequest
{
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? ScheduledSendDate { get; set; }
    public IFormFile? CustomArt { get; set; }
    public string? PremadeArtId { get; set; }
}
