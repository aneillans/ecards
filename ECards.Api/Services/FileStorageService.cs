namespace ECards.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveCustomArtAsync(Stream fileStream, string fileName);
    Task<Stream> GetArtAsync(string path);
    Task DeleteArtAsync(string path);
    List<string> GetPremadeArtIds();
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _customArtPath;
    private readonly string _premadeArtPath;
    private readonly ILogger<LocalFileStorageService> _logger;
    
    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _customArtPath = configuration["Storage:CustomArtPath"] ?? "/app/storage/custom";
        _premadeArtPath = configuration["Storage:PremadeArtPath"] ?? "/app/storage/premade";
        
        // Ensure directories exist
        Directory.CreateDirectory(_customArtPath);
        Directory.CreateDirectory(_premadeArtPath);
    }
    
    public async Task<string> SaveCustomArtAsync(Stream fileStream, string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var filePath = Path.Combine(_customArtPath, uniqueFileName);
        
        using var fileStreamOutput = File.Create(filePath);
        await fileStream.CopyToAsync(fileStreamOutput);
        
        _logger.LogInformation("Saved custom art to {FilePath}", filePath);
        return filePath;
    }
    
    public Task<Stream> GetArtAsync(string path)
    {
        if (!File.Exists(path))
        {
            // Try premade art
            var premadePath = Path.Combine(_premadeArtPath, path);
            if (File.Exists(premadePath))
            {
                return Task.FromResult<Stream>(File.OpenRead(premadePath));
            }
            throw new FileNotFoundException($"Art file not found: {path}");
        }
        
        return Task.FromResult<Stream>(File.OpenRead(path));
    }
    
    public Task DeleteArtAsync(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Deleted art file {FilePath}", path);
        }
        
        return Task.CompletedTask;
    }
    
    public List<string> GetPremadeArtIds()
    {
        if (!Directory.Exists(_premadeArtPath))
        {
            return new List<string>();
        }
        
        return Directory.GetFiles(_premadeArtPath)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();
    }
}
