using ECards.Api.Data;
using ECards.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECards.Api.Services;

public interface ITemplateService
{
    Task<List<PremadeTemplate>> GetAllTemplatesAsync();
    Task<PremadeTemplate?> GetTemplateByIdAsync(string templateId);
    Task<PremadeTemplate> CreateTemplateAsync(PremadeTemplate template);
    Task<bool> UpdateTemplateAsync(string templateId, PremadeTemplate template);
    Task<bool> DeleteTemplateAsync(string templateId);
}

public class TemplateService : ITemplateService
{
    private readonly ECardsDbContext _db;

    public TemplateService(ECardsDbContext db)
    {
        _db = db;
    }

    public async Task<List<PremadeTemplate>> GetAllTemplatesAsync()
    {
        return await _db.PremadeTemplates.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<PremadeTemplate?> GetTemplateByIdAsync(string templateId)
    {
        return await _db.PremadeTemplates.FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive);
    }

    public async Task<PremadeTemplate> CreateTemplateAsync(PremadeTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Id)) template.Id = Guid.NewGuid().ToString();
        _db.PremadeTemplates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task<bool> UpdateTemplateAsync(string templateId, PremadeTemplate template)
    {
        var existing = await _db.PremadeTemplates.FirstOrDefaultAsync(t => t.Id == templateId);
        if (existing == null) return false;
        existing.Name = template.Name;
        existing.Category = template.Category;
        existing.IconEmoji = template.IconEmoji;
        existing.Description = template.Description;
        existing.ImagePath = template.ImagePath;
        existing.IsActive = template.IsActive;
        existing.SortOrder = template.SortOrder;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(string templateId)
    {
        var existing = await _db.PremadeTemplates.FirstOrDefaultAsync(t => t.Id == templateId);
        if (existing == null) return false;
        existing.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
}
