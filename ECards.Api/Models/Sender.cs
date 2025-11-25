namespace ECards.Api.Models;

public class Sender
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    
    public ICollection<ECard> ECards { get; set; } = new List<ECard>();
}
