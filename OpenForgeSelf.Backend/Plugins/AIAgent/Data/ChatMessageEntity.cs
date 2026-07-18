using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Data;

[Table("AIAgent_ChatMessages")]
public class ChatMessageEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("Role")]
    public string Role { get; set; } = string.Empty;

    [Required]
    [Column("Content", TypeName = "TEXT")]
    public string Content { get; set; } = string.Empty;

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; }
}
