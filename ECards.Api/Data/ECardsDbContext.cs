using ECards.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ECards.Api.Data;

public class ECardsDbContext : DbContext
{
    public ECardsDbContext(DbContextOptions<ECardsDbContext> options) : base(options)
    {
    }
    
    public DbSet<ECard> ECards { get; set; }
    public DbSet<Sender> Senders { get; set; }
    public DbSet<ViewAudit> ViewAudits { get; set; }
    public DbSet<PremadeTemplate> PremadeTemplates { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // ECard configuration
        modelBuilder.Entity<ECard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipientName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CustomArtPath).HasMaxLength(500);
            entity.Property(e => e.PremadeArtId).HasMaxLength(100);
            
            entity.HasOne(e => e.Sender)
                .WithMany(s => s.ECards)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.ExpiryDate);
            entity.HasIndex(e => e.ScheduledSendDate);
        });
        
        // Sender configuration
        modelBuilder.Entity<Sender>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(s => s.Email);
        });
        
        // ViewAudit configuration
        modelBuilder.Entity<ViewAudit>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.IpAddress).HasMaxLength(45);
            entity.Property(v => v.UserAgent).HasMaxLength(500);
            
            entity.HasOne(v => v.ECard)
                .WithMany(e => e.ViewAudits)
                .HasForeignKey(v => v.ECardId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(v => v.ViewedDate);
        });

        // PremadeTemplate configuration
        modelBuilder.Entity<PremadeTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Category).IsRequired().HasMaxLength(100);
            entity.Property(t => t.IconEmoji).IsRequired().HasMaxLength(10);
            entity.Property(t => t.Description).HasMaxLength(1000);
            entity.Property(t => t.ImagePath).HasMaxLength(500);
            entity.Property(t => t.IsActive).HasDefaultValue(true);
            entity.HasIndex(t => t.SortOrder);
        });
    }
}
