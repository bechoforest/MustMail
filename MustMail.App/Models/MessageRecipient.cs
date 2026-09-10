using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MustMail.App.Models;

public enum RecipientType
{
    To,
    Cc,
    Bcc
}

public class MessageRecipient
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [MaxLength(255)]
    public string MessageId { get; set; } = null!;// Required foreign key property
    public Message Message { get; set; } = null!;// Required reference navigation to principal
    [MaxLength(254)]
    public required string Email { get; set; }
    [MaxLength(255)]
    public string? Name { get; set; }
    public required RecipientType Type { get; set; }
    public int Position { get; set; }
}
