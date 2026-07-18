using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using OpenForgeSelf.Backend.Plugins.DevTools.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class ColorServiceTests
{
    private readonly ColorService _service;

    public ColorServiceTests()
    {
        _service = new ColorService();
    }

    [Fact]
    public async Task ConvertColorAsync_HexFormat_ReturnsAllFormats()
    {
        // Arrange
        var request = new ColorConvertRequest { Hex = "#FF5733" };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Hex.Should().Be("#ff5733");
        result.R.Should().Be(255);
        result.G.Should().Be(87);
        result.B.Should().Be(51);
        result.A.Should().Be(1.0);
        result.HexWithAlpha.Should().StartWith("#ff5733");
        result.RgbString.Should().Be("rgb(255, 87, 51)");
        result.RgbaString.Should().StartWith("rgba(255, 87, 51, 1)");
    }

    [Fact]
    public async Task ConvertColorAsync_RGBFormat_ReturnsCorrectHex()
    {
        // Arrange
        var request = new ColorConvertRequest { R = 100, G = 150, B = 200 };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Hex.Should().Be("#6496c8");
        result.R.Should().Be(100);
        result.G.Should().Be(150);
        result.B.Should().Be(200);
    }

    [Fact]
    public async Task ConvertColorAsync_RGBWithAlpha_ReturnsAlphaValue()
    {
        // Arrange
        var request = new ColorConvertRequest { R = 100, G = 150, B = 200, Alpha = 0.5 };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.A.Should().Be(0.5);
    }

    [Fact]
    public async Task ConvertColorAsync_RGBClampsValues_ReturnsClampedValues()
    {
        // Arrange - 测试超出范围的值
        var request = new ColorConvertRequest { R = 300, G = -50, B = 255 };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert - 值应该被限制在 0-255 之间
        result.R.Should().Be(255);
        result.G.Should().Be(0);
        result.B.Should().Be(255);
    }

    [Fact]
    public async Task ConvertColorAsync_HSLFormat_ReturnsCorrectRGB()
    {
        // Arrange - 红色 H=0, S=100, L=50
        var request = new ColorConvertRequest { H = 0, S = 100, L = 50 };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.R.Should().Be(255);
        result.G.Should().Be(0);
        result.B.Should().Be(0);
    }

    [Fact]
    public async Task ConvertColorAsync_EmptyRequest_ThrowsArgumentException()
    {
        // Arrange
        var request = new ColorConvertRequest();

        // Act & Assert
        var act = () => _service.ConvertColorAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*请提供至少一种颜色格式*");
    }

    [Fact]
    public async Task ConvertColorAsync_InvalidHex_ThrowsArgumentException()
    {
        // Arrange
        var request = new ColorConvertRequest { Hex = "not-a-color" };

        // Act & Assert
        var act = () => _service.ConvertColorAsync(request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConvertColorAsync_HexWithHash_ReturnsSameResult()
    {
        // Arrange
        var request1 = new ColorConvertRequest { Hex = "#FF5733" };
        var request2 = new ColorConvertRequest { Hex = "FF5733" };

        // Act
        var result1 = await _service.ConvertColorAsync(request1);
        var result2 = await _service.ConvertColorAsync(request2);

        // Assert
        result1.R.Should().Be(result2.R);
        result1.G.Should().Be(result2.G);
        result1.B.Should().Be(result2.B);
    }

    [Fact]
    public async Task ConvertColorAsync_HexWithAlpha_ReturnsAlphaHex()
    {
        // Arrange
        var request = new ColorConvertRequest { Hex = "#FF573380" };

        // Act
        var result = await _service.ConvertColorAsync(request);

        // Assert
        result.HexWithAlpha.Should().HaveLength(9); // # + 6 hex + 2 alpha
        result.A.Should().BeLessThan(1.0);
    }

    [Fact]
    public async Task GeneratePaletteAsync_AnalogousScheme_ReturnsColors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 5, "analogous");

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().HaveCount(5);
        result.BaseColor.Should().Be("#FF5733");
        result.Scheme.Should().Be("analogous");
    }

    [Fact]
    public async Task GeneratePaletteAsync_ComplementaryScheme_ReturnsColors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 4, "complementary");

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().NotBeEmpty();
        result.Scheme.Should().Be("complementary");
    }

    [Fact]
    public async Task GeneratePaletteAsync_TriadicScheme_ReturnsColors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 3, "triadic");

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task GeneratePaletteAsync_SplitComplementaryScheme_ReturnsColors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 5, "split-complementary");

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GeneratePaletteAsync_MonochromaticScheme_ReturnsColors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 5, "monochromatic");

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().HaveCount(5);
    }

    [Fact]
    public async Task GeneratePaletteAsync_InvalidScheme_DefaultsToAnalogous()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 5, "invalid-scheme");

        // Assert
        result.Scheme.Should().Be("invalid-scheme");
        result.Colors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GeneratePaletteAsync_CountClampedTo12_ReturnsMax12Colors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 20, "analogous");

        // Assert
        result.Colors.Should().HaveCountLessOrEqualTo(12);
    }

    [Fact]
    public async Task GeneratePaletteAsync_CountClampedTo2_ReturnsMin2Colors()
    {
        // Act
        var result = await _service.GeneratePaletteAsync("#FF5733", 1, "analogous");

        // Assert
        result.Colors.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task CheckContrastAsync_BlackOnWhite_ReturnsHighRatio()
    {
        // Act
        var result = await _service.CheckContrastAsync("#000000", "#FFFFFF");

        // Assert
        result.Should().NotBeNull();
        result.Ratio.Should().BeGreaterThan(20);
        result.AAANormal.Should().BeTrue();
    }

    [Fact]
    public async Task CheckContrastAsync_SameColors_ReturnsRatio1()
    {
        // Act
        var result = await _service.CheckContrastAsync("#FF5733", "#FF5733");

        // Assert
        result.Ratio.Should().Be(1.0);
        result.AANormal.Should().BeFalse();
        result.Level.Should().Contain("Fail");
    }

    [Fact]
    public async Task CheckContrastAsync_WhiteOnBlack_ReturnsHighRatio()
    {
        // Act
        var result = await _service.CheckContrastAsync("#FFFFFF", "#000000");

        // Assert
        result.Should().NotBeNull();
        result.Ratio.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task CheckContrastAsync_AACompliant_ReturnsCorrectFlags()
    {
        // Arrange - WCAG AA 要求正常文本对比度 >= 4.5
        var result = await _service.CheckContrastAsync("#000000", "#767676");

        // Assert
        result.AANormal.Should().Be(result.Ratio >= 4.5);
    }
}
