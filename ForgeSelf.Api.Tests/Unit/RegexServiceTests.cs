using ForgeSelf.Api.Plugins.DevTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class RegexServiceTests
{
    private readonly RegexService _service;

    public RegexServiceTests()
    {
        _service = new RegexService();
    }

    [Fact]
    public async Task TestMatchAsync_ValidPatternMatching_ReturnsMatchCount()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc 123 def 456";

        // Act
        var result = await _service.TestMatchAsync(pattern, input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(2);
    }

    [Fact]
    public async Task TestMatchAsync_NoMatch_ReturnsZeroCount()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc def ghi";

        // Act
        var result = await _service.TestMatchAsync(pattern, input);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task TestMatchAsync_EmptyPattern_ReturnsError()
    {
        // Act - 空模式返回错误结果而不是抛出异常
        var result = await _service.TestMatchAsync("", "input");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestMatchAsync_InvalidPattern_ReturnsError()
    {
        // Arrange
        var pattern = @"[invalid";

        // Act
        var result = await _service.TestMatchAsync(pattern, "input");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestMatchAsync_WithGroups_ReturnsGroupInfo()
    {
        // Arrange
        var pattern = @"(\w+)@(\w+)\.(\w+)";
        var input = "test@example.com";

        // Act
        var result = await _service.TestMatchAsync(pattern, input);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(1);
        result.Matches.Should().HaveCount(1);
        result.Matches[0].Groups.Should().HaveCount(3);
    }

    [Fact]
    public async Task TestMatchAsync_IgnoreCase_ReturnMatches()
    {
        // Arrange
        var pattern = @"test";
        var input = "TEST test Test";

        // Act
        var result = await _service.TestMatchAsync(pattern, input, ignoreCase: true);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(3);
    }

    [Fact]
    public async Task TestMatchAsync_NullInput_ReturnsZeroMatch()
    {
        // Arrange
        var pattern = @"\d+";

        // Act
        var result = await _service.TestMatchAsync(pattern, null!);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMatchGroupsAsync_ValidPattern_ReturnsGroups()
    {
        // Arrange
        var pattern = @"(\w+)\s+(\d+)";
        var input = "test 123";

        // Act
        var result = await _service.GetMatchGroupsAsync(pattern, input);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceAsync_ValidPattern_ReplacesMatches()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc 123 def 456";
        var replacement = "NUM";

        // Act
        var result = await _service.ReplaceAsync(pattern, input, replacement);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().Be("abc NUM def NUM");
        result.ReplacementCount.Should().Be(2);
    }

    [Fact]
    public async Task ReplaceAsync_NoMatch_ReturnsOriginal()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc def ghi";
        var replacement = "NUM";

        // Act
        var result = await _service.ReplaceAsync(pattern, input, replacement);

        // Assert
        result.Result.Should().Be(input);
        result.ReplacementCount.Should().Be(0);
    }

    [Fact]
    public async Task ReplaceAsync_EmptyPattern_ReturnsError()
    {
        // Act - 空模式返回错误结果而不是抛出异常
        var result = await _service.ReplaceAsync("", "input", "replacement");

        // Assert
        result.Should().NotBeNull();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReplaceAsync_NullReplacement_ReplacesWithEmpty()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc 123 def";

        // Act
        var result = await _service.ReplaceAsync(pattern, input, null!);

        // Assert
        result.Result.Should().Be("abc  def");
    }

    [Fact]
    public async Task SplitAsync_ValidPattern_SplitsString()
    {
        // Arrange
        var pattern = @"\s+";
        var input = "a b  c   d";

        // Act
        var result = await _service.SplitAsync(pattern, input);

        // Assert
        result.Should().NotBeNull();
        result.Parts.Should().HaveCount(4);
        result.Parts.Should().Contain("a");
        result.Parts.Should().Contain("b");
        result.Parts.Should().Contain("c");
        result.Parts.Should().Contain("d");
    }

    [Fact]
    public async Task SplitAsync_NoMatch_ReturnsSinglePart()
    {
        // Arrange
        var pattern = @"\d+";
        var input = "abc def";

        // Act
        var result = await _service.SplitAsync(pattern, input);

        // Assert
        result.Parts.Should().HaveCount(1);
        result.Parts[0].Should().Be("abc def");
    }

    [Fact]
    public async Task SplitAsync_EmptyPattern_ReturnsError()
    {
        // Act - 空模式返回错误结果而不是抛出异常
        var result = await _service.SplitAsync("", "input");

        // Assert
        result.Should().NotBeNull();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateRegexAsync_EmailKeyword_ReturnsEmailPattern()
    {
        // Act
        var result = await _service.GenerateRegexAsync("邮箱");

        // Assert
        result.Should().Contain("@");
        result.Should().Contain(@"\.");
    }

    [Fact]
    public async Task GenerateRegexAsync_PhoneKeyword_ReturnsPhonePattern()
    {
        // Act
        var result = await _service.GenerateRegexAsync("手机");

        // Assert
        result.Should().Contain("1[3-9]");
    }

    [Fact]
    public async Task GenerateRegexAsync_EmptyDescription_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateRegexAsync("");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*描述不能为空*");
    }

    [Fact]
    public async Task GenerateRegexAsync_WhitespaceDescription_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateRegexAsync("   ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*描述不能为空*");
    }

    [Fact]
    public async Task GenerateRegexAsync_UnknownKeyword_ReturnsDefaultMessage()
    {
        // Act
        var result = await _service.GenerateRegexAsync("unknown keyword xyz");

        // Assert
        result.Should().Contain("未能");
    }

    [Fact]
    public async Task GetCommonPatternsAsync_NoCategory_ReturnsAll()
    {
        // Act
        var result = await _service.GetCommonPatternsAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(p => p.Category == "常用验证");
        result.Should().Contain(p => p.Category == "网络");
    }

    [Fact]
    public async Task GetCommonPatternsAsync_WithCategory_ReturnsFiltered()
    {
        // Act
        var result = await _service.GetCommonPatternsAsync("网络");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(p => p.Category == "网络");
    }

    [Fact]
    public async Task GetCommonPatternsAsync_EmailPattern_HasValidPattern()
    {
        // Act
        var result = await _service.GetCommonPatternsAsync("常用验证");
        var emailPattern = result.FirstOrDefault(p => p.Name == "邮箱地址");

        // Assert
        emailPattern.Should().NotBeNull();
        emailPattern!.Pattern.Should().Contain("@");
    }

    [Fact]
    public async Task TestMatchAsync_MultilineMode_HandlesNewlines()
    {
        // Arrange
        var pattern = @"^test$";
        var input = "test\ntest\ntest";

        // Act
        var result = await _service.TestMatchAsync(pattern, input, multiline: true);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(3);
    }

    [Fact]
    public async Task TestMatchAsync_SinglelineMode_DotMatchesNewline()
    {
        // Arrange
        var pattern = @"a.*b";
        var input = "a\nb";

        // Act
        var result = await _service.TestMatchAsync(pattern, input, singleline: true);

        // Assert
        result.Success.Should().BeTrue();
        result.MatchCount.Should().Be(1);
    }

    [Fact]
    public async Task ReplaceAsync_WithCapturingGroups_ReplacesCorrectly()
    {
        // Arrange
        var pattern = @"(\w+)@(\w+)\.(\w+)";
        var input = "test@example.com";
        var replacement = "[$1]($2.$3)";

        // Act
        var result = await _service.ReplaceAsync(pattern, input, replacement);

        // Assert
        result.Result.Should().Contain("[test]");
        result.Result.Should().Contain("example.com");
    }
}
