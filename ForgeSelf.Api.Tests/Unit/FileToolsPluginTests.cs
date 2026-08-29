using ForgeSelf.Api.Plugins.FileTools.Models;
using ForgeSelf.Api.Plugins.FileTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class FileToolsPluginTests : IDisposable
{
    private readonly RenameService _renameService;
    private readonly CleanupService _cleanupService;
    private readonly string _testDir;

    public FileToolsPluginTests()
    {
        _renameService = new RenameService();
        _cleanupService = new CleanupService();
        _testDir = Path.Combine(Path.GetTempPath(), "ForgeSelfTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    #region RenameService - Sequence Rule Tests

    [Fact]
    public async Task PreviewRenameAsync_WithSequenceRule_ShouldGenerateSequentialNames()
    {
        // Arrange
        var files = CreateTestFiles(3, "file", ".txt");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.Sequence,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["startIndex"] = "1",
                    ["padding"] = "3",
                    ["position"] = "0",
                    ["separator"] = "_"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(3);
        result[0].NewName.Should().Be("001_file_0.txt");
        result[1].NewName.Should().Be("002_file_1.txt");
        result[2].NewName.Should().Be("003_file_2.txt");
    }

    [Fact]
    public async Task PreviewRenameAsync_WithSequenceRule_CustomStartIndex_ShouldStartFromIndex()
    {
        // Arrange
        var files = CreateTestFiles(2, "doc", ".pdf");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.Sequence,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["startIndex"] = "10",
                    ["padding"] = "2",
                    ["position"] = "1",
                    ["separator"] = "-"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(2);
        result[0].NewName.Should().Be("doc_0-10.pdf");
        result[1].NewName.Should().Be("doc_1-11.pdf");
    }

    #endregion

    #region RenameService - Date Rule Tests

    [Fact]
    public async Task PreviewRenameAsync_WithDateRule_ShouldAddDatePrefix()
    {
        // Arrange
        var files = CreateTestFiles(1, "report", ".docx");
        var dateFormat = "yyyyMMdd";
        var expectedDate = DateTime.Now.ToString(dateFormat);
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.Date,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["format"] = dateFormat,
                    ["position"] = "0",
                    ["separator"] = "_"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].NewName.Should().StartWith(expectedDate + "_");
        result[0].NewName.Should().EndWith("_report_0.docx");
    }

    [Fact]
    public async Task PreviewRenameAsync_WithDateRule_SuffixPosition_ShouldAddDateSuffix()
    {
        // Arrange
        var files = CreateTestFiles(1, "photo", ".jpg");
        var dateFormat = "yyyy-MM-dd";
        var expectedDate = DateTime.Now.ToString(dateFormat);
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.Date,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["format"] = dateFormat,
                    ["position"] = "1",
                    ["separator"] = "_"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].NewName.Should().Be($"photo_0_{expectedDate}.jpg");
    }

    #endregion

    #region RenameService - FindReplace Rule Tests

    [Fact]
    public async Task PreviewRenameAsync_WithFindReplaceRule_ShouldReplaceText()
    {
        // Arrange
        var files = CreateTestFiles(2, "old_name", ".txt");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.FindReplace,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["find"] = "old",
                    ["replace"] = "new",
                    ["ignoreCase"] = "false",
                    ["useRegex"] = "false"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(2);
        result[0].NewName.Should().Be("new_name_0.txt");
        result[1].NewName.Should().Be("new_name_1.txt");
    }

    [Fact]
    public async Task PreviewRenameAsync_WithFindReplaceRule_IgnoreCase_ShouldReplaceRegardlessOfCase()
    {
        // Arrange
        var files = CreateTestFiles(1, "OLD_file_OLD", ".txt");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.FindReplace,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["find"] = "old",
                    ["replace"] = "new",
                    ["ignoreCase"] = "true",
                    ["useRegex"] = "false"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].NewName.Should().Be("new_file_new_0.txt");
    }

    [Fact]
    public async Task PreviewRenameAsync_WithFindReplaceRule_Regex_ShouldUseRegexReplacement()
    {
        // Arrange
        var files = CreateTestFiles(1, "img_2024_001", ".png");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.FindReplace,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["find"] = @"img_(\d+)_(\d+)",
                    ["replace"] = "photo_$2_$1",
                    ["ignoreCase"] = "false",
                    ["useRegex"] = "true"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].NewName.Should().Be("photo_001_2024_0.png");
    }

    #endregion

    #region RenameService - Multiple Rules Tests

    [Fact]
    public async Task PreviewRenameAsync_WithMultipleRules_ShouldApplyInOrder()
    {
        // Arrange
        var files = CreateTestFiles(2, "test", ".txt");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.FindReplace,
                Order = 1,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["find"] = "test",
                    ["replace"] = "file",
                    ["ignoreCase"] = "false",
                    ["useRegex"] = "false"
                }
            },
            new()
            {
                RuleType = RenameRuleType.Sequence,
                Order = 2,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["startIndex"] = "1",
                    ["padding"] = "2",
                    ["position"] = "0",
                    ["separator"] = "_"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(2);
        result[0].NewName.Should().Be("01_file_0.txt");
        result[1].NewName.Should().Be("02_file_1.txt");
    }

    [Fact]
    public async Task PreviewRenameAsync_WithDisabledRule_ShouldSkipDisabledRule()
    {
        // Arrange
        var files = CreateTestFiles(1, "test", ".txt");
        var rules = new List<RenameRule>
        {
            new()
            {
                RuleType = RenameRuleType.FindReplace,
                Order = 1,
                Enabled = false,
                Parameters = new Dictionary<string, object>
                {
                    ["find"] = "test",
                    ["replace"] = "disabled",
                    ["ignoreCase"] = "false",
                    ["useRegex"] = "false"
                }
            },
            new()
            {
                RuleType = RenameRuleType.Prefix,
                Order = 2,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["prefix"] = "pre_"
                }
            }
        };

        // Act
        var result = await _renameService.PreviewRenameAsync(files, rules);

        // Assert
        result.Should().HaveCount(1);
        result[0].NewName.Should().Be("pre_test_0.txt");
    }

    #endregion

    #region CleanupService - ByExtension Rule Tests

    [Fact]
    public async Task PreviewCleanupAsync_WithExtensionIncludeRule_ShouldFilterByExtension()
    {
        // Arrange
        CreateTestFile("document.pdf", 100);
        CreateTestFile("image.jpg", 200);
        CreateTestFile("data.txt", 300);
        CreateTestFile("photo.png", 400);

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.ByExtension,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["extensions"] = "jpg,png",
                    ["mode"] = "include"
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(2);
        result.Files.Should().Contain(f => f.FileName == "image.jpg");
        result.Files.Should().Contain(f => f.FileName == "photo.png");
        result.Files.Should().NotContain(f => f.FileName == "document.pdf");
        result.Files.Should().NotContain(f => f.FileName == "data.txt");
    }

    [Fact]
    public async Task PreviewCleanupAsync_WithExtensionExcludeRule_ShouldExcludeExtensions()
    {
        // Arrange
        CreateTestFile("a.txt", 100);
        CreateTestFile("b.log", 200);
        CreateTestFile("c.txt", 300);
        CreateTestFile("d.tmp", 400);

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.ByExtension,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["extensions"] = "txt",
                    ["mode"] = "exclude"
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(2);
        result.Files.Should().Contain(f => f.FileName == "b.log");
        result.Files.Should().Contain(f => f.FileName == "d.tmp");
        result.Files.Should().NotContain(f => f.FileName == "a.txt");
        result.Files.Should().NotContain(f => f.FileName == "c.txt");
    }

    #endregion

    #region CleanupService - BySize Rule Tests

    [Fact]
    public async Task PreviewCleanupAsync_WithSizeRangeRule_ShouldFilterBySize()
    {
        // Arrange
        CreateTestFile("small.txt", 100);
        CreateTestFile("medium.txt", 500);
        CreateTestFile("large.txt", 1000);
        CreateTestFile("extralarge.txt", 2000);

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.BySize,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["minSizeBytes"] = "200",
                    ["maxSizeBytes"] = "1000"
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(2);
        result.Files.Should().Contain(f => f.FileName == "medium.txt");
        result.Files.Should().Contain(f => f.FileName == "large.txt");
        result.Files.Should().NotContain(f => f.FileName == "small.txt");
        result.Files.Should().NotContain(f => f.FileName == "extralarge.txt");
    }

    [Fact]
    public async Task PreviewCleanupAsync_WithMinSizeOnly_ShouldFilterAboveMin()
    {
        // Arrange
        CreateTestFile("tiny.dat", 10);
        CreateTestFile("big.dat", 5000);

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.BySize,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["minSizeBytes"] = "1000"
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(1);
        result.Files.Should().Contain(f => f.FileName == "big.dat");
    }

    #endregion

    #region CleanupService - ByDate Rule Tests

    [Fact]
    public async Task PreviewCleanupAsync_WithDateBeforeRule_ShouldFilterByModifiedDate()
    {
        // Arrange
        var oldFile = CreateTestFile("old.txt", 100);
        var newFile = CreateTestFile("new.txt", 200);
        File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-30));
        File.SetLastWriteTime(newFile, DateTime.Now.AddDays(-1));

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.ByDate,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["dateType"] = "modified",
                    ["beforeDate"] = DateTime.Now.AddDays(-7).ToString("o")
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(1);
        result.Files.Should().Contain(f => f.FileName == "old.txt");
        result.Files.Should().NotContain(f => f.FileName == "new.txt");
    }

    [Fact]
    public async Task PreviewCleanupAsync_WithDateAfterRule_ShouldFilterAfterDate()
    {
        // Arrange
        var oldFile = CreateTestFile("archived.txt", 100);
        var recentFile = CreateTestFile("recent.txt", 200);
        File.SetCreationTime(oldFile, DateTime.Now.AddDays(-60));
        File.SetCreationTime(recentFile, DateTime.Now.AddDays(-3));

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.ByDate,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["dateType"] = "created",
                    ["afterDate"] = DateTime.Now.AddDays(-7).ToString("o")
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(1);
        result.Files.Should().Contain(f => f.FileName == "recent.txt");
    }

    #endregion

    #region CleanupService - Multiple Rules Tests

    [Fact]
    public async Task PreviewCleanupAsync_WithMultipleRules_ShouldMatchAllRules()
    {
        // Arrange
        CreateTestFile("small_old.txt", 50);
        CreateTestFile("small_new.txt", 50);
        CreateTestFile("big_old.txt", 2000);
        CreateTestFile("big_new.txt", 2000);

        File.SetLastWriteTime(Path.Combine(_testDir, "small_old.txt"), DateTime.Now.AddDays(-30));
        File.SetLastWriteTime(Path.Combine(_testDir, "small_new.txt"), DateTime.Now);
        File.SetLastWriteTime(Path.Combine(_testDir, "big_old.txt"), DateTime.Now.AddDays(-30));
        File.SetLastWriteTime(Path.Combine(_testDir, "big_new.txt"), DateTime.Now);

        var rules = new List<CleanupRule>
        {
            new()
            {
                RuleType = CleanupRuleType.ByExtension,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["extensions"] = "txt",
                    ["mode"] = "include"
                }
            },
            new()
            {
                RuleType = CleanupRuleType.BySize,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["minSizeBytes"] = "1000"
                }
            },
            new()
            {
                RuleType = CleanupRuleType.ByDate,
                Enabled = true,
                Parameters = new Dictionary<string, object>
                {
                    ["dateType"] = "modified",
                    ["beforeDate"] = DateTime.Now.AddDays(-7).ToString("o")
                }
            }
        };

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(1);
        result.Files.Should().Contain(f => f.FileName == "big_old.txt");
    }

    [Fact]
    public async Task PreviewCleanupAsync_WithNoRules_ShouldReturnAllFiles()
    {
        // Arrange
        CreateTestFile("a.txt", 100);
        CreateTestFile("b.pdf", 200);
        CreateTestFile("c.jpg", 300);

        var rules = new List<CleanupRule>();

        // Act
        var result = await _cleanupService.PreviewCleanupAsync(_testDir, rules, false);

        // Assert
        result.FileCount.Should().Be(3);
    }

    [Fact]
    public async Task PreviewCleanupAsync_WithInvalidDirectory_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDir, "nonexistent");
        var rules = new List<CleanupRule>();

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _cleanupService.PreviewCleanupAsync(nonExistentDir, rules));
    }

    #endregion

    #region Helper Methods

    private List<string> CreateTestFiles(int count, string baseName, string extension)
    {
        var files = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var fileName = $"{baseName}_{i}{extension}";
            var filePath = Path.Combine(_testDir, fileName);
            File.WriteAllText(filePath, "test content");
            files.Add(filePath);
        }
        return files;
    }

    private string CreateTestFile(string fileName, long sizeInBytes)
    {
        var filePath = Path.Combine(_testDir, fileName);
        var content = new string('x', (int)sizeInBytes);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #endregion
}
