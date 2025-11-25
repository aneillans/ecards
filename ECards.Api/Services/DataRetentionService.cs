using Microsoft.EntityFrameworkCore;

namespace ECards.Api.Services;

public class DataRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    
    public DataRetentionService(IServiceProvider serviceProvider, ILogger<DataRetentionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Retention Service starting...");
        
        // Wait for database to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        _logger.LogInformation("Data Retention Service now active");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredCardsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data retention cleanup");
            }
            
            await Task.Delay(_interval, stoppingToken);
        }
    }
    
    private async Task CleanupExpiredCardsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.ECardsDbContext>();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        
        var now = DateTime.UtcNow;
        var expiredCards = await dbContext.ECards
            .Where(e => e.ExpiryDate <= now)
            .ToListAsync(cancellationToken);
        
        foreach (var card in expiredCards)
        {
            // Delete custom art if exists
            if (!string.IsNullOrEmpty(card.CustomArtPath))
            {
                try
                {
                    await fileService.DeleteArtAsync(card.CustomArtPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete art file for card {CardId}", card.Id);
                }
            }
            
            dbContext.ECards.Remove(card);
        }
        
        if (expiredCards.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {Count} expired ecards", expiredCards.Count);
        }
    }
}
