using ForgeSelf.Abstractions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// <see cref="ApiKeysController"/> 错误外壳测试。
/// </summary>
/// <remarks>
/// 六个 handler 都必须把未预期故障收敛成「HTTP 500 + <c>{ success:false, error }</c>」外壳，
/// 而不是把原始异常与堆栈透出。此处用「加密必抛」的桩制造库异常路径。
/// Create / Update 会先查表，故复用 XCode 临时库并归入 <c>XCode</c> 集合。
/// </remarks>
[Collection("XCode")]
public class ApiKeysControllerTests : IClassFixture<XCodeTestFixture>
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ApiKeyService _normalService;

    public ApiKeysControllerTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        _normalService = new ApiKeyService(
            new AesSecretEncryptionService(new ConfigurationBuilder().Build(), new MachineKeyProvider()));
    }

    [Fact]
    public void Create_库异常_返回500且body为successFalse外壳()
    {
        var controller = new ApiKeysController(new ApiKeyService(new ThrowingEncryptionService()));

        var result = controller.Create(new CreateApiKeyRequest { Name = "会失败的密钥" });

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(500);

        var body = JsonSerializer.Serialize(objectResult.Value, WebJson);
        body.Should().NotContain("   at ", "不得把异常堆栈透出到响应体");

        var shell = ParseShell(body);
        shell.Success.Should().BeFalse("必须是统一响应外壳 { success:false, error }");
        shell.Error.Should().Contain("创建子密钥失败");
    }

    [Fact]
    public void Update_条目不存在_返回404且body为successFalse外壳()
    {
        var controller = new ApiKeysController(_normalService);

        var result = controller.Update(987654321, new UpdateApiKeyRequest { Name = "不存在" });

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(404);

        var shell = ParseShell(JsonSerializer.Serialize(objectResult.Value, WebJson));
        shell.Success.Should().BeFalse();
        shell.Error.Should().Contain("密钥不存在");
    }

    /// <summary>把匿名响应对象序列化后解析为外壳字段（避免直接比对 JSON 的非 ASCII 转义形态）</summary>
    private static (bool Success, string Error) ParseShell(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("success").GetBoolean(),
            root.TryGetProperty("error", out var error) ? error.GetString() ?? "" : "");
    }

    /// <summary>加密必抛的桩：模拟宿主库 / 加密子系统不可用</summary>
    private sealed class ThrowingEncryptionService : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => throw new InvalidOperationException("模拟加密子系统不可用");

        public string Decrypt(string cipherText) => throw new InvalidOperationException("模拟加密子系统不可用");

        public bool TryDecrypt(string cipherText, out string plainText)
        {
            plainText = string.Empty;
            return false;
        }

        public string Mask(string plainText) => string.Empty;
    }
}
