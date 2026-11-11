using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>
/// 安装包校验。ZIP 在解压前拒绝路径穿越；DLL 只用元数据读内嵌清单，不 <c>Assembly.Load</c>。
/// 安装包文件只认 <c>.zip</c>。单文件 DLL 是第二种发行物，不是另一种压缩包。
/// </summary>
public static class PluginArtifactValidator
{
    public const string EmbeddedManifestName = "ForgeSelf.PluginManifest.json";
    public const string LegacyPackageExtension = ".forgeself-plugin";

    public static bool IsLegacyPackage(string path) =>
        path.EndsWith(LegacyPackageExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsZip(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    public static bool IsDll(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    /// <summary>拒绝 <c>..</c>、绝对路径、盘符和忽略大小写的重名。</summary>
    public static void EnsureSafe(ZipArchive archive)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = (entry.FullName ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
                continue;

            if (name.StartsWith('/') || Path.IsPathRooted(entry.FullName) || name.Contains(':'))
                throw new InvalidDataException($"压缩项路径非法: {entry.FullName}");

            foreach (var part in name.Split('/'))
            {
                if (part is ".." or ".")
                    throw new InvalidDataException($"压缩项路径穿越: {entry.FullName}");
            }

            if (!seen.Add(name))
                throw new InvalidDataException($"压缩项重名: {entry.FullName}");
        }
    }

    public static void ExtractSafe(ZipArchive archive, string targetDir)
    {
        EnsureSafe(archive);
        var root = Path.GetFullPath(targetDir);
        Directory.CreateDirectory(root);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
            var dest = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"解压路径越界: {entry.FullName}");

            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    /// <summary>读取 DLL 内嵌 <c>ForgeSelf.PluginManifest.json</c>。失败返回 null，不加载程序集。</summary>
    public static PluginMetadata? ReadDllManifest(string dllPath)
    {
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return null;

            var bytes = ReadEmbedded(pe, EmbeddedManifestName);
            if (bytes == null || bytes.Length == 0)
                return null;

            return JsonSerializer.Deserialize<PluginMetadata>(bytes, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // PC-027：统一不变量——所有 JsonSerializerOptions 一律声明 Encoder（反序列化时 Encoder 被忽略，零读侧风险）
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void InstallDll(string dllPath, string versionDir)
    {
        var metadata = ReadDllManifest(dllPath)
            ?? throw new InvalidDataException("DLL 缺少内嵌 ForgeSelf.PluginManifest.json");
        Directory.CreateDirectory(versionDir);
        var fileName = string.IsNullOrWhiteSpace(metadata.EntryAssembly)
            ? Path.GetFileName(dllPath)
            : metadata.EntryAssembly;
        File.Copy(dllPath, Path.Combine(versionDir, fileName), true);
        var json = JsonSerializer.Serialize(metadata, ForgeJson.Indented);
        File.WriteAllText(Path.Combine(versionDir, "plugin.json"), json);
    }

    private static byte[]? ReadEmbedded(PEReader pe, string resourceName)
    {
        var reader = pe.GetMetadataReader();
        var headers = pe.PEHeaders.CorHeader;
        if (headers == null)
            return null;

        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (!string.Equals(reader.GetString(resource.Name), resourceName, StringComparison.Ordinal))
                continue;
            if (!resource.Implementation.IsNil)
                return null;

            var section = pe.GetSectionData(headers.ResourcesDirectory.RelativeVirtualAddress);
            var start = (int)resource.Offset;
            if (start < 0 || start >= section.Length)
                return null;

            var blob = section.GetReader(start, section.Length - start);
            var length = blob.ReadInt32();
            if (length < 0 || length > blob.RemainingBytes)
                return null;
            return blob.ReadBytes(length);
        }

        return null;
    }
}
