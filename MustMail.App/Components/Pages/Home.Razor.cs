using Ganss.Xss;
using Microsoft.AspNetCore.Components.Authorization;
using MimeKit;
using MudBlazor.Extensions;
using MustMail.App.Services.Maintenance;
using System.Security.Claims;

namespace MustMail.App.Components.Pages;

public class HomeBase : ComponentBase
{
    // Class variables
    private Models.Profile? _profile;
    private IDisposable? _subscription;

    // Mobile state
    protected bool IsMobile;
    protected bool MobileShowDetail;
    protected Message? MobileSelectedMessage;

    // Page variables
    protected string? UserId;
    protected string? UserEmail;
    protected string Name = "";
    protected int MessageCount;
    protected string? MostRecentEmailSubject = "Unknown";
    protected DateTime MostRecentEmailTimestamp = DateTime.MinValue;
    protected List<Message> Messages = [];
    protected MudTabs MessageTabs = null!;
    protected MimeMessage? ActiveMessage;
    protected bool StoreMailContent;
    private string _maildropFolder = null!;


    // Component parameters and dependency injection
    [Inject] private AuthenticationStateProvider AuthenticationState { get; set; } = null!;
    [Inject] private IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;
    [Inject] private UpdateService Updates { get; set; } = null!;
    [Inject] public IConfiguration Configuration { get; set; } = null!;
    [Inject] private IBrowserViewportService BrowserViewportService { get; set; } = null!;

    // Lifecycle method called after parameters and property values are set
    protected override async Task OnInitializedAsync()
    {

        StoreMailContent = Configuration.Get<Configuration>()!.Mail.StoreMailContent;

        AuthenticationState authState = await AuthenticationState.GetAuthenticationStateAsync();

        // Set name
        Name = authState.User.Identity?.Name ?? "";

        await using DatabaseContext dbContext = await DbFactory.CreateDbContextAsync();

        // Get user
        User? user = await dbContext.User.FindAsync(authState.User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (user == null)
        {
            return;
        }

        await dbContext.Entry(user).Reference(u => u.Profile).LoadAsync();

        UserId = user.Id;
        UserEmail = user.Email;
        _profile = user.Profile;

        _maildropFolder = Path.Combine(AppContext.BaseDirectory, "Data", "maildrop");


        // Subscribe to events for the current user id using the UpdateServer
        _subscription = Updates.Subscribe(UserId, async () => {
            await GetMessages();
            await InvokeAsync(StateHasChanged);
        });

        await GetMessages();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        Breakpoint bp = await BrowserViewportService.GetCurrentBreakpointAsync();
        IsMobile = bp is Breakpoint.Xs or Breakpoint.Sm;
        StateHasChanged();
    }

    // Open a message in the mobile detail view
    protected async Task MobileOpenMessage(Message message)
    {
        MobileSelectedMessage = message;

        if (message.ContentStored && StoreMailContent)
        {
            string path = Path.Combine(_maildropFolder, $"{message.Id}.eml");
            ActiveMessage = await MimeMessage.LoadAsync(path);
        }
        else
        {
            ActiveMessage = null;
        }

        MobileShowDetail = true;
    }

    protected void MobileBack()
    {
        MobileShowDetail = false;
        MobileSelectedMessage = null;
        ActiveMessage = null;
    }

    // Get messages - gets the users messages from the database and gathers some details about the most recent message
    private async Task GetMessages()
    {
        await using DatabaseContext dbContext = await DbFactory.CreateDbContextAsync();

        // Get messages where this user's email is one of the recipients, excluding ones that failed to send
        List<Message> messages = await dbContext.Message
            .Include(m => m.Recipients)
            .Where(m => !m.DeliveryFailed && m.Recipients.Any(r => r.Email == UserEmail))
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        // Set message count
        MessageCount = messages.Count;

        // If there are messages get the most recent one
        if (MessageCount > 0)
        {
            // Get the most recent message
            Message mostRecentMessage = messages[0];

            // Grab details for panel
            MostRecentEmailTimestamp = mostRecentMessage.Timestamp;
            MostRecentEmailSubject = mostRecentMessage.Subject;

            if (mostRecentMessage.ContentStored && StoreMailContent)
            {
                // Create file path
                string path = Path.Combine(
                                       _maildropFolder,
                                       $"{mostRecentMessage.Id}.eml");

                // Load the eml file
                ActiveMessage = await MimeMessage.LoadAsync(path);
            }
               

        }

        // All messages
        Messages = messages;

    }

    // Sanitized body - Prevent Cross-site scripting (XSS) by Sanitizing HTML string
    protected string SanitizedBody(string html)
    {
        HtmlSanitizer sanitizer = new();

        // Allow inline images
        _ = sanitizer.AllowedSchemes.Add("data");

        sanitizer.FilterUrl += (_, e) => {
            if (e.OriginalUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return;

            e.SanitizedUrl = null;
        };

        return sanitizer.Sanitize(html);
    }

    protected string DateTimeConvertAndFormat(DateTime dateTime)
    {
        if (_profile == null)
            return dateTime.ToIsoDateString();

        // Get the user's time zone
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(_profile.TimeZone);

        return TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone).ToString($"{_profile.DateFormat} {_profile.TimeFormat}");
    }

    // Format friendly date - style date
    protected string FormatFriendlyDate(DateTime dateTime)
    {
        DateTime now = DateTime.Now;
        DateTime today = now.Date;
        DateTime inputDate = dateTime.Date;
        TimeZoneInfo timeZone = TimeZoneInfo.Utc;

        if (_profile != null)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(_profile.TimeZone);
        }

        // Today → time only with AM/PM
        if (inputDate == today)
        {
            return _profile == null
                ? dateTime.ToString("h:mm tt")
                : TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone).ToString(_profile.TimeFormat);
        }

        // Last 5 days (excluding today)
        if (inputDate >= today.AddDays(-5))
        {
            return _profile == null
                ? dateTime.ToString("ddd d/M")
                : TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone).ToString(_profile.DateFormat.Replace("y", ""));
        }

        // Older than 5 days
        return _profile == null
            ? dateTime.ToString("d/M/yy")
            : TimeZoneInfo.ConvertTimeFromUtc(dateTime, timeZone).ToString(_profile.DateFormat);
    }

    // Message changed - When tab changed, get the message id and load the eml file
    protected async Task MessageChanged(int index)
    {
        if (index < 0 && index > MessageTabs.Panels.Count - 1)
            index = 1;

        if (MessageTabs.Panels[index].ID is not string messageId)
            return;

        await using DatabaseContext dbContext = await DbFactory.CreateDbContextAsync();

        Message message = await dbContext.Message.Include(m => m.Recipients)
            .Where(m => !m.DeliveryFailed && m.Recipients.Any(r => r.Email == UserEmail)).SingleAsync(m => m.Id == messageId);

        if (message.ContentStored && StoreMailContent)
        {
            // Create file path
            string path = Path.Combine(
                                   _maildropFolder,
                                   $"{messageId}.eml");

            // Load the eml file
            ActiveMessage = MimeMessage.Load(path);
        }
        else
        {
            ActiveMessage = null;
        }
    }

    // Only the recipient actually bcc'd should see a Bcc line, and only their own address - never the other bcc'd recipients
    protected bool IsUserBcc(Message message) =>
        UserEmail != null && message.Recipients.Any(r => r.Type == RecipientType.Bcc && string.Equals(r.Email, UserEmail, StringComparison.OrdinalIgnoreCase));

    // Recipients are stored in the database, so To/Cc can always be displayed regardless of whether the eml content is stored
    protected static string FormatRecipients(Message message, RecipientType type)
    {
        IEnumerable<string> recipients = message.Recipients
            .Where(r => r.Type == type)
            .OrderBy(r => r.Position)
            .Select(r => string.IsNullOrWhiteSpace(r.Name) ? r.Email : $"{r.Name} <{r.Email}>");

        return string.Join(", ", recipients);
    }

    protected static string FormatAttachmentMeta(MimePart part)
    {
        // ContentType like "image/png"
        string type = part.ContentType.MimeType;

        // Size if known
        // MimeKit sometimes has ContentDisposition?.Size, or you may track size elsewhere.
        long? size = part.ContentDisposition?.Size;
        return size is null ? type : $"{type} • {FormatBytes(size.Value)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.#} {units[unit]}";
    }

    // Dispose - Unsubscribe from events
    public void Dispose()
    {
        _subscription?.Dispose();
    }
}