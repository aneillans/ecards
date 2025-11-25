namespace ECards.Api.Models;

public class ViewAudit
{
    public Guid Id { get; set; }
    public Guid ECardId { get; set; }
    public ECard ECard { get; set; } = null!;
    
    public DateTime ViewedDate { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
