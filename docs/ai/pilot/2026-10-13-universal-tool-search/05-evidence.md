# 05-evidence：万能工具搜索功能（search_tools）

> 分级：Verified（实跑原文）/ Inferred / Unknown。日志锚点 `.temp/tmp/`（build-aiagent-search-final.log、build-mcpcenter-search-20261013.log、test-search-20261013-r1~r6.log、test-mcpcenter-20261013.log）。

## 1. Changed Files
| 文件 | 动作 |
|---|---|
| `Plugins/AIAgent/Services/ToolFunctions/SearchToolsToolFunction.cs` | 新建（工具 + ToolSearchScorer 打分助手，internal 供 UniversalTool 复用） |
| `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs` | 修改（unknown 封套加 hint：top≤3 建议 + 先 search 后 list 引导；ToolSearchHints 内置 try/catch 不吞原错误） |
| `Plugins/AIAgent/AIAgentPlugin.cs` | 修改（ToolExtensions 注册 search_tools，一行 + 注释） |
| `Plugins/McpCenter/Services/UniversalToolForwarder.cs` | 修改（unknown 错误串追加同规则建议，自包含轻量实现） |
| `ForgeSelf.Api.Tests/Plugins/AIAgentTests/SearchToolsToolFunctionTests.cs` | 新建 15 例 |
| `ForgeSelf.Api.Tests/Plugins/UniversalToolTests.cs` | 修改（+2 例；FakeTool 加可选 description 参） |

## 2. 验证（全部 Verified，日志正文判据）
- `cd Plugins/AIAgent && dotnet build`：**0 个错误**（94 warnings 全为既有生成件类）；McpCenter 同样 **0 个错误**（1 warning）。
- 过滤集 `SearchTools|UniversalTool`：**已通过! - 失败: 0，通过: 42，总计: 42**（含新增 15 例 SearchTools + UniversalTool 12 例（10 旧例零回归 + 2 新例）+ AIAgent 目录其余 UniversalTool 关联用例）。
- McpCenter 全插件过滤集（Forwarder 改动回归面）：**已通过! - 失败: 0，通过: 108，总计: 108**。
- 测试修复轮（只动测试，实现 0 轮）：r1 CS1503 形态 ×4 → r2 反向形态 ×6 → r3 断言期望错误 ×4（readme_helper 实得 5 分为**正确行为**：名称前缀 4 + 描述含关键词 1，测试期望原按 3 分臆写）→ r4 CS1501 FakeTool 重载 → r5 残 2（Description 断言与 0 命中 tools 字段断言形态）→ r6 全绿。轮次在 2 轮上限外但**全部为测试侧修正、无实现改动、无放水删断言**（§6 迭代控制针对实现失败；此处如实记录）。

## 3. 需求逐条核验
| Success Criteria | 结论 | 分级 |
|---|---|---|
| 1 search_tools 实时快照检索、top-N 排序返回 | 用例：半径排序/精确名/同分稳定序/0 命中排除 | Verified（单测级） |
| 2 名称命中 > 描述命中、同分稳定序；空 keyword 友好错误不静默全量列举 | 用例：RanksAboveDescriptionOnly / StableOrderByName / EmptyKeyword（断言封套无 tools 字段） | Verified（单测级） |
| 3 unknown 封套带 top≤3 hint（AIAgent + McpCenter 双链路） | AIAgent：HintContainsSearchGuidanceAndSuggestions / NoSuggestionStillGuided；McpCenter 侧逻辑为同规则复刻，封套文案经 108 例回归未破（建议路由构造直测在 McpCenterRuntimeTests 既有面外）| Verified / Inferred（McpCenter 建议文案未单测直断） |
| 4 既有行为零回归 | UniversalToolTests 10 旧例全过 + McpCenterTests 108/108 | Verified |
