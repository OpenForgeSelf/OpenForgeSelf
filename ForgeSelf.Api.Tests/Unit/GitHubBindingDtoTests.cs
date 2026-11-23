using System;
using System.Text;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using ForgeSelf.Abstractions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>GitHub 绑定状态 DTO 投影与存取单测：令牌只落密文、投影永不含令牌。</summary>
public class GitHubBindingDtoTests
{
    private sealed class FakeEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => "v2:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        public string Decrypt(string cipherText)
        {
            if (!cipherText.StartsWith("v2:")) throw new System.Security.Cryptography.CryptographicException("bad cipher");
            return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[3..]));
        }
        public bool TryDecrypt(string cipherText, out string plainText)
        {
            try { plainText = Decrypt(cipherText); return true; }
            catch { plainText = ""; return false; }
        }
        public string Mask(string plainText) => "****";
    }

    private static GitHubBindingStore NewStore() => new(new FakeEncryption());

    [Fact]
    public void Unbound_DefaultShape_IsNotBound()
    {
        var store = NewStore();
        store.Unbind();
        var dto = store.Get();
        dto.Bound.Should().BeFalse();
        dto.AccountLogin.Should().BeEmpty();
        dto.BoundAt.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void SaveToken_NullToken_Throws()
    {
        var act = () => NewStore().SaveToken("", "octocat", "repo");
        act.Should().Throw<ArgumentException>().WithMessage("*accessToken*");
    }

    [Fact]
    public void SaveToken_NullLogin_Throws()
    {
        var act = () => NewStore().SaveToken("gho_X", "", "repo");
        act.Should().Throw<ArgumentException>().WithMessage("*accountLogin*");
    }

    [Fact]
    public void SaveToken_PersistsCipherAndAccount_NotPlaintext()
    {
        var store = NewStore();
        store.SaveToken("gho_SECRET", "octocat", "repo workflow");

        ForgeSetting.Current.GitHubTokenCipher.Should().StartWith("v2:");
        ForgeSetting.Current.GitHubTokenCipher.Should().NotContain("gho_SECRET");
        ForgeSetting.Current.GitHubAccountLogin.Should().Be("octocat");
        ForgeSetting.Current.GitHubBoundAt.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public void Get_AfterSave_ReportsBound()
    {
        var store = NewStore();
        store.SaveToken("gho_SECRET", "octocat", "repo workflow");
        var dto = store.Get();
        dto.Bound.Should().BeTrue();
        dto.AccountLogin.Should().Be("octocat");
        dto.Scopes.Should().Be("repo workflow");
    }

    [Fact]
    public void Get_Projection_NeverContainsTokenMaterial()
    {
        var store = NewStore();
        store.SaveToken("gho_SECRET", "octocat", "repo");
        var json = System.Text.Json.JsonSerializer.Serialize(store.Get());
        json.Should().NotContain("gho_SECRET");
        json.Should().NotContain("TokenCipher");
    }

    [Fact]
    public void Unbind_ClearsAllBindingFields()
    {
        var store = NewStore();
        store.SaveToken("gho_SECRET", "octocat", "repo");
        store.Unbind();

        ForgeSetting.Current.GitHubTokenCipher.Should().BeEmpty();
        ForgeSetting.Current.GitHubAccountLogin.Should().BeEmpty();
        store.Get().Bound.Should().BeFalse();
    }

    [Fact]
    public void SetAutoAttach_PersistsFlag()
    {
        var store = NewStore();
        store.SetAutoAttach(false);
        ForgeSetting.Current.GitHubAutoAttachToken.Should().BeFalse();
        store.SetAutoAttach(true);
        ForgeSetting.Current.GitHubAutoAttachToken.Should().BeTrue();
    }

    [Fact]
    public void GetTokenAsync_Unbound_ReturnsNull()
    {
        var store = NewStore();
        store.Unbind();
        store.GetTokenAsync().Should().BeNull();
    }

    [Fact]
    public void GetTokenAsync_Bound_RoundtripsPlaintext()
    {
        var store = NewStore();
        store.SaveToken("gho_SECRET", "octocat", "repo");
        store.GetTokenAsync().Should().Be("gho_SECRET");
    }

    [Fact]
    public void GetTokenAsync_CorruptCipher_ReturnsNullInsteadOfThrowing()
    {
        ForgeSetting.Current.GitHubTokenCipher = "not-a-cipher";
        NewStore().GetTokenAsync().Should().BeNull();
    }
}
