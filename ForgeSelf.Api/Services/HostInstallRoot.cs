namespace ForgeSelf.Api.Services;

/// <summary>
/// 安装根（QQNT 公共层目录）解析。宿主业务层 exe 位于 <c>&lt;安装根&gt;/versions/&lt;ver&gt;/</c> 内，
/// 更新链路必须把<b>安装根</b>交给 update-agent.ps1：把业务层自身目录当安装根，新版本就会落进
/// <c>versions/&lt;ver&gt;/versions/&lt;new&gt;/</c>，每升一代多一层嵌套（2026-10-04 现场实测三层）。
/// 规则与 <c>scripts/update-agent.ps1</c> 的 <c>Resolve-ForgeInstallRoot</c> 一致：父目录名为
/// <c>versions</c> 就一路上跳两级；扁平布局（dev/测试输出目录）原样返回。
/// 结构真源：docs/04-standards/packaging-upgrade-backup.md §3。
/// </summary>
public static class HostInstallRoot
{
    /// <summary>版本层目录名（安装根与业务层之间的那一层）。</summary>
    public const string VersionsFolderName = "versions";

    /// <summary>由宿主自身可执行文件路径解析安装根。</summary>
    public static string ResolveFromExecutable(string? hostExecutablePath)
    {
        var exeDir = Path.GetDirectoryName(hostExecutablePath ?? string.Empty);
        if (string.IsNullOrEmpty(exeDir))
            throw new InvalidOperationException("无法确定应用程序目录。");
        return Resolve(exeDir);
    }

    /// <summary>把任意起点目录归一化为安装根。</summary>
    public static string Resolve(string startDir) => ResolveCore(startDir).Root;

    /// <summary>
    /// 起点相对安装根的 <c>versions/&lt;ver&gt;</c> 层数：0=扁平布局（业务层即在安装根），
    /// 1=正常 QQNT 布局，≥2=发生了逐代嵌套。
    /// </summary>
    public static int VersionLayerCount(string startDir) => ResolveCore(startDir).Layers;

    private static (string Root, int Layers) ResolveCore(string startDir)
    {
        var current = Path.GetFullPath(startDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var layers = 0;
        while (true)
        {
            var versionsDir = Path.GetDirectoryName(current);
            if (versionsDir is null) break;
            if (!string.Equals(Path.GetFileName(versionsDir), VersionsFolderName, StringComparison.OrdinalIgnoreCase))
                break;
            var root = Path.GetDirectoryName(versionsDir);
            if (root is null) break;
            current = root;
            layers++;
        }
        return (current, layers);
    }
}
