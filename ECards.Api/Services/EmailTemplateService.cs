using HandlebarsDotNet;

namespace ECards.Api.Services;

public interface IEmailTemplateService
{
    Task<(string subject, string htmlBody, string? textBody)> RenderEmailAsync(string templateKey, Dictionary<string, string> variables);
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IHandlebars _handlebars;
    private readonly string _templatesPath;

    public EmailTemplateService(
        ILogger<EmailTemplateService> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
        _templatesPath = Path.Combine(_environment.ContentRootPath, "EmailTemplates");
        
        // Initialize Handlebars
        _handlebars = Handlebars.Create();
    }

    public async Task<(string subject, string htmlBody, string? textBody)> RenderEmailAsync(
        string templateKey, 
        Dictionary<string, string> variables)
    {
        try
        {
            // Load templates from files
            var subjectPath = Path.Combine(_templatesPath, $"{templateKey}.subject.hbs");
            var htmlPath = Path.Combine(_templatesPath, $"{templateKey}.html.hbs");
            var textPath = Path.Combine(_templatesPath, $"{templateKey}.text.hbs");

            if (!File.Exists(subjectPath) || !File.Exists(htmlPath))
            {
                _logger.LogWarning("Email template files for '{TemplateKey}' not found", templateKey);
                throw new InvalidOperationException($"Email template '{templateKey}' not found");
            }

            // Read and compile templates
            var subjectTemplate = await File.ReadAllTextAsync(subjectPath);
            var htmlTemplate = await File.ReadAllTextAsync(htmlPath);
            var textTemplate = File.Exists(textPath) ? await File.ReadAllTextAsync(textPath) : null;

            var subjectCompiled = _handlebars.Compile(subjectTemplate);
            var htmlCompiled = _handlebars.Compile(htmlTemplate);
            var textCompiled = textTemplate != null ? _handlebars.Compile(textTemplate) : null;

            // Render with variables
            var subject = subjectCompiled(variables);
            var htmlBody = htmlCompiled(variables);
            var textBody = textCompiled?.Invoke(variables);

            return (subject, htmlBody, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering email template '{TemplateKey}'", templateKey);
            throw;
        }
    }
}
