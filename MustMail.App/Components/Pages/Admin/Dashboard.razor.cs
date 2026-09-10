using System.Security.Cryptography.X509Certificates;

namespace MustMail.App.Components.Pages.Admin;

public class DashboardBase : ComponentBase
{
    protected int UserCount;
    protected int AdminCount;
    protected int SmtpAccountCount;
    protected int EmailCount;
    protected int ContentStoredCount;
    protected DateTime MostRecentMessage;
    protected string LogLevel = "Information";
    protected bool StoreMailContent;
    protected int MailContentRetentionDays;
    protected int MailRetentionDays;
    protected string SmtpHost = "localhost";
    protected bool AllowInsecure;
    protected string? CertificateCommonName;
    protected DateTime? CertificateExpiry;

    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;
    [Inject] public IConfiguration Configuration { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        Configuration config = Configuration.Get<Configuration>()!;

        LogLevel = config.Serilog.MinimumLevel.Default;
        StoreMailContent = config.Mail.StoreMailContent;
        MailContentRetentionDays = config.Mail.MailContentRetentionDays;
        MailRetentionDays = config.Mail.MailRetentionDays;
        SmtpHost = config.Smtp.Host;
        AllowInsecure = config.Smtp.AllowInsecure;
        
        if (config.Certificate.Format == "PFX")
        {
            if (!string.IsNullOrEmpty(config.Certificate.PFXPath) && !string.IsNullOrEmpty(config.Certificate.Password) && File.Exists(config.Certificate.PFXPath))
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(config.Certificate.PFXPath, config.Certificate.Password);
                CertificateCommonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
                CertificateExpiry = certificate.NotAfter;
            }

        }
        else if (config.Certificate.Format == "PEM")
        {
            if (!string.IsNullOrEmpty(config.Certificate.PEMCertPath) && !string.IsNullOrEmpty(config.Certificate.PEMKeyPath) && File.Exists(config.Certificate.PEMCertPath) && File.Exists(config.Certificate.PEMKeyPath))
            {
                X509Certificate2 certificate = X509Certificate2.CreateFromPemFile(config.Certificate.PEMCertPath, config.Certificate.PEMKeyPath);
                CertificateCommonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
                CertificateExpiry = certificate.NotAfter;
            }
        }

        await using DatabaseContext dbContext = await DbFactory.CreateDbContextAsync();

        UserCount = await dbContext.User.CountAsync();
        AdminCount = await dbContext.User.CountAsync(u => u.Admin);
        SmtpAccountCount = await dbContext.SMTPAccount.CountAsync();
        EmailCount = await dbContext.Message.CountAsync();
        ContentStoredCount = await dbContext.Message.CountAsync(m => m.ContentStored);

        Message? message = await dbContext.Message
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();

        MostRecentMessage = message?.Timestamp ?? DateTime.MinValue;
    }

    protected static string FormatRelativeTime(DateTime utc)
    {
        TimeSpan diff = DateTime.UtcNow - utc;
        return diff.TotalMinutes < 1 ? "Just now"
            : diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}m ago"
            : diff.TotalDays < 1 ? $"{(int)diff.TotalHours}h ago"
            : diff.TotalDays < 7 ? $"{(int)diff.TotalDays}d ago"
            : utc.ToString("dd MMM yyyy");
    }
}
