using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Data;

[Table("WorkflowDefinitions")]
public class WorkflowDefinitionEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(500)]
    public string? Icon { get; set; }

    public string StepsJson { get; set; } = "[]";

    public string VariablesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsFavorite { get; set; }

    public int UsageCount { get; set; }

    public int Status { get; set; }

    [MaxLength(100)]
    public string? StartStepId { get; set; }

    public string? MetadataJson { get; set; }
}
