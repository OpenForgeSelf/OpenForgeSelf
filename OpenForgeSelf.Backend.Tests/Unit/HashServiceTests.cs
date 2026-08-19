using OpenForgeSelf.Backend.Plugins.DevTools.Services;

namespace OpenForgeSelf.Backend.Tests.Unit;

public class HashServiceTests
{
    private readonly HashService _service;

    public HashServiceTests()
    {
        _service = new HashService();
    }

    [Fact]
    public async Task ComputeMd5Async_ValidText_ReturnsMd5Hash()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.ComputeMd5Async(text);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(32);
        result.Should().Be("65a8e27d8879283831b664bd8b7f0ad4");
    }

    [Fact]
    public async Task ComputeMd5Async_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ComputeMd5Async("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeMd5Async_SameInput_ReturnsSameHash()
    {
        // Arrange
        var text = "test";

        // Act
        var result1 = await _service.ComputeMd5Async(text);
        var result2 = await _service.ComputeMd5Async(text);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task ComputeSha1Async_ValidText_ReturnsSha1Hash()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.ComputeSha1Async(text);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(40);
    }

    [Fact]
    public async Task ComputeSha1Async_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ComputeSha1Async("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeSha256Async_ValidText_ReturnsSha256Hash()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.ComputeSha256Async(text);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64);
    }

    [Fact]
    public async Task ComputeSha256Async_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ComputeSha256Async("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeSha512Async_ValidText_ReturnsSha512Hash()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.ComputeSha512Async(text);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(128);
    }

    [Fact]
    public async Task ComputeSha512Async_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ComputeSha512Async("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAllHashesAsync_ValidText_ReturnsAllHashes()
    {
        // Arrange
        var text = "Hello, World!";

        // Act
        var result = await _service.ComputeAllHashesAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Md5.Should().NotBeNullOrEmpty();
        result.Sha1.Should().NotBeNullOrEmpty();
        result.Sha256.Should().NotBeNullOrEmpty();
        result.Sha512.Should().NotBeNullOrEmpty();
        result.Md5.Should().HaveLength(32);
        result.Sha1.Should().HaveLength(40);
        result.Sha256.Should().HaveLength(64);
        result.Sha512.Should().HaveLength(128);
    }

    [Fact]
    public async Task ComputeHmacAsync_ValidInput_ReturnsHmacHash()
    {
        // Arrange
        var text = "Hello";
        var key = "secret";

        // Act
        var result = await _service.ComputeHmacAsync(text, key, "sha256");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64);
    }

    [Fact]
    public async Task ComputeHmacAsync_EmptyText_ReturnsEmptyString()
    {
        // Act
        var result = await _service.ComputeHmacAsync("", "key", "sha256");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeHmacAsync_EmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.ComputeHmacAsync("text", "", "sha256");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*密钥不能为空*");
    }

    [Fact]
    public async Task ComputeHmacAsync_Sha256Algorithm_ReturnsValidHmac()
    {
        // Arrange
        var text = "test";
        var key = "key";

        // Act
        var result = await _service.ComputeHmacAsync(text, key, "sha256");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ComputeHmacAsync_Sha384Algorithm_ReturnsValidHmac()
    {
        // Arrange
        var text = "test";
        var key = "key";

        // Act
        var result = await _service.ComputeHmacAsync(text, key, "sha384");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(96);
    }

    [Fact]
    public async Task ComputeHmacAsync_Sha512Algorithm_ReturnsValidHmac()
    {
        // Arrange
        var text = "test";
        var key = "key";

        // Act
        var result = await _service.ComputeHmacAsync(text, key, "sha512");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(128);
    }

    [Fact]
    public async Task ComputeHmacAsync_Md5Algorithm_ReturnsValidHmac()
    {
        // Arrange
        var text = "test";
        var key = "key";

        // Act
        var result = await _service.ComputeHmacAsync(text, key, "md5");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(32);
    }

    [Fact]
    public async Task AesEncryptAsync_ValidInput_ReturnsEncryptedBase64()
    {
        // Arrange
        var text = "Hello, World!";
        var key = "mysecretkey12345";

        // Act
        var result = await _service.AesEncryptAsync(text, key);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // AES 加密结果应该是有效的 Base64
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AesEncryptAsync_EmptyText_ReturnsEmptyString()
    {
        // Act
        var result = await _service.AesEncryptAsync("", "key");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AesEncryptAsync_EmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.AesEncryptAsync("text", "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*密钥不能为空*");
    }

    [Fact]
    public async Task AesDecryptAsync_ValidEncrypted_ReturnsDecryptedText()
    {
        // Arrange
        var original = "Hello, World!";
        var key = "mysecretkey12345";
        var encrypted = await _service.AesEncryptAsync(original, key);

        // Act
        var result = await _service.AesDecryptAsync(encrypted, key);

        // Assert
        result.Should().Be(original);
    }

    [Fact]
    public async Task AesDecryptAsync_EmptyText_ReturnsEmptyString()
    {
        // Act
        var result = await _service.AesDecryptAsync("", "key");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AesDecryptAsync_EmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _service.AesDecryptAsync("text", "");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*密钥不能为空*");
    }

    [Fact]
    public async Task AesDecryptAsync_WrongKey_ThrowsException()
    {
        // Arrange
        var original = "Hello, World!";
        var key = "mysecretkey12345";
        var wrongKey = "wrongkey12345678";
        var encrypted = await _service.AesEncryptAsync(original, key);

        // Act
        // 根因：CBC+PKCS7 无认证，错误密钥有约 1/256 概率解出合法 padding（末字节恰为 0x01）
        // 而不抛异常，此时会返回乱码明文。因此断言放宽为「抛异常 或 明文不等于原文」二者之一成立，
        // 避免该概率性场景导致测试偶发失败。
        string? decrypted = null;
        Exception? thrown = null;
        try
        {
            decrypted = await _service.AesDecryptAsync(encrypted, wrongKey);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Assert
        (thrown is not null || decrypted != original).Should().BeTrue();
    }

    [Fact]
    public async Task Roundtrip_AesEncryptDecrypt_ReturnsOriginal()
    {
        // Arrange
        var original = "测试文本 Test 123!@#$%";
        var key = "mysecretkey12345";

        // Act
        var encrypted = await _service.AesEncryptAsync(original, key);
        var decrypted = await _service.AesDecryptAsync(encrypted, key);

        // Assert
        decrypted.Should().Be(original);
    }

    [Fact]
    public async Task ComputeHashes_DifferentInputs_ReturnsDifferentHashes()
    {
        // Act
        var hash1 = await _service.ComputeMd5Async("input1");
        var hash2 = await _service.ComputeMd5Async("input2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task ComputeHmac_SameInputSameKey_ReturnsSameHash()
    {
        // Arrange
        var text = "test";
        var key = "secret";

        // Act
        var result1 = await _service.ComputeHmacAsync(text, key, "sha256");
        var result2 = await _service.ComputeHmacAsync(text, key, "sha256");

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task ComputeHmac_SameInputDifferentKeys_ReturnsDifferentHashes()
    {
        // Arrange
        var text = "test";

        // Act
        var result1 = await _service.ComputeHmacAsync(text, "key1", "sha256");
        var result2 = await _service.ComputeHmacAsync(text, "key2", "sha256");

        // Assert
        result1.Should().NotBe(result2);
    }
}
