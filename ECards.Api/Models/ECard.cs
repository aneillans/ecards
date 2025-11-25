namespace ECards.Api.Models;

public class ECard
{
    public Guid Id { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    
    // Art settings
    public string? CustomArtPath { get; set; }
    public string? PremadeArtId { get; set; }
    
    // Scheduling
    public DateTime? ScheduledSendDate { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentDate { get; set; }
    
    // Tracking
    public DateTime CreatedDate { get; set; }
    public DateTime? FirstViewedDate { get; set; }
    public int ViewCount { get; set; }
    
    // Data lifecycle - for cleanup service
    public DateTime ExpiryDate { get; set; }
    
    // Sender information
    public Guid SenderId { get; set; }
    public Sender Sender { get; set; } = null!;
    
    // View audit trail
    public ICollection<ViewAudit> ViewAudits { get; set; } = new List<ViewAudit>();
}
