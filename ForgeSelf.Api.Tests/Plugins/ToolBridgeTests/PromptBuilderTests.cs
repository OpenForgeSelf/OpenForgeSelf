using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// AC1 / AC2：初始指令的**内容判据**。
/// 这里刻意不写「提示词长什么样」的金值（措辞会改），只钉三件会坏的事：
/// ① 四个工具与各自参数名必须逐字在内（AI 靠它拼调用）；
/// ② 不得外泄本机绝对路径 / token / 端口（这段文本会被粘进外部聊天站点，FR-1.3）；
/// ③ 提示词、界面清单、解析别名三处必须同源于 <see cref="ToolSpec"/>（AC2 反向探针改 ToolSpec 即红）。
/// </summary>
public class PromptBuilderTests
{
    private static readonly string[] ExpectedTools = ["read_file", "write_file", "list_dir", "run_command"];

    [Theory]
    [InlineData("read_file")]
    [InlineData("write_file")]
    [InlineData("list_dir")]
    [InlineData("run_command")]
    public void 提示词含每个工具名与其参数名(string tool)
    {
        var text = PromptBuilder.Build();
        var entry = ToolSpec.Find(tool);

        entry.Should().NotBeNull();
        text.Should().Contain($"### {tool}");
        foreach (var required in entry!.Required)
        {
            text.Should().Contain(required, $"{tool} 的必填参数 {required} 必须出现在提示词里，AI 才知道要填");
        }
    }

    [Fact]
    public void 提示词含协议世代标记()
    {
        var text = PromptBuilder.Build();
        text.Should().Contain(ToolSpec.SpecVersion);
        text.Should().Contain(PromptBuilder.ResultMarker);
        text.Should().Contain(PromptBuilder.ResultEndMarker);
    }

    [Fact]
    public void 提示词不含本机绝对路径_令牌与端口()
    {
        var text = PromptBuilder.Build();

        Regex.Matches(text, "[A-Za-z]:[\\\\/]").Should().BeEmpty("出现盘符式路径即等于把本机路径交给外部站点");
        text.Should().NotContain("forge_api_token");
        Regex.IsMatch(text, @"localhost[:：]?\d*").Should().BeFalse("不得带宿主地址或端口");
        text.Should().NotContain("7102");
        text.Should().NotContain("51888");
    }

    [Fact]
    public void 提示词声明命令白名单与拒绝口径()
    {
        var text = PromptBuilder.Build();
        var joined = string.Join(" / ", CommandGuard.DefaultAllowlist);

        // 白名单句必须由守卫常量派生（FR-1.2 同源）。反向探针实测过：手抄的第二份清单会漂——
        // 把守卫的 ssh 删掉，硬写的提示词照样对外承诺能跑 ssh，说明与实现自相矛盾。
        text.Should().Contain($"首 token 只能是 {joined}");
        foreach (var exe in CommandGuard.DefaultAllowlist)
        {
            text.Should().Contain(exe);
        }
        text.Should().NotContain("dotnet / pnpm / node / git / pwsh", "少列一项即等于提示词与守卫不同判");
        text.Should().Contain("管道");
    }

    [Fact]
    public void 界面清单与提示词同源_工具数量名字逐一对应()
    {
        var text = PromptBuilder.Build();
        var tools = PromptBuilder.ToolsAsJson();

        tools.Should().HaveCount(ToolSpec.All.Count);
        foreach (var node in tools.Cast<JsonObject>())
        {
            var name = node["name"]!.GetValue<string>();
            name.Should().BeOneOf(ExpectedTools);
            text.Should().Contain(name);

            // schema 必须可解析（AI 侧要拿它拼参数）
            var schema = node["parametersSchema"]!;
            schema.GetValueKind().Should().Be(JsonValueKind.Object);
            schema["type"]!.GetValue<string>().Should().Be("object");
        }
    }

    [Fact]
    public void 别名表覆盖每个工具的规范名自身()
    {
        // Normalize 依赖别名表；若某工具的规范名不在自己别名里，AI 按提示词写的名字会认不出。
        foreach (var entry in ToolSpec.All)
        {
            ToolSpec.Normalize(entry.Name).Should().Be(entry.Name);
            entry.Aliases.Should().Contain(entry.Name);
        }
    }
}
