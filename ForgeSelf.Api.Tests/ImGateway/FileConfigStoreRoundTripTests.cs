using System;
using System.IO;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.ImGateway;

/// <summary>
/// FileConfigStore 整链路单测：复现「保存往返」场景——
/// 用环境变量把数据目录指到临时目录（与 e2e globalSetup 注入 FORGESelf_PLUGIN_CONFIG_DIR 一致），
/// 验证 Save 落盘后 Load 能读回、且 WeCom 单通道字段（BotId/Secret）不丢。
/// v2.0.0 起配置简化为单一 WeCom 通道（回调形态已移除），本测试随之收敛。
/// </summary>
public class FileConfigStoreRoundTripTests
{
    [Fact]
    public void Save后Load能读回WeCom字段_且文件落盘()
    {
        var dir = Path.Combine(Path.GetTempPath(), "im-gateway-rt-" + Guid.NewGuid().ToString("N"));
        // 模拟隔离：FORGESelf_PLUGIN_CONFIG_DIR 指向临时目录
        Environment.SetEnvironmentVariable("FORGESelf_PLUGIN_CONFIG_DIR", dir);
        try
        {
            var store = new FileConfigStore();

            var cfg = new ImGatewayConfig
            {
                WeCom = new WeComConfig
                {
                    BotId = "aib_e2e_roundtrip_bot_id",
                    Secret = "e2e_long_connection_secret",
                    Enabled = true,
                    BoundAgentId = "agent-x",
                },
            };

            store.Save(cfg);

            // 断言 1：文件确实落盘
            var file = Path.Combine(dir, "config.json");
            File.Exists(file).Should().BeTrue("Save 应把配置写到 FORGESelf_PLUGIN_CONFIG_DIR/config.json");

            // 断言 2：Load 读回 WeCom 单通道配置
            var back = store.Load();
            back.Should().NotBeNull();
            back.WeCom.Should().NotBeNull("保存后应能读回 WeCom 配置");

            // 断言 3：字段完整还原（BotId/Secret/绑定不丢）
            back.WeCom.BotId.Should().Be("aib_e2e_roundtrip_bot_id");
            back.WeCom.Secret.Should().Be("e2e_long_connection_secret");
            back.WeCom.Enabled.Should().BeTrue();
            back.WeCom.BoundAgentId.Should().Be("agent-x");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORGESelf_PLUGIN_CONFIG_DIR", null);
            // 数据安全铁律：测试自建配置目录只创建、不自动删除
        }
    }
}
