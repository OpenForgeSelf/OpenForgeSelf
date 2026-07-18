using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;
using Microsoft.Extensions.Configuration;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 配置服务单元测试
/// </summary>
public class ConfigurationServiceTests
{
    private readonly Mock<ILogService> _mockLogService;

    public ConfigurationServiceTests()
    {
        _mockLogService = new Mock<ILogService>();
    }

    /// <summary>
    /// 创建模拟配置
    /// </summary>
    private IConfiguration CreateMockConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void GetAIConfig_WhenAISectionExists_ShouldReturnCorrectConfig()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "https://api.example.com/v1/chat" },
            { "AI:ApiKey", "test-api-key-12345" },
            { "AI:ModelName", "gpt-4" }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.Should().NotBeNull();
        config.ApiEndpoint.Should().Be("https://api.example.com/v1/chat");
        config.ApiKey.Should().Be("test-api-key-12345");
        config.ModelName.Should().Be("gpt-4");
    }

    [Fact]
    public void GetAIConfig_WhenAISectionNotExists_ShouldReturnEmptyConfig()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "OtherSetting", "SomeValue" }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.Should().NotBeNull();
        config.ApiEndpoint.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
        config.ModelName.Should().BeEmpty();
    }

    [Fact]
    public void GetAIConfig_WhenPartialConfigExists_ShouldReturnPartialConfig()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "https://api.example.com/v1/chat" },
            { "AI:ModelName", "gpt-3.5-turbo" }
            // ApiKey is missing
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.Should().NotBeNull();
        config.ApiEndpoint.Should().Be("https://api.example.com/v1/chat");
        config.ApiKey.Should().BeEmpty();
        config.ModelName.Should().Be("gpt-3.5-turbo");
    }

    [Fact]
    public void GetAIConfig_WhenCalledMultipleTimes_ShouldReturnSameConfig()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "https://api.example.com/v1/chat" },
            { "AI:ApiKey", "test-key" },
            { "AI:ModelName", "gpt-4" }
        };
        var configuration = CreateMockConfiguration(settings);
        var service = new ConfigurationService(configuration);

        // Act
        var config1 = service.GetAIConfig();
        var config2 = service.GetAIConfig();

        // Assert
        config1.Should().BeSameAs(config2);
    }

    [Fact]
    public void GetAIConfig_WithEmptyValues_ShouldReturnEmptyStrings()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "" },
            { "AI:ApiKey", "" },
            { "AI:ModelName", "" }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.Should().NotBeNull();
        config.ApiEndpoint.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
        config.ModelName.Should().BeEmpty();
    }

    [Fact]
    public void GetAIConfig_WithNullValues_ShouldReturnEmptyStrings()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", null },
            { "AI:ApiKey", null },
            { "AI:ModelName", null }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.Should().NotBeNull();
        config.ApiEndpoint.Should().BeEmpty();
        config.ApiKey.Should().BeEmpty();
        config.ModelName.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitializeSuccessfully()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "https://api.test.com" },
            { "AI:ApiKey", "key" },
            { "AI:ModelName", "model" }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act & Assert - 不应抛出异常
        var action = () => new ConfigurationService(configuration);
        action.Should().NotThrow();
    }

    [Fact]
    public void GetAIConfig_WithSpecialCharacters_ShouldReturnCorrectValues()
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            { "AI:ApiEndpoint", "https://api.example.com/v1/chat?version=2&format=json" },
            { "AI:ApiKey", "sk-test+key/with=special&chars!@#$%" },
            { "AI:ModelName", "gpt-4-turbo-preview" }
        };
        var configuration = CreateMockConfiguration(settings);

        // Act
        var service = new ConfigurationService(configuration);
        var config = service.GetAIConfig();

        // Assert
        config.ApiEndpoint.Should().Be("https://api.example.com/v1/chat?version=2&format=json");
        config.ApiKey.Should().Be("sk-test+key/with=special&chars!@#$%");
        config.ModelName.Should().Be("gpt-4-turbo-preview");
    }
}