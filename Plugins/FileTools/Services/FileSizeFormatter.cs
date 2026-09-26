namespace ForgeSelf.Api.Plugins.FileTools.Services;

public static class FileSizeFormatter
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "-" + FormatSize(-bytes);
        if (bytes == 0) return "0 B";

        int i = 0;
        decimal d = bytes;
        while (d >= 1024 && i < SizeSuffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }

        return $"{d:N2} {SizeSuffixes[i]}";
    }
}
