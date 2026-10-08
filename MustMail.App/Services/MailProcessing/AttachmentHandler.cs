using Microsoft.Graph.Models;
using MimeKit;

namespace MustMail.App.Services.MailProcessing;

public partial class AttachmentHandler(ILogger<AttachmentHandler> logger)
{
    public async Task<List<Attachment>> HandelAttachments(MimeMessage message)
    {
        // Create a list to store attachments
        List<Attachment> attachments = [];
        
        // Loop through each attachment in message 
        foreach (MimeEntity mimeEntity in message.Attachments)
        {
            switch (mimeEntity)
            {
                // Regular file attachment
                case MimePart { Content: not null } mimePart:
                {
                    string fileName = mimePart.FileName ?? "unnamed-attachment";

                    // Replace invalid characters with hyphens
                    Array.ForEach(Path.GetInvalidFileNameChars(),
                                  c => fileName = fileName.Replace(c.ToString(), "-"));

                    // Write to byte stream
                    using MemoryStream memory = new();
                    await mimePart.Content.DecodeToAsync(memory);
                    byte[] attachmentBytes = memory.ToArray();

                    // Create graph attachment 
                    attachments.Add(new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = fileName,
                        ContentType = mimePart.ContentType.MimeType,
                        ContentBytes = attachmentBytes
                    });

                    LogAttachment(fileName, attachmentBytes.Length, mimePart.ContentType.MimeType);
                    break;
                }
                // Embedded email message
                case MessagePart { Message: not null } messagePart:
                {
                    string embeddedName = messagePart.Message.Subject ?? "embedded-message";

                    // Replace invalid characters with hyphens
                    Array.ForEach(Path.GetInvalidFileNameChars(),
                                  c => embeddedName = embeddedName.Replace(c.ToString(), "-"));

                    // Write to byte stream
                    using MemoryStream memory = new();
                    await messagePart.Message.WriteToAsync(memory);
                    byte[] messageBytes = memory.ToArray();

                    // Create graph attachment 
                    attachments.Add(new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = embeddedName + ".eml",
                        ContentType = "message/rfc822",
                        ContentBytes = messageBytes
                    });

                    LogEmbeddedMessage(embeddedName, messageBytes.Length);
                    break;
                }
            }
        }

        // Inline body parts referenced from the HTML as cid:xyz (e.g. embedded images).
        foreach (MimePart inlinePart in message.BodyParts.OfType<MimePart>())
        {
            if (inlinePart.IsAttachment
                || string.IsNullOrEmpty(inlinePart.ContentId)
                || inlinePart.Content is null
                || inlinePart.ContentType.IsMimeType("text", "*"))
                continue;

            string fileName = inlinePart.FileName ?? inlinePart.ContentId;

            // Replace invalid characters with hyphens
            Array.ForEach(Path.GetInvalidFileNameChars(),
                          c => fileName = fileName.Replace(c.ToString(), "-"));

            // Write to byte stream
            using MemoryStream memory = new();
            await inlinePart.Content.DecodeToAsync(memory);
            byte[] inlineBytes = memory.ToArray();

            // Create inline graph attachment; ContentId must match the cid: reference in the HTML body
            attachments.Add(new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = fileName,
                ContentType = inlinePart.ContentType.MimeType,
                ContentBytes = inlineBytes,
                ContentId = inlinePart.ContentId,
                IsInline = true
            });

            LogInlineAttachment(fileName, inlinePart.ContentId, inlineBytes.Length, inlinePart.ContentType.MimeType);
        }

        return attachments;
    }
    // 1140s = MessageHandler 
    [LoggerMessage(EventId = 1140, Level = LogLevel.Debug, Message = "Processing attachment: {FileName}, Size: {Size} bytes, Type: {ContentType}")]
    private partial void LogAttachment(string fileName, int size, string contentType);
    [LoggerMessage(EventId = 1141, Level = LogLevel.Debug, Message = "Processing embedded message: {Name}, Size: {Size} bytes")]
    private partial void LogEmbeddedMessage(string name, int size);
    [LoggerMessage(EventId = 1142, Level = LogLevel.Debug, Message = "Processing inline attachment: {FileName}, Content-ID: {ContentId}, Size: {Size} bytes, Type: {ContentType}")]
    private partial void LogInlineAttachment(string fileName, string contentId, int size, string contentType);
}