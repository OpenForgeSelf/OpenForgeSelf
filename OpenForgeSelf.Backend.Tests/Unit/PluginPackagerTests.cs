using System.IO.Compression;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class PluginPackagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly PluginPackagerService _packagerService;

    public PluginPackagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"OpenForgeSelf_PkgTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var permissionChecker = new Mock<IPermissionChecker>();
        var pluginManager = new PluginManager(serviceProvider, permissionChecker.Object);
        _packagerService = new PluginPackagerService(pluginManager);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    #region ValidatePackage Tests

    [Fact]
    public void ValidatePackage_ValidPackage_ShouldReturnTrue()
    {
        // Arrange
        var packagePath = CreateValidPackage();

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidatePackage_NonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.zip");

        // Act
        var result = _packagerService.ValidatePackage(nonExistentPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_MissingPluginJson_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "invalid.zip");
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("somefile.txt");
                using var sw = new StreamWriter(entry.Open());
                sw.Write("test");
            }
            File.WriteAllBytes(packagePath, ms.ToArray());
        }

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_InvalidJson_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "badjson.zip");
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("plugin.json");
                using var sw = new StreamWriter(entry.Open());
                sw.Write("not valid json {{{");
            }
            File.WriteAllBytes(packagePath, ms.ToArray());
        }

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_MissingId_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = CreatePackageWithManifest(new { name = "Test", version = "1.0.0" });

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_MissingName_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = CreatePackageWithManifest(new { id = "test", version = "1.0.0" });

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_MissingVersion_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = CreatePackageWithManifest(new { id = "test", name = "Test" });

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePackage_NotAZipFile_ShouldReturnFalse()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "notzip.zip");
        File.WriteAllText(packagePath, "this is not a zip file");

        // Act
        var result = _packagerService.ValidatePackage(packagePath);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ExtractPackage Tests

    [Fact]
    public void ExtractPackage_ValidPackage_ShouldExtractAndReturnMetadata()
    {
        // Arrange
        var packagePath = CreateValidPackage();
        var targetDir = Path.Combine(_testDir, "extracted");

        // Act
        var result = _packagerService.ExtractPackage(packagePath, targetDir);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("TestPlugin");
        result.Name.Should().Be("测试插件");
        result.Version.Should().Be("1.0.0");
        result.PluginDirectory.Should().Be(targetDir);

        Directory.Exists(targetDir).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "plugin.json")).Should().BeTrue();
    }

    [Fact]
    public void ExtractPackage_NonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.zip");
        var targetDir = Path.Combine(_testDir, "extracted");

        // Act & Assert
        var act = () => _packagerService.ExtractPackage(nonExistentPath, targetDir);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ExtractPackage_InvalidPackage_ShouldThrowInvalidDataException()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "invalid.zip");
        File.WriteAllText(packagePath, "not a valid package");
        var targetDir = Path.Combine(_testDir, "extracted");

        // Act & Assert
        var act = () => _packagerService.ExtractPackage(packagePath, targetDir);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ExtractPackage_TargetDirectoryNotExists_ShouldCreateIt()
    {
        // Arrange
        var packagePath = CreateValidPackage();
        var targetDir = Path.Combine(_testDir, "newdir", "subdir");

        // Act
        var result = _packagerService.ExtractPackage(packagePath, targetDir);

        // Assert
        Directory.Exists(targetDir).Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public void ExtractPackage_OverwriteExistingFiles_ShouldOverwrite()
    {
        // Arrange
        var packagePath = CreateValidPackage();
        var targetDir = Path.Combine(_testDir, "overwrite");
        Directory.CreateDirectory(targetDir);
        var existingFile = Path.Combine(targetDir, "plugin.json");
        File.WriteAllText(existingFile, "old content");

        // Act
        var result = _packagerService.ExtractPackage(packagePath, targetDir);

        // Assert
        var content = File.ReadAllText(existingFile);
        content.Should().NotBe("old content");
        content.Should().Contain("TestPlugin");
    }

    #endregion

    #region ReadPackageMetadata Tests

    [Fact]
    public void ReadPackageMetadata_ValidPackage_ShouldReturnMetadata()
    {
        // Arrange
        var packagePath = CreateValidPackage();

        // Act
        var result = _packagerService.ReadPackageMetadata(packagePath);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("TestPlugin");
        result.Name.Should().Be("测试插件");
        result.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void ReadPackageMetadata_NonExistentFile_ShouldReturnNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "nonexistent.zip");

        // Act
        var result = _packagerService.ReadPackageMetadata(nonExistentPath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadPackageMetadata_MissingPluginJson_ShouldReturnNull()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "nomanifest.zip");
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("data.txt");
                using var sw = new StreamWriter(entry.Open());
                sw.Write("test");
            }
            File.WriteAllBytes(packagePath, ms.ToArray());
        }

        // Act
        var result = _packagerService.ReadPackageMetadata(packagePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ReadPackageMetadata_NotAZipFile_ShouldReturnNull()
    {
        // Arrange
        var packagePath = Path.Combine(_testDir, "notzip_meta.zip");
        File.WriteAllText(packagePath, "this is not a zip");

        // Act
        var result = _packagerService.ReadPackageMetadata(packagePath);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private string CreateValidPackage()
    {
        var packagePath = Path.Combine(_testDir, "valid.zip");
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var pluginJson = new
            {
                id = "TestPlugin",
                name = "测试插件",
                version = "1.0.0",
                author = "TestAuthor",
                description = "测试用插件"
            };
            var entry = archive.CreateEntry("plugin.json");
            using (var sw = new StreamWriter(entry.Open()))
            {
                sw.Write(JsonSerializer.Serialize(pluginJson));
            }

            var dllEntry = archive.CreateEntry("TestPlugin.dll");
            using (var dllSw = new StreamWriter(dllEntry.Open()))
            {
                dllSw.Write("fake dll content");
            }

            var settingsEntry = archive.CreateEntry("Config/settings.json");
            using (var settingsSw = new StreamWriter(settingsEntry.Open()))
            {
                settingsSw.Write("{}");
            }
        }
        File.WriteAllBytes(packagePath, ms.ToArray());
        return packagePath;
    }

    private string CreatePackageWithManifest(object manifest)
    {
        var packagePath = Path.Combine(_testDir, $"manifest_{Guid.NewGuid():N}.zip");
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("plugin.json");
            using var sw = new StreamWriter(entry.Open());
            sw.Write(JsonSerializer.Serialize(manifest));
        }
        File.WriteAllBytes(packagePath, ms.ToArray());
        return packagePath;
    }

    #endregion
}
