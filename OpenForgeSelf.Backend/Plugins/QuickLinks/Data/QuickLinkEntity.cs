using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Data;

[Table("QuickLinks")]
public class QuickLinkEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("Url")]
    public string Url { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Icon")]
    public string? Icon { get; set; }

    [MaxLength(1000)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("CategoryId")]
    public long? CategoryId { get; set; }

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    [Column("ClickCount")]
    public int ClickCount { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}
