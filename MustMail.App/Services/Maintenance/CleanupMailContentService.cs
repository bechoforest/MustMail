using Microsoft.Extensions.Options;
using Quartz;

namespace MustMail.App.Services.Maintenance;

public partial class CleanupMailContentService(IOptionsMonitor<Configuration> config,
    ILogger<CleanupMailContentService> logger, IDbContextFactory<DatabaseContext> dbFactory, UpdateService updates) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {

        LogCleanupStarted();

        await using DatabaseContext dbContext = await dbFactory.CreateDbContextAsync();

        DateTime expiryDate = DateTime.UtcNow - TimeSpan.FromDays(config.CurrentValue.Mail.MailContentRetentionDays);
        LogExpiryCutoff(config.CurrentValue.Mail.MailContentRetentionDays, expiryDate);

        // Find the expired messages that still have their content stored
        List<string> expiredIds = await dbContext.Message
            .Where(m => m.ContentStored && m.Timestamp < expiryDate)
            .Select(m => m.Id)
            .ToListAsync();

        if (expiredIds.Count == 0)
            return;

        // Find any accounts among the recipients of the expired messages so we can notify their clients
        List<string> affectedUserIds = await dbContext.User
            .Where(u => dbContext.MessageRecipient.Any(r => expiredIds.Contains(r.MessageId) && r.Email == u.Email))
            .Select(u => u.Id)
            .ToListAsync();

        // Remove the .eml file and attachment folder for each expired message
        foreach (string id in expiredIds)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "data", "maildrop", $"{id}.eml");
            string attachmentsPath = Path.Combine(AppContext.BaseDirectory, "maildrop", id);

            // Delete the file if it exists
            if (File.Exists(path))
            {
                File.Delete(path);
                LogFileDeleted(path);
            }

            // Delete the attachments folder if it exists
            if (Directory.Exists(attachmentsPath))
            {
                Directory.Delete(attachmentsPath, recursive: true);
                LogAttachmentFolderDeleted(attachmentsPath);
            }
        }

        // Mark the messages as no longer having their content stored, keeping the message row itself
        _ = await dbContext.Message
            .Where(m => expiredIds.Contains(m.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ContentStored, false));

        LogMessagesDeleted(expiredIds.Count);

        // Trigger update service to update any clients
        foreach (string userId in affectedUserIds)
        {
            LogNotifyClients(userId);
            await updates.NewMessageForUserAsync(userId);
        }
    }

    // 2000s = Cleanup Service

    [LoggerMessage(
                      EventId = 2101,
                      Level = LogLevel.Information,
                      Message = "Starting mail content cleanup")]
    private partial void LogCleanupStarted();

    [LoggerMessage(
                      EventId = 2102,
                      Level = LogLevel.Information,
                      Message = "Mail content cleanup retention is {RetentionDays} days. Expiry cutoff is {ExpiryDate}")]
    private partial void LogExpiryCutoff(int retentionDays, DateTime expiryDate);

    [LoggerMessage(
                      EventId = 2104,
                      Level = LogLevel.Debug,
                      Message = "Deleted message file {Path}")]
    private partial void LogFileDeleted(string path);

    [LoggerMessage(
                      EventId = 2105,
                      Level = LogLevel.Debug,
                      Message = "Deleted attachment folder {Path}")]
    private partial void LogAttachmentFolderDeleted(string path);

    [LoggerMessage(
                      EventId = 2107,
                      Level = LogLevel.Information,
                      Message = "Cleared stored content for {Count} expired messages")]
    private partial void LogMessagesDeleted(int count);

    [LoggerMessage(
                      EventId = 2108,
                      Level = LogLevel.Debug,
                      Message = "Notifying clients of mailbox content update for user {UserId}")]
    private partial void LogNotifyClients(string userId);
}
