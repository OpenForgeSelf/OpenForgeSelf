using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Data;

/// <summary>
/// 聊天消息实体（用于数据库存储）
/// </summary>
[Table("ChatMessages")]
public class ChatMessageEntity
{
    /// <summary>
    /// 消息ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("SessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 消息角色（user/assistant/system）
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("Role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    [Required]
    [Column("Content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    [Column("UpdateTime")]
    public DateTime UpdateTime { get; set; }
}