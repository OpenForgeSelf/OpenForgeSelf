using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Data;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using ForgeSelf.Api.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ForgeSelf.Api.Tests.ImGateway;

/// <summary>
/// DbConfigStore（v2.0.0 存库加密）整链路单测：
/// - Secret 必须加密落库（密文列非空、且不等于明文——明文不得落库）；
/// - Load 解密还原字段；
/// - 无配置 / 空 Secret 边界。
/// 加密用真实宿主 <see cref="AesSecretEncryptionService"/>（配置走内存空配置 + 机器派生密钥），
/// 存储走 XCode 测试隔离连接（XCodeTestFixture 按测试类生成随机目录 = 每个类一个独立数据库，
/// 类间零冲突；类内方法各管各的数据，用 <see cref="PurgeChannel"/> 清本类涉及的单行配置表）。
/// </summary>
[Collection("XCode")]
public class DbConfigStoreTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public DbConfigStoreTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>清空本类用到的单行配置表（直查数据库，不用实体缓存）。</summary>
    private static void PurgeChannel()
    {
        foreach (var row in ImGatewayChannel.FindAll().ToList())
        {
            row.Delete();
        }
    }

    private static ISecretEncryptionService MakeEncryption()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new AesSecretEncryptionService(configuration, new MachineKeyProvider());
    }

    [Fact]
    public void Save后Load能读回字段_且Secret密文入库()
    {
        PurgeChannel();
        var store = new DbConfigStore(() => MakeEncryption());

        var cfg = new ImGatewayConfig
        {
            WeCom = new WeComConfig
            {
                BotId = "aib_db_roundtrip_bot_id",
                Secret = "db_long_connection_secret_真实密钥",
                Enabled = true,
                BoundAgentId = "agent-x",
                BoundChatModelId = "openai:gpt-4o",
            },
        };

        store.Save(cfg);

        // Load 解密还原
        var back = store.Load();
        back.WeCom.BotId.Should().Be("aib_db_roundtrip_bot_id");
        back.WeCom.Secret.Should().Be("db_long_connection_secret_真实密钥", "Load 应能解密还原 Secret");
        back.WeCom.Enabled.Should().BeTrue();
        back.WeCom.BoundAgentId.Should().Be("agent-x");
        back.WeCom.BoundChatModelId.Should().Be("openai:gpt-4o");

        // 密文落库铁证：直查实体表，密文列非空且不等于明文
        var row = ImGatewayChannel.FindAll().OrderByDescending(r => r.Id).First();
        row.Should().NotBeNull();
        row.BotId.Should().Be("aib_db_roundtrip_bot_id");
        row.SecretCipher.Should().NotBeNullOrEmpty("Secret 必须加密入库，不得为空");
        row.SecretCipher.Should().NotBe(cfg.WeCom.Secret, "库中不得存 Secret 明文");
    }

    [Fact]
    public void 无配置时Load返回空配置_且不抛异常()
    {
        PurgeChannel();
        var store = new DbConfigStore(() => MakeEncryption());

        var cfg = store.Load();

        cfg.Should().NotBeNull();
        cfg.WeCom.Should().NotBeNull();
        cfg.WeCom.BotId.Should().BeNullOrEmpty();
        cfg.WeCom.Secret.Should().BeNullOrEmpty();
        cfg.WeCom.Enabled.Should().BeFalse();
    }

    [Fact]
    public void 空Secret保存后密文列为空()
    {
        PurgeChannel();
        var store = new DbConfigStore(() => MakeEncryption());

        var cfg = new ImGatewayConfig
        {
            WeCom = new WeComConfig
            {
                BotId = "aib_db_empty_secret",
                Secret = string.Empty,
                Enabled = false,
            },
        };

        store.Save(cfg);

        var row = ImGatewayChannel.FindAll().OrderByDescending(r => r.Id).First();
        row.SecretCipher.Should().BeNullOrEmpty("空 Secret 不应产生密文");

        var back = store.Load();
        back.WeCom.Secret.Should().BeNullOrEmpty();
        back.WeCom.BotId.Should().Be("aib_db_empty_secret");
    }

    [Fact]
    public void 重复Save覆盖单行配置_不产生多行()
    {
        PurgeChannel();
        var store = new DbConfigStore(() => MakeEncryption());

        store.Save(new ImGatewayConfig { WeCom = new WeComConfig { BotId = "a", Secret = "s1", Enabled = true } });
        store.Save(new ImGatewayConfig { WeCom = new WeComConfig { BotId = "b", Secret = "s2", Enabled = false } });

        ImGatewayChannel.FindAll().Count().Should().Be(1, "单行配置表重复保存应覆盖而非追加");
        var row = ImGatewayChannel.FindAll().Single();
        row.BotId.Should().Be("b");
        row.Enabled.Should().BeFalse();
    }
}
