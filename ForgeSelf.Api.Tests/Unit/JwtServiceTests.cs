using ForgeSelf.Api.Plugins.DevTools.Services;

namespace ForgeSelf.Api.Tests.Unit;

public class JwtServiceTests
{
    private readonly JwtService _service;

    public JwtServiceTests()
    {
        _service = new JwtService();
    }

    [Fact]
    public async Task DecodeJwtAsync_ValidToken_ReturnsDecodedResult()
    {
        // Arrange - 生成一个有效的 JWT token
        var payload = new Dictionary<string, object> { { "sub", "test" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.DecodeJwtAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Header.Should().NotBeNull();
        result.Payload.Should().NotBeNull();
        result.Signature.Should().NotBeNullOrEmpty();
        result.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task DecodeJwtAsync_EmptyToken_ReturnsFailedResult()
    {
        // Act - 空字符串不抛出异常，而是返回失败的 result
        var result = await _service.DecodeJwtAsync("");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DecodeJwtAsync_WhitespaceToken_ReturnsFailedResult()
    {
        // Act - 空白字符串不抛出异常，而是返回失败的 result
        var result = await _service.DecodeJwtAsync("   ");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DecodeJwtAsync_InvalidFormat_ReturnsFailedResult()
    {
        // Act - 无效格式不抛出异常，而是返回失败的 result
        var result = await _service.DecodeJwtAsync("not.a.valid.token");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret123", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.ValidateSignatureAsync(token, "secret123", "HS256");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSignatureAsync_InvalidSecret_ReturnsFalse()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret123", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.ValidateSignatureAsync(token, "wrong_secret", "HS256");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateSignatureAsync_EmptyToken_ReturnsFailedResult()
    {
        // Act - 空字符串不抛出异常，而是返回失败的 result
        var result = await _service.ValidateSignatureAsync("", "secret");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSignatureAsync_EmptySecret_ReturnsFailedResult()
    {
        // Act - 空密钥不抛出异常，而是返回失败的 result
        var result = await _service.ValidateSignatureAsync("token", "");

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task IsExpiredAsync_ValidToken_ReturnsFalse()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.IsExpiredAsync(token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetClaimsAsync_ValidToken_ReturnsPayload()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" }, { "name", "John" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.GetClaimsAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("sub");
        result.Should().ContainKey("name");
    }

    [Fact]
    public async Task GenerateJwtAsync_ValidParams_ReturnsToken()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };

        // Act
        var result = await _service.GenerateJwtAsync(payload, "secret", "HS256", 60);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.Token.Split('.').Should().HaveCount(3);
        result.IssuedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Expiration.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateJwtAsync_NullPayload_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateJwtAsync(null!, "secret", "HS256", 60);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Payload 不能为空*");
    }

    [Fact]
    public async Task GenerateJwtAsync_EmptySecret_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.GenerateJwtAsync(new Dictionary<string, object>(), "", "HS256", 60);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*密钥不能为空*");
    }

    [Fact]
    public async Task GenerateJwtAsync_WithoutExpiration_ReturnsTokenWithoutExp()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };

        // Act
        var result = await _service.GenerateJwtAsync(payload, "secret", "HS256", null);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.Expiration.Should().BeNull();
    }

    [Fact]
    public async Task GenerateJwtAsync_HS384Algorithm_ReturnsValidToken()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };

        // Act
        var result = await _service.GenerateJwtAsync(payload, "secret", "HS384", 60);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateJwtAsync_HS512Algorithm_ReturnsValidToken()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };

        // Act
        var result = await _service.GenerateJwtAsync(payload, "secret", "HS512", 60);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetHeaderInfoAsync_ValidToken_ReturnsHeaderInfo()
    {
        // Arrange
        var payload = new Dictionary<string, object> { { "sub", "test" } };
        var generateResult = await _service.GenerateJwtAsync(payload, "secret", "HS256", 60);
        var token = generateResult.Token;

        // Act
        var result = await _service.GetHeaderInfoAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.Alg.Should().Be("HS256");
        result.Typ.Should().Be("JWT");
    }
}
