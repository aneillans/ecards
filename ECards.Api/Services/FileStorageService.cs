using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

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
    private readonly int _maxImageDimension;
    
    public LocalFileStorageService(IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        var opts = options.Value ?? new StorageOptions();
        _customArtPath = string.IsNullOrWhiteSpace(opts.CustomArtPath) ? "/app/storage/custom" : opts.CustomArtPath;
        _premadeArtPath = string.IsNullOrWhiteSpace(opts.PremadeArtPath) ? "/app/storage/premade" : opts.PremadeArtPath;
        _maxImageDimension = opts.MaxImageDimension > 0 ? opts.MaxImageDimension : 0;
        
        Directory.CreateDirectory(_customArtPath);
        Directory.CreateDirectory(_premadeArtPath);
    }
    
    public async Task<string> SaveCustomArtAsync(Stream fileStream, string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var ext = Path.GetExtension(safeFileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "upload";
        var uniqueSuffix = Guid.NewGuid().ToString("N");
        var uniqueFileName = $"{baseName}_{uniqueSuffix}{ext}";
        var filePath = Path.Combine(_customArtPath, uniqueFileName);
        
        try
        {
            using var image = await Image.LoadAsync(fileStream);
            IImageFormat format = image.Metadata.DecodedImageFormat ?? PngFormat.Instance;
            if (_maxImageDimension > 0 && (image.Width > _maxImageDimension || image.Height > _maxImageDimension))
            {
                var target = new Size(_maxImageDimension, _maxImageDimension);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = target
                }));
            }
            await using var output = File.Create(filePath);
            await image.SaveAsync(output, format);
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Unsupported image format for {FileName}", safeFileName);
            throw new InvalidDataException("Unsupported image format", ex);
        }
        catch (ImageFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid image data for {FileName}", safeFileName);
            throw new InvalidDataException("Invalid image data", ex);
        }

        _logger.LogInformation("Saved custom art to {FilePath}", filePath);
        return filePath;
    }
    
    public Task<Stream> GetArtAsync(string path)
    {
        if (!File.Exists(path))
        {
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
