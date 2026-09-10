using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;

namespace MustMail.App.API;

public static class MaildropEndpoints
{
    public static async Task<IResult> DownloadEmail(string messageId, ClaimsPrincipal user, IDbContextFactory<DatabaseContext> dbFactory)
    {

        if (!await IsRecipient(messageId, user, dbFactory))
            return Results.Unauthorized();

        string basePath = Path.Combine(AppContext.BaseDirectory, "Data", "maildrop");

        if (!Helpers.IsPathSegmentSafe(basePath, messageId))
            return Results.NotFound();

        string path = Path.GetFullPath(Path.Combine(basePath, messageId));

        if (!File.Exists(path))
            return Results.NotFound();

        return Results.File(path, "message/rfc822", $"{messageId}.eml");
    }

    public static async Task<IResult> DownloadAttachment(string messageId, string fileName, ClaimsPrincipal user, IDbContextFactory<DatabaseContext> dbFactory)
    {
        if (!await IsRecipient(messageId, user, dbFactory))
            return Results.Unauthorized();

        string basePath = Path.Combine(AppContext.BaseDirectory, "Data", "maildrop");
        string relativePath = Path.Combine(messageId, Helpers.SanitizeFileName(fileName));

        if (!Helpers.IsPathSegmentSafe(basePath, relativePath))
            return Results.NotFound();

        string path = Path.GetFullPath(Path.Combine(basePath, relativePath));

        if (!File.Exists(path))
            return Results.NotFound();

        if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out string? contentType))
            contentType = "application/octet-stream";

        return Results.File(path, contentType, fileName);
    }

    private static async Task<bool> IsRecipient(string messageId, ClaimsPrincipal user, IDbContextFactory<DatabaseContext> dbFactory)
    {
        // Require authentication
        if (user.Identity?.IsAuthenticated != true)
            return false;

        string? userEmail = user.FindFirstValue(ClaimTypes.Email);

        // Null check for email, shouldn't be null but sanity check
        if (string.IsNullOrEmpty(userEmail))
            return false;

        await using DatabaseContext dbContext = await dbFactory.CreateDbContextAsync();

        List<string> recipientEmails = await dbContext.MessageRecipient
            .Where(r => r.MessageId == messageId)
            .Select(r => r.Email)
            .ToListAsync();

        // Only allow a user access to mail addressed to their own exact email address
        return recipientEmails.Any(email => string.Equals(email, userEmail, StringComparison.OrdinalIgnoreCase));
    }
}
