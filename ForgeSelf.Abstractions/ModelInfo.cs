namespace ForgeSelf.Abstractions;

public class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public long Created { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public bool SupportsVision { get; set; }
    public bool SupportsStreaming { get; set; } = true;
    public int MaxTokens { get; set; }
}
