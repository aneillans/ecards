namespace ECards.Api.Services;

public class StorageOptions
{
    public string? CustomArtPath { get; set; }
    public string? PremadeArtPath { get; set; }
    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024; // 5 MB default
    public int MaxImageDimension { get; set; } = 1600; // pixels
}
