using OpenForgeSelf.Backend.Plugins.DevTools.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class QrCodeServiceTests
{
    private readonly QrCodeService _service;

    public QrCodeServiceTests()
    {
        _service = new QrCodeService();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_ValidText_ReturnsBase64Image()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("https://example.com", 256, "M", 4);

        // Assert
        result.Should().NotBeNull();
        result.ImageBase64.Should().NotBeNullOrEmpty();
        result.Size.Should().Be(256);
        result.Level.Should().Be("M");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_EmptyText_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("", 256, "M", 4);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*二维码内容不能为空*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_WhitespaceText_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("   ", 256, "M", 4);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*二维码内容不能为空*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_SizeTooSmall_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("test", 100, "M", 4);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*图片大小必须在 128-1024 像素之间*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_SizeTooLarge_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("test", 2000, "M", 4);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*图片大小必须在 128-1024 像素之间*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_NegativeMargin_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("test", 256, "M", -1);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*边距必须在 0-20 之间*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_MarginTooLarge_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("test", 256, "M", 25);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*边距必须在 0-20 之间*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_InvalidLevel_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateQrCodeAsync("test", 256, "X", 4);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*不支持的容错级别*");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_LevelL_ReturnsValidQrCode()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("test data", 256, "L", 4);

        // Assert
        result.Level.Should().Be("L");
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_LevelQ_ReturnsValidQrCode()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("test data", 256, "Q", 4);

        // Assert
        result.Level.Should().Be("Q");
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_LevelH_ReturnsValidQrCode()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("test data", 256, "H", 4);

        // Assert
        result.Level.Should().Be("H");
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateCustomQrCodeAsync_ValidParams_ReturnsColoredQrCode()
    {
        // Act
        var result = await _service.GenerateCustomQrCodeAsync("https://example.com", 256, "M", 4, "#FF0000", "#FFFFFF");

        // Assert
        result.Should().NotBeNull();
        result.ImageBase64.Should().NotBeNullOrEmpty();
        result.Size.Should().Be(256);
        result.Level.Should().Be("M");
    }

    [Fact]
    public async Task GenerateCustomQrCodeAsync_EmptyText_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateCustomQrCodeAsync("", 256, "M", 4, "#FF0000", "#FFFFFF");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*二维码内容不能为空*");
    }

    [Fact]
    public async Task GenerateCustomQrCodeAsync_InvalidSize_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateCustomQrCodeAsync("test", 50, "M", 4, "#FF0000", "#FFFFFF");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DecodeQrCodeAsync_AnyInput_ReturnsNotImplemented()
    {
        // Act
        var result = await _service.DecodeQrCodeAsync("any_base64_string");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("暂未实现");
    }

    [Fact]
    public async Task GenerateQrCodeAsync_MinimumValidSize_ReturnsValidQrCode()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("min", 128, "M", 0);

        // Assert
        result.Should().NotBeNull();
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_MaximumValidSize_ReturnsValidQrCode()
    {
        // Act
        var result = await _service.GenerateQrCodeAsync("max", 1024, "M", 20);

        // Assert
        result.Should().NotBeNull();
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateQrCodeAsync_LongText_ReturnsValidQrCode()
    {
        // Arrange - 生成一个较长的文本
        var longText = new string('a', 500);

        // Act
        var result = await _service.GenerateQrCodeAsync(longText, 512, "H", 4);

        // Assert
        result.Should().NotBeNull();
        result.ImageBase64.Should().NotBeNullOrEmpty();
    }
}
