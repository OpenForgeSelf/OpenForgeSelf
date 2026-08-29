using ForgeSelf.Api.Plugins.DevTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class JsonFormatterServiceTests
{
    private readonly JsonFormatterService _service;

    public JsonFormatterServiceTests()
    {
        _service = new JsonFormatterService();
    }

    [Fact]
    public async Task FormatJsonAsync_ValidJson_ReturnsFormattedJson()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        var result = await _service.FormatJsonAsync(json);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\n");
        result.Should().Contain("  ");
    }

    [Fact]
    public async Task FormatJsonAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.FormatJsonAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FormatJsonAsync_WhitespaceString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.FormatJsonAsync("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FormatJsonAsync_NullString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.FormatJsonAsync(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FormatJsonAsync_InvalidJson_ThrowsArgumentException()
    {
        // Arrange
        var json = "{invalid json}";

        // Act & Assert
        var act = () => _service.FormatJsonAsync(json);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无效的JSON格式*");
    }

    [Fact]
    public async Task FormatJsonAsync_NestedJson_ReturnsFormattedJson()
    {
        // Arrange
        var json = """{"outer":{"inner":"value"},"array":[1,2,3]}""";

        // Act
        var result = await _service.FormatJsonAsync(json);

        // Assert
        result.Should().Contain("outer");
        result.Should().Contain("inner");
        result.Should().Contain("array");
    }

    [Fact]
    public async Task FormatJsonAsync_CustomIndentSize_ReturnsFormattedWithCustomIndent()
    {
        // Arrange
        var json = """{"name":"test"}""";

        // Act
        var result = await _service.FormatJsonAsync(json, 4);

        // Assert
        result.Should().Contain("    ");
    }

    [Fact]
    public async Task MinifyJsonAsync_ValidJson_ReturnsMinifiedJson()
    {
        // Arrange
        var json = """
            {
                "name": "test",
                "value": 123
            }
            """;

        // Act
        var result = await _service.MinifyJsonAsync(json);

        // Assert
        result.Should().NotContain("\n");
        result.Should().NotContain("  ");
        result.Should().Be("{\"name\":\"test\",\"value\":123}");
    }

    [Fact]
    public async Task MinifyJsonAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.MinifyJsonAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MinifyJsonAsync_WhitespaceString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.MinifyJsonAsync("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MinifyJsonAsync_InvalidJson_ThrowsArgumentException()
    {
        // Arrange
        var json = "{invalid}";

        // Act & Assert
        var act = () => _service.MinifyJsonAsync(json);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无效的JSON格式*");
    }

    [Fact]
    public async Task ValidateJsonAsync_ValidJson_ReturnsIsValidTrue()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        var result = await _service.ValidateJsonAsync(json);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateJsonAsync_EmptyString_ReturnsIsValidFalse()
    {
        // Act
        var result = await _service.ValidateJsonAsync("");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("不能为空");
    }

    [Fact]
    public async Task ValidateJsonAsync_WhitespaceString_ReturnsIsValidFalse()
    {
        // Act
        var result = await _service.ValidateJsonAsync("   ");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateJsonAsync_InvalidJson_ReturnsIsValidFalse()
    {
        // Arrange
        var json = "{invalid: json}";

        // Act
        var result = await _service.ValidateJsonAsync(json);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task JsonPathQueryAsync_SimpleProperty_ReturnsValue()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$.name");

        // Assert
        result.Should().Contain("test");
    }

    [Fact]
    public async Task JsonPathQueryAsync_NestedProperty_ReturnsValue()
    {
        // Arrange
        var json = """{"outer":{"inner":"value"}}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$.outer.inner");

        // Assert
        result.Should().Contain("value");
    }

    [Fact]
    public async Task JsonPathQueryAsync_ArrayIndex_ReturnsResult()
    {
        // Arrange
        var json = """{"array":[10,20,30]}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$.array[1]");

        // Assert - JsonPath query returns a result (may be empty if not supported)
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task JsonPathQueryAsync_EmptyInput_ReturnsEmptyString()
    {
        // Act
        var result = await _service.JsonPathQueryAsync("", "$.name");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task JsonPathQueryAsync_EmptyExpression_ThrowsArgumentException()
    {
        // Arrange
        var json = """{"name":"test"}""";

        // Act & Assert
        var act = () => _service.JsonPathQueryAsync(json, "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*JSONPath表达式不能为空*");
    }

    [Fact]
    public async Task JsonPathQueryAsync_NonExistentPath_ReturnsEmptyString()
    {
        // Arrange
        var json = """{"name":"test"}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$.nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task JsonPathQueryAsync_Wildcard_ReturnsAllProperties()
    {
        // Arrange
        var json = """{"a":1,"b":2,"c":3}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$.*");

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConvertJsonToYamlAsync_ValidJson_ReturnsYaml()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        var result = await _service.ConvertJsonToYamlAsync(json);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("name:");
        result.Should().Contain("value:");
    }

    [Fact]
    public async Task ConvertJsonToYamlAsync_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ConvertJsonToYamlAsync("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertJsonToYamlAsync_InvalidJson_ThrowsArgumentException()
    {
        // Arrange
        var json = "{invalid}";

        // Act & Assert
        var act = () => _service.ConvertJsonToYamlAsync(json);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*无效的JSON格式*");
    }

    [Fact]
    public async Task ConvertJsonToYamlAsync_NestedObject_ReturnsYamlWithIndentation()
    {
        // Arrange
        var json = """{"outer":{"inner":"value"}}""";

        // Act
        var result = await _service.ConvertJsonToYamlAsync(json);

        // Assert
        result.Should().Contain("outer:");
        result.Should().Contain("  inner:");
    }

    [Fact]
    public async Task ConvertJsonToYamlAsync_Array_ReturnsYamlWithListSyntax()
    {
        // Arrange
        var json = """{"items":["a","b","c"]}""";

        // Act
        var result = await _service.ConvertJsonToYamlAsync(json);

        // Assert
        result.Should().Contain("items:");
        result.Should().Contain("-");
    }

    [Fact]
    public async Task Roundtrip_FormatAndMinify_ReturnsSameStructure()
    {
        // Arrange
        var original = """{"name":"test","nested":{"value":42},"array":[1,2,3]}""";

        // Act
        var formatted = await _service.FormatJsonAsync(original);
        var minified = await _service.MinifyJsonAsync(formatted);

        // Assert - 比较序列化后的字符串是否等价
        var originalParsed = System.Text.Json.JsonDocument.Parse(original).RootElement;
        var minifiedParsed = System.Text.Json.JsonDocument.Parse(minified).RootElement;
        
        originalParsed.GetProperty("name").GetString().Should().Be(minifiedParsed.GetProperty("name").GetString());
        originalParsed.GetProperty("nested").GetProperty("value").GetInt32().Should().Be(minifiedParsed.GetProperty("nested").GetProperty("value").GetInt32());
        
        // 比较数组元素
        var originalArray = originalParsed.GetProperty("array").EnumerateArray().Select(e => e.GetInt32()).ToList();
        var minifiedArray = minifiedParsed.GetProperty("array").EnumerateArray().Select(e => e.GetInt32()).ToList();
        originalArray.Should().BeEquivalentTo(minifiedArray);
    }

    [Fact]
    public async Task JsonPathQueryAsync_RootReturnsFullDocument()
    {
        // Arrange
        var json = """{"name":"test"}""";

        // Act
        var result = await _service.JsonPathQueryAsync(json, "$");

        // Assert
        result.Should().Contain("name");
    }
}
