using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// SecretRef 引用契约测试：解析/生成/校验面（PILOT-086 FR-4）。
/// 引用只定位不携带授权；任何形态不合法一律拒绝（Fail Closed）。
/// </summary>
public class SecretRefTests
{
    [Fact]
    public void New_生成合法引用Id_且互不重复()
    {
        var a = SecretRef.New();
        var b = SecretRef.New();

        a.Id.Should().StartWith("sec_");
        a.Id.Length.Should().Be(36); // sec_ + 32 hex
        a.Id[4..].Should().MatchRegex("^[0-9a-f]{32}$");
        a.Version.Should().Be(SecretRef.CurrentVersion);
        a.Id.Should().NotBe(b.Id, "随机引用 Id 不得重复");
    }

    [Fact]
    public void ToJson_Parse_往返一致()
    {
        var r = SecretRef.New();

        var json = r.ToJson();
        json.Should().Contain("\"source\":\"vault\"");
        json.Should().Contain($"\"id\":\"{r.Id}\"");
        json.Should().Contain("\"version\":\"current\"");

        SecretRef.TryParse(json, out var parsed).Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Id.Should().Be(r.Id);
        parsed.Source.Should().Be(r.Source);
        parsed.Version.Should().Be(r.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("不是json")]
    [InlineData("{}")]
    [InlineData("{\"source\":\"env\",\"id\":\"sec_0123456789abcdef0123456789abcdef\"}")]   // 来源不对
    [InlineData("{\"source\":\"vault\",\"id\":\"0123456789abcdef0123456789abcdef\"}")]    // 缺 sec_ 前缀
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_XXX\"}")]                             // 前缀对但长度/字符不对
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_0123456789ABCDEF0123456789abcdef\"}")] // 大写 hex 拒绝（统一小写）
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_0123456789abcdef0123456789abcdef\",\"version\":\"latest\"}")] // 版本词非法
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_0123456789abcdef0123456789abcdef\",\"version\":\"0\"}")]      // 版本 0 非法
    public void TryParse_非法形态一律拒绝(string? json)
    {
        SecretRef.TryParse(json, out var parsed).Should().BeFalse();
        parsed.Should().BeNull();
        SecretRef.ParseOrNull(json).Should().BeNull();
    }

    [Theory]
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_0123456789abcdef0123456789abcdef\",\"version\":\"3\"}")] // 固定轮值版本
    [InlineData("{\"source\":\"vault\",\"id\":\"sec_0123456789abcdef0123456789abcdef\",\"version\":\"\"}")]  // 空 → current
    public void TryParse_合法变体接受(string json)
    {
        SecretRef.TryParse(json, out var parsed).Should().BeTrue();
        parsed.Should().NotBeNull();
    }
}
