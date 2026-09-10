using System.ComponentModel.DataAnnotations;

namespace MustMail.App.Models;

public class Message
{
    [MaxLength(255)]
    public required string Id { get; init; }
    public DateTime Timestamp { get; init; }
    [MaxLength(255)]
    public required string SenderName { get; init; }
    [MaxLength(254)]
    public required string SenderEmail { get; init; }
    [MaxLength(255)]
    public required string Subject { get; init; }
    public int AttachmentCount { get; init; }
    public bool ContentStored { get; set; } = false;
    public int? SMTPAccountId { get; init; }// Optional foreign key property
    public SMTPAccount? SMTPAccount { get; init; }// Optional reference navigation to principal
    public ICollection<MessageRecipient> Recipients { get; init; } = [];
}