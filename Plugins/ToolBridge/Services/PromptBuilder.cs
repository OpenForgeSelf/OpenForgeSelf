namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 初始指令生成（PILOT-053 02-spec FR-1）：纯函数，零 IO。
/// 文本里**不得**出现本机绝对路径 / token / 端口（FR-1.3，AC1 守卫用例钉住）——
/// 因为这段文本会被粘进外部聊天站点。
/// </summary>
public static class PromptBuilder
{
    /// <summary>结果标记（AI 据此定位观察数据；与 ResultFormatter 共用同一对常量）。</summary>
    public const string ResultMarker = "[tool-bridge-result]";

    public const string ResultEndMarker = "[/tool-bridge-result]";

    public static string Build()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# 工具执行桥 · {ToolSpec.SpecVersion}");
        sb.AppendLine();
        sb.AppendLine("你是一个正在操作本机文件的助手。你**没有**内置工具通道，只能通过下面约定的文本格式发出调用；");
        sb.AppendLine("我（用户）会把你的调用原文粘贴到本机「工具桥」插件里执行，再把执行结果原样粘贴回来。");
        sb.AppendLine("请严格按格式输出，否则本机识别不到，只会把「未识别」的原因回给你。");
        sb.AppendLine();
        sb.AppendLine("## 可用工具");
        sb.AppendLine();
        foreach (var tool in ToolSpec.All)
        {
            sb.AppendLine($"### {tool.Name}");
            sb.AppendLine(tool.Description);
            sb.AppendLine("参数 JSON Schema：");
            sb.AppendLine("```json");
            sb.AppendLine(tool.ParametersSchema.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## 输出格式（任选其一，一次可发多条）");
        sb.AppendLine();
        sb.AppendLine("1) 推荐：把调用放进一个 json 代码围栏里，形如");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"tool_calls\": [");
        sb.AppendLine("    { \"id\": \"1\", \"type\": \"function\", \"function\": { \"name\": \"write_file\", \"arguments\": { \"path\": \"notes/plan.md\", \"content\": \"# 计划\" } } }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine("也接受单条对象（键名 tool / name / function / action + args / arguments / parameters / input）、");
        sb.AppendLine("顶层数组、以及 arguments 写成 JSON 字符串的形态。");
        sb.AppendLine();
        sb.AppendLine("2) 行式：每条一行，工具名后跟 key=value，值需要空格时加双引号。例如");
        sb.AppendLine("read_file path=notes/plan.md");
        sb.AppendLine("run_command command=\"git status --short\"");
        sb.AppendLine();
        sb.AppendLine("3) 箭头式单动作：exec/run/read/write/ls 冒号后跟一个值。例如");
        sb.AppendLine("> exec: dotnet --version");
        sb.AppendLine();
        sb.AppendLine("## 必须遵守的约定");
        sb.AppendLine();
        sb.AppendLine("- 所有 path 一律是**工作目录内的相对路径**，不要用绝对路径，也不要用 .. 越出工作目录（越界会被拒绝）。");
        sb.AppendLine("- content 里的换行请写成 JSON 字符串转义（\\n），行式格式不支持跨行值。");
        sb.AppendLine("- 一次可以发多条调用，我会按顺序执行并把每条的结果都回给你。");
        sb.AppendLine($"- 结果会以 {ResultMarker} 开头、{ResultEndMarker} 结尾回传；被拒绝的调用同样在里面并带原因，");
        sb.AppendLine("  请据此调整（例如换命令、补参数），不要重复粘贴同一条已被拒绝的调用。");
        // 白名单一句话必须由守卫常量派生（FR-1.2 同源口径；反向探针实测：手抄清单时守卫改小、
        // 提示词仍在对外承诺能跑 ssh，属于"说明与实现自相矛盾"）。
        sb.AppendLine($"- 命令执行受限：首 token 只能是 {string.Join(" / ", CommandGuard.DefaultAllowlist)}，");
        sb.AppendLine("  管道 | 、分号 ; 、重定向 < > 与换行一律拒绝；删除/格式化/下载/提权类命令一律拒绝。");
        sb.AppendLine($"  单条超时上限 {CommandGuard.MaxTimeoutSeconds}s，单段输出上限 {CommandGuard.MaxOutputBytes} 字节。");
        sb.AppendLine("- 不要输出本机绝对路径、密钥或端口号。");

        return sb.ToString();
    }

    /// <summary>工具清单的机器可读形状（界面「支持的工具」区与 prompt 同源，FR-1.2）。</summary>
    public static JsonArray ToolsAsJson()
    {
        var array = new JsonArray();
        foreach (var tool in ToolSpec.All)
        {
            array.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parametersSchema"] = JsonNode.Parse(tool.ParametersSchema),
                ["aliases"] = new JsonArray(tool.Aliases.Select(a => (JsonNode)a).ToArray()),
                ["required"] = new JsonArray(tool.Required.Select(r => (JsonNode)r).ToArray())
            });
        }
        return array;
    }
}
