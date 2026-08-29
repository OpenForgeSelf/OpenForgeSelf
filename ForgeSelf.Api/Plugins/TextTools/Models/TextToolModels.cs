namespace ForgeSelf.Api.Plugins.TextTools.Models;

public class FormatRequest
{
    public string Text { get; set; } = string.Empty;
    public int IndentSize { get; set; } = 2;
}

public class EncodeRequest
{
    public string Text { get; set; } = string.Empty;
}

public class HashRequest
{
    public string Text { get; set; } = string.Empty;
}

public class TextStatsRequest
{
    public string Text { get; set; } = string.Empty;
}

public class TextStatsResult
{
    public int CharCount { get; set; }
    public int CharCountNoSpaces { get; set; }
    public int WordCount { get; set; }
    public int LineCount { get; set; }
    public int ByteCount { get; set; }
}

public class TextToolResult
{
    public string Result { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
