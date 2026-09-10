using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using MimeKit;
using MimeKit.Utils;
using MustMail.App.Services.Maintenance;

namespace MustMail.App.Services.MailProcessing;

public partial class MessageStorage(IDbContextFactory<DatabaseContext> dbFactory, UpdateService updates, ILogger<MessageStorage> logger, IOptionsMonitor<Configuration> config)
{

    public async Task StoreMessage(MimeMessage message, ResolvedRecipients recipients, ResolvedSender sender, string? smtpAccountUsername)
    {
        await using DatabaseContext dbContext = await dbFactory.CreateDbContextAsync();

        // Look up the SMTP account that authenticated
        int smtpAccountId = await dbContext.SMTPAccount
                .Where(a => a.Username == smtpAccountUsername)
                .Select(a => (int)a.Id)
                .SingleAsync();

        // Store the message
        Models.Message newMessage = new()
        {
            Id = message.MessageId!,
            SenderName = sender.Name!,
            SenderEmail = sender.Address!,
            Timestamp = message.Date.DateTime.ToUniversalTime(),
            Subject = message.Subject ?? "(No subject)",
            AttachmentCount = message.Attachments.Count(),
            SMTPAccountId = smtpAccountId,
            Recipients = BuildRecipients(recipients)
        };

        dbContext.Message.Add(newMessage);

        // Store the eml file if store mail content is enabled
        if (config.CurrentValue.Mail.StoreMailContent)
        {
            // Create a path maildrop/messageId.eml
            string emailPath = Path.Combine(
                                            AppContext.BaseDirectory,
                                            "Data",
                                            "maildrop",
                                            $"{message.MessageId}.eml");


            // Ensure all directories in this path exist
            _ = Directory.CreateDirectory(Path.GetDirectoryName(emailPath)!);

            LogEmailStored(message.MessageId!, emailPath);

            // Save message to file
            await using (FileStream fileStream = File.Create(emailPath))
            {
                await message.WriteToAsync(fileStream);
            }

            newMessage.ContentStored = true;

            // If the message has attachments then add them
            if (message.Attachments.Any())
            {

                foreach (MimeEntity mimeEntity in message.Attachments)
                {
                    switch (mimeEntity)
                    {
                        // Regular file attachment
                        case MimePart { Content: not null } mimePart:
                            {
                                string fileName = Helpers.SanitizeFileName(mimePart.FileName ?? "unnamed-attachment");

                                // Create path maildrop/messageId/filename
                                string attachmentPath = Path.Combine(
                                                                     AppContext.BaseDirectory,
                                                                     "Data",
                                                                     "maildrop",
                                                                     message.MessageId!,
                                                                     fileName);

                                // Ensure directory exists
                                _ = Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);

                                // Write file
                                await using FileStream fileStream = File.Create(attachmentPath);
                                await mimePart.Content.DecodeToAsync(fileStream);
                                break;
                            }
                        // Embedded email message
                        case MessagePart { Message: not null } messagePart:
                            {
                                string embeddedName = Helpers.SanitizeFileName($"{messagePart.Message.Subject ?? "embedded-message"}.eml");

                                // Create path maildrop/messageId/filename
                                string attachmentPath = Path.Combine(
                                                                     AppContext.BaseDirectory,
                                                                     "Data",
                                                                     "maildrop",
                                                                     message.MessageId!,
                                                                     embeddedName);

                                // Ensure directory exists
                                _ = Directory.CreateDirectory(Path.GetDirectoryName(attachmentPath)!);

                                // Write file
                                await using FileStream fileStream = File.Create(attachmentPath);
                                await messagePart.Message.WriteToAsync(fileStream);
                                break;
                            }
                    }
                }
            }
        }

        _ = await dbContext.SaveChangesAsync();

        // Notify any connected clients whose account matches one of the recipients
        foreach (Recipient recipient in recipients.All)
        {
            if (recipient.EmailAddress?.Address == null)
                continue;

            Models.User? appUser = await dbContext.User.SingleOrDefaultAsync(u => u.Email == recipient.EmailAddress.Address);

            if (appUser == null)
                continue;

            await updates.NewMessageForUserAsync(appUser.Id);
            LogClientUpdate(appUser.Id);
        }
    }

    private static List<Models.MessageRecipient> BuildRecipients(ResolvedRecipients recipients)
    {
        List<Models.MessageRecipient> messageRecipients = [];

        AppendRecipients(messageRecipients, recipients.To, Models.RecipientType.To);
        AppendRecipients(messageRecipients, recipients.Cc, Models.RecipientType.Cc);
        AppendRecipients(messageRecipients, recipients.Bcc, Models.RecipientType.Bcc);

        return messageRecipients;
    }

    private static void AppendRecipients(List<Models.MessageRecipient> messageRecipients, List<Recipient> recipients, Models.RecipientType type)
    {
        for (int position = 0; position < recipients.Count; position++)
        {
            EmailAddress? address = recipients[position].EmailAddress;

            if (string.IsNullOrWhiteSpace(address?.Address))
                continue;

            messageRecipients.Add(new Models.MessageRecipient
            {
                Email = address.Address,
                Name = address.Name,
                Type = type,
                Position = position
            });
        }
    }
    // 1150s = MessageStorage

    [LoggerMessage(EventId = 1150, Level = LogLevel.Information, Message = "Email stored locally for {MessageId} at {Path}")]
    private partial void LogEmailStored(string messageId, string path);

    [LoggerMessage(EventId = 1151, Level = LogLevel.Debug, Message = "Notified clients of mailbox update for user {UserId}")]
    private partial void LogClientUpdate(string userId);
}
