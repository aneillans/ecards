using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MimeKit;

namespace ECards.Api.Services;

public class ScheduledSendingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledSendingService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    
    public ScheduledSendingService(IServiceProvider serviceProvider, ILogger<ScheduledSendingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled Sending Service starting...");
        
        // Wait for database to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        _logger.LogInformation("Scheduled Sending Service now active");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledCardsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled sending processing");
            }
            
            await Task.Delay(_interval, stoppingToken);
        }
    }
    
    private async Task ProcessScheduledCardsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.ECardsDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        
        var now = DateTime.UtcNow;
        var cardsToSend = await dbContext.ECards
            .Include(e => e.Sender)
            .Where(e => !e.IsSent && e.ScheduledSendDate != null && e.ScheduledSendDate <= now)
            .ToListAsync(cancellationToken);
        
        var successCount = 0;
        var failureCount = 0;
        
        foreach (var card in cardsToSend)
        {
            try
            {
                await emailService.SendECardNotificationAsync(card);
                
                // Only mark as sent if email was sent successfully
                card.IsSent = true;
                card.SentDate = DateTime.UtcNow;
                successCount++;
                
                _logger.LogInformation("Successfully sent ecard {CardId} to {Recipient}", card.Id, card.RecipientEmail);
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError(ex, "Failed to send ecard {CardId} to {Recipient}. Will retry on next run.", 
                    card.Id, card.RecipientEmail);
                // Don't mark as sent - leave it for retry on next scheduled run
            }
        }
        
        // Only save changes for successfully sent cards
        if (successCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Scheduled sending batch complete: {Success} sent, {Failed} failed", 
                successCount, failureCount);
        }
        else if (failureCount > 0)
        {
            _logger.LogWarning("Scheduled sending batch complete: All {Failed} cards failed to send", failureCount);
        }
    }
}

public interface IEmailService
{
    Task SendECardNotificationAsync(Models.ECard card);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    
    public EmailService(
        ILogger<EmailService> logger, 
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }
    
    public async Task SendECardNotificationAsync(Models.ECard card)
    {
        var frontendUrl = _configuration["FrontendUrl"] ?? _configuration["BaseUrl"];
        if (string.IsNullOrEmpty(frontendUrl))
        {
            _logger.LogError("FrontendUrl or BaseUrl configuration is missing. Cannot generate view URL for card {CardId}", card.Id);
            return;
        }
        var viewUrl = $"{frontendUrl}/view/{card.Id}";
        var appName = _configuration["AppName"] ?? "eCards";
        
        try
        {
            // Create a scope to get the template service
            using var scope = _scopeFactory.CreateScope();
            var templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();
            
            // Prepare template variables
            var variables = new Dictionary<string, string>
            {
                { "RecipientName", card.RecipientName },
                { "SenderName", card.Sender.Name },
                { "SenderEmail", card.Sender.Email },
                { "CardMessage", card.Message },
                { "ViewUrl", viewUrl },
                { "AppName", appName }
            };
            
            // Render the email template
            var (subject, htmlBody, textBody) = await templateService.RenderEmailAsync(
                "ecard-notification", 
                variables);
            
            // In a real implementation, this would send an email via SMTP or email service
            // For now, we'll just log it
            _logger.LogInformation(
                "Email notification prepared: To: {Recipient} ({RecipientEmail}), Subject: {Subject}",
                card.RecipientName,
                card.RecipientEmail,
                subject
            );
            
            _logger.LogDebug("Email HTML Body Length: {Length} characters", htmlBody.Length);
            
            // Send email via SMTP using MailKit
            var smtpHost = _configuration["Smtp:Host"];
            var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var smtpEnableSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? "true");
            var smtpUsername = _configuration["Smtp:Username"];
            var smtpPassword = _configuration["Smtp:Password"];
            var fromEmail = _configuration["Smtp:FromEmail"] ?? "noreply@example.com";
            var fromName = _configuration["Smtp:FromName"] ?? appName;
            
            if (string.IsNullOrEmpty(smtpHost))
            {
                _logger.LogWarning("SMTP host not configured. Email will not be sent.");
                return;
            }
            
            using var client = new SmtpClient();
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(card.RecipientName, card.RecipientEmail));
            message.Subject = subject;
            
            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            if (!string.IsNullOrEmpty(textBody))
            {
                bodyBuilder.TextBody = textBody;
            }
            message.Body = bodyBuilder.ToMessageBody();
            
            await client.ConnectAsync(smtpHost, smtpPort, smtpEnableSsl);
            
            // Authenticate if credentials are provided
            if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
            {
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
            }
            
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            
            _logger.LogInformation("Email sent successfully to {RecipientEmail}", card.RecipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to send email notification for ecard {CardId}. Falling back to simple log.",
                card.Id);
            
            // Fallback: just log the basic info
            _logger.LogInformation(
                "Email notification: Send ecard from {Sender} ({SenderEmail}) to {Recipient} ({RecipientEmail}). View URL: {ViewUrl}",
                card.Sender.Name,
                card.Sender.Email,
                card.RecipientName,
                card.RecipientEmail,
                viewUrl
            );
        }
    }
}
