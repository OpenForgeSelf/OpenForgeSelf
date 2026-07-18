using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Data;

[Table("QuickLinkCategories")]
public class QuickLinkCategoryEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Icon")]
    public string? Icon { get; set; }

    [Column("SortOrder")]
    public int SortOrder { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }
}
