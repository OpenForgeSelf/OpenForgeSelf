using OpenForgeSelf.Backend.Plugins.DevTools.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class EncodingServiceTests
{
    private readonly EncodingService _service;

    public EncodingServiceTests()
    {
        _service = new EncodingService();
    }

    [Fact]
    public async Task Base64EncodeAsync_ValidText_ReturnsBase64String()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.Base64EncodeAsync(text);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be("SGVsbG8sIFdvcmxkIQ==");
    }

    [Fact]
    public async Task Base64EncodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.Base64EncodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Base64EncodeAsync_NullString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.Base64EncodeAsync(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Base64DecodeAsync_ValidBase64_ReturnsOriginalText()
    {
        // Arrange
        var base64 = "SGVsbG8sIFdvcmxkIQ==";

        // Act
        var result = await _service.Base64DecodeAsync(base64);

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public async Task Base64DecodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.Base64DecodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Base64DecodeAsync_Base64WithWhitespace_IgnoresWhitespace()
    {
        // Arrange - Base64 字符串中间有换行和空格
        var base64 = "SGVs bG8s IFdv cmxk IQ==";

        // Act
        var result = await _service.Base64DecodeAsync(base64);

        // Assert
        result.Should().Be("Hello, World!");
    }

    [Fact]
    public async Task Base64DecodeAsync_InvalidBase64_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.Base64DecodeAsync("not-valid-base64!!!");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无效的Base64字符串*");
    }

    [Fact]
    public async Task UrlEncodeAsync_ValidText_ReturnsUrlEncodedString()
    {
        // Arrange
        var text = "Hello World&foo=bar";

        // Act
        var result = await _service.UrlEncodeAsync(text);

        // Assert
        result.Should().Contain("Hello+World"); // WebUtility.UrlEncode encodes spaces as +
        result.Should().Contain("%26"); // &
    }

    [Fact]
    public async Task UrlEncodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.UrlEncodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UrlDecodeAsync_EncodedText_ReturnsDecodedString()
    {
        // Arrange
        var encoded = "Hello%20World%26foo%3Dbar";

        // Act
        var result = await _service.UrlDecodeAsync(encoded);

        // Assert
        result.Should().Be("Hello World&foo=bar");
    }

    [Fact]
    public async Task UrlDecodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.UrlDecodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UnicodeEncodeAsync_ChineseText_ReturnsUnicodeEscaped()
    {
        // Arrange
        var text = "你好";

        // Act
        var result = await _service.UnicodeEncodeAsync(text);

        // Assert
        result.Should().Contain("\\u");
        result.Should().Contain("4f60"); // 你 (lowercase hex from Encoding service)
        result.Should().Contain("597d"); // 好
    }

    [Fact]
    public async Task UnicodeEncodeAsync_AsciiText_ReturnsOriginalText()
    {
        // Arrange
        var text = "Hello";

        // Act
        var result = await _service.UnicodeEncodeAsync(text);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public async Task UnicodeEncodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.UnicodeEncodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UnicodeDecodeAsync_EscapedText_ReturnsDecodedString()
    {
        // Arrange
        var escaped = "\\u4F60\\u597D";

        // Act
        var result = await _service.UnicodeDecodeAsync(escaped);

        // Assert
        result.Should().Be("你好");
    }

    [Fact]
    public async Task UnicodeDecodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.UnicodeDecodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HtmlEncodeAsync_SpecialChars_ReturnsHtmlEncoded()
    {
        // Arrange
        var text = "<script>alert('XSS')</script>";

        // Act
        var result = await _service.HtmlEncodeAsync(text);

        // Assert
        result.Should().Contain("&lt;");
        result.Should().Contain("&gt;");
        result.Should().Contain("&#39;"); // WebUtility.HtmlEncode encodes ' as &#39;
    }

    [Fact]
    public async Task HtmlEncodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.HtmlEncodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HtmlDecodeAsync_EncodedHtml_ReturnsDecodedString()
    {
        // Arrange
        var encoded = "&lt;script&gt;alert(&apos;XSS&apos;)&lt;/script&gt;";

        // Act
        var result = await _service.HtmlDecodeAsync(encoded);

        // Assert
        result.Should().Contain("<script>");
    }

    [Fact]
    public async Task HtmlDecodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.HtmlDecodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HexEncodeAsync_ValidText_ReturnsHexString()
    {
        // Arrange
        var text = "Hello";

        // Act
        var result = await _service.HexEncodeAsync(text);

        // Assert - BitConverter.ToString returns uppercase, then ToLowerInvariant makes it lowercase
        // "Hello" in UTF8 bytes = {72, 101, 108, 108, 111} = "48656c6c6f"
        result.Should().Be("48656c6c6f");
        result.Should().NotContain("-");
    }

    [Fact]
    public async Task HexEncodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.HexEncodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HexDecodeAsync_ValidHex_ReturnsOriginalText()
    {
        // Arrange - BitConverter uses uppercase, so input should be uppercase
        var hex = "48656c6c6f";

        // Act
        var result = await _service.HexDecodeAsync(hex);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public async Task HexDecodeAsync_HexWithSpaces_ReturnsOriginalText()
    {
        // Arrange - "Hello" bytes are {72, 101, 108, 108, 111} = "48 65 6c 6c 6f"
        var hex = "48 65 6c 6c 6f";

        // Act
        var result = await _service.HexDecodeAsync(hex);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public async Task HexDecodeAsync_HexWithDashes_ReturnsOriginalText()
    {
        // Arrange - BitConverter uses uppercase hex
        var hex = "48-65-6c-6c-6f";

        // Act
        var result = await _service.HexDecodeAsync(hex);

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public async Task HexDecodeAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.HexDecodeAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HexDecodeAsync_OddLengthHex_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.HexDecodeAsync("123");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无效的十六进制字符串*");
    }

    [Fact]
    public async Task HexDecodeAsync_InvalidHex_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.HexDecodeAsync("GGGGGGGG");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Roundtrip_Base64EncodeDecode_ReturnsOriginal()
    {
        // Arrange
        var original = "测试文本 Test 123 !@#$%";

        // Act
        var encoded = await _service.Base64EncodeAsync(original);
        var decoded = await _service.Base64DecodeAsync(encoded);

        // Assert
        decoded.Should().Be(original);
    }

    [Fact]
    public async Task Roundtrip_UrlEncodeDecode_ReturnsOriginal()
    {
        // Arrange
        var original = "Hello World&foo=bar?test=1";

        // Act
        var encoded = await _service.UrlEncodeAsync(original);
        var decoded = await _service.UrlDecodeAsync(encoded);

        // Assert
        decoded.Should().Be(original);
    }

    [Fact]
    public async Task Roundtrip_HexEncodeDecode_ReturnsOriginal()
    {
        // Arrange
        var original = "Test Data 123";

        // Act
        var encoded = await _service.HexEncodeAsync(original);
        var decoded = await _service.HexDecodeAsync(encoded);

        // Assert
        decoded.Should().Be(original);
    }
}
