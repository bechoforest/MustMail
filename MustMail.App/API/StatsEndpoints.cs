namespace MustMail.App.API;

public record EmailStats(int TotalEmails, int ContentStoredCount, int FailedCount, int TotalSmtpAccounts, int UniqueRecipients);

public static class StatsEndpoints
{
    public static async Task<IResult> GetStats(IDbContextFactory<DatabaseContext> dbFactory)
    {
        await using DatabaseContext dbContext = await dbFactory.CreateDbContextAsync();

        int totalEmails = await dbContext.Message.CountAsync();
        int contentStoredCount = await dbContext.Message.CountAsync(m => m.ContentStored);
        int failedCount = await dbContext.Message.CountAsync(m => m.DeliveryFailed);
        int totalSmtpAccounts = await dbContext.SMTPAccount.CountAsync();
        int uniqueRecipients = await dbContext.MessageRecipient.Select(r => r.Email).Distinct().CountAsync();

        return Results.Ok(new EmailStats(totalEmails, contentStoredCount, failedCount, totalSmtpAccounts, uniqueRecipients));
    }
}
