using Microsoft.Extensions.Options;
using Quartz;

namespace MustMail.App.Services.Maintenance;

public partial class CleanupMailService(IOptionsMonitor<Configuration> config,
    ILogger<CleanupMailService> logger, IDbContextFactory<DatabaseContext> dbFactory, UpdateService updates) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {

        LogCleanupStarted();

        await using DatabaseContext dbContext = await dbFactory.CreateDbContextAsync();

        DateTime expiryDate = DateTime.UtcNow - TimeSpan.FromDays(config.CurrentValue.Mail.MailRetentionDays);
        LogExpiryCutoff(config.CurrentValue.Mail.MailRetentionDays, expiryDate);

        // Find the expired messages
        List<string> expiredIds = await dbContext.Message
            .Where(m => m.Timestamp < expiryDate)
            .Select(m => m.Id)
            .ToListAsync();

        if (expiredIds.Count == 0)
            return;

        // Find any accounts among the recipients of the expired messages so we can notify their clients
        List<string> affectedUserIds = await dbContext.User
            .Where(u => dbContext.MessageRecipient.Any(r => expiredIds.Contains(r.MessageId) && r.Email == u.Email))
            .Select(u => u.Id)
            .ToListAsync();

        // Remove expired messages from the database (this cascades to their recipients)
        _ = await dbContext.Message
            .Where(m => expiredIds.Contains(m.Id))
            .ExecuteDeleteAsync();

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
                      EventId = 2001,
                      Level = LogLevel.Information,
                      Message = "Starting mail cleanup")]
    private partial void LogCleanupStarted();

    [LoggerMessage(
                      EventId = 2002,
                      Level = LogLevel.Information,
                      Message = "Mail cleanup retention is {RetentionDays} days. Expiry cutoff is {ExpiryDate}")]
    private partial void LogExpiryCutoff(int retentionDays, DateTime expiryDate);

    [LoggerMessage(
                      EventId = 2007,
                      Level = LogLevel.Information,
                      Message = "Deleted {Count} expired messages from database")]
    private partial void LogMessagesDeleted(int count);

    [LoggerMessage(
                      EventId = 2008,
                      Level = LogLevel.Debug,
                      Message = "Notifying clients of mail update for user {UserId}")]
    private partial void LogNotifyClients(string userId);
}