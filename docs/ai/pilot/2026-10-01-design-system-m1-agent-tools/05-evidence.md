# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

> **状态：NOT_STARTED（交接骨架）**——实现尚未开始。除「基线」一节是规划会话的真实实测外，其余各节**全部待实现方填写**；
> 空栏 = 未做，不代表通过。填写规则见 04-task.md「交接说明」第 5 条；规划方事后按 06-review.md 独立复验，不采信本文自述。

## Task

PILOT-ds-m1-agent-tools（设计插件 v2.8.0：Agent 工具层）

## 基线（规划会话 2026-10-01 实测，Verified）

| 项                     | 命令 / 条件                                                                                                                                   | 结果                                                                                                                                                                                | 来源等级 |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| 发现用例数             | `dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests`                                          | 208                                                                                                                                                                                 | Verified |
| 未重定向 Temp          | 同过滤集直接 `dotnet test`                                                                                                                    | 总数 208 / 通过 107 / 失败 101，失败全部为 `UnauthorizedAccessException`（`Directory.CreateDirectory('C:\Users\Administrator\AppData\Local\Temp\ForgeSelfDs…')`，发生在测试主机内） | Verified |
| 重定向 Temp 后         | 先 `$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'` 再跑同过滤集（`--no-build`，`console;verbosity=normal`） | 终端原文：`测试运行成功。 测试总数: 208 通过数: 208 总时间: 4.6885 分钟`                                                                                                            | Verified |
| 版本现状               | `DesignSystemConstants` 三常量 / `plugin.json`                                                                                                | 均为 `2.7.1`（git HEAD `61b327d`）                                                                                                                                                  | Verified |
| 不属本任务的工作区改动 | `git status`                                                                                                                                  | 有 `D CLAUDE.md`、未跟踪 `docs/ai/pilot/2026-09-30-proxy-timeout-1h/`，以及并行会话（plugin-dev-experience）的在飞改动——**一律不碰**                                                | Verified |

> 实现方开工时**重新取一次基线**（对方会话可能已改变工作区），与上表对比，差异记入下方「基线复取」。

### 基线复取（实现方填）

| 项                                                                  | 命令 | 结果 | 来源等级 |
| ------------------------------------------------------------------- | ---- | ---- | -------- |
| 后端过滤集总数/通过/失败                                            |      |      |          |
| `AIAgent\|McpCenter\|Sems` 回归集（含已知存量红对表）               |      |      |          |
| 插件 web `check / test / build`                                     |      |      |          |
| 既有 `e2e/plugins/design-system`（若环境可行；成本高可只在 CP3 取） |      |      |          |

## Changed Files

<!-- 实现方列出全部改动/新增文件（以 git status 为准）；与 04-task Expected Files 逐项对账，多出的要解释 -->

-

## AC → 证据矩阵（28 行必须全填；e2e 不可行写 Unknown + 原因 + 替代证据）

| AC   | 判据（摘自 02-spec）                                                            | 验证命令 / 用例全名 | 结果 | 来源等级 | 输出摘要（贴关键原文，勿贴推断） |
| ---- | ------------------------------------------------------------------------------- | ------------------- | ---- | -------- | -------------------------------- |
| AC1  | 工具集精确 8 个；Id 唯一且前缀；PluginId；schema 合法                           |                     |      |          |                                  |
| AC2  | 真实 `ToolRegistry` 计数 8；缺必填被 `ValidateParameters` 拦                    |                     |      |          |                                  |
| AC3  | 网关往返 `design_guide`；`list_tools keyword=design` 枚举 8                     |                     |      |          |                                  |
| AC4  | `design_guide` 内容（工具索引/三条工作流/项目/预设/写开关/发现提示）            |                     |      |          |                                  |
| AC5  | `design_context` 章节/过滤/预算/`omitted`/json/hash/无令牌不抛                  |                     |      |          |                                  |
| AC6  | 同源：说明书 hex == `Load` 的 `ColorHex`；变量名 == `CssVarName`                |                     |      |          |                                  |
| AC7  | `design_lookup` token/component/export/icon                                     |                     |      |          |                                  |
| AC8  | `nearest` 六类 + 不可解析错误                                                   |                     |      |          |                                  |
| AC9  | 审查零假警报（含自产 CSS 全主题）                                               |                     |      |          |                                  |
| AC10 | 每条规则反例必响；strict；passed 语义                                           |                     |      |          |                                  |
| AC11 | 建议正确；`replace` 可直接替换；行号一致；排序稳定                              |                     |      |          |                                  |
| AC12 | 输入上限；无读盘语义；二进制/空文件跳过                                         |                     |      |          |                                  |
| AC13 | `mode=checklist`：`any` ≥8 条；`tokens[]` 全部真存在                            |                     |      |          |                                  |
| AC14 | `design_audit` 只读/`run=true`/与 `GET audit` 一致/写开关拒                     |                     |      |          |                                  |
| AC15 | 8 个预设、id 唯一、每个预设「生成→审计」无 critical；推荐                       |                     |      |          |                                  |
| AC16 | `design_create` 干跑零写库；落库后令牌/组件/审计；中文名 code；冲突；失败软归档 |                     |      |          |                                  |
| AC17 | `design_edit` set_token/成环整批拒/regenerate 预览/publish 门禁                 |                     |      |          |                                  |
| AC18 | 写开关：关闭拒写、PUT 立即生效、损坏只读、新实例保持                            |                     |      |          |                                  |
| AC19 | 异常转 error；体积保护；无绝对路径                                              |                     |      |          |                                  |
| AC20 | REST 类级鉴权；与工具关键字段一致                                               |                     |      |          |                                  |
| AC21 | `brief`/`agent-rules` 导出 + bundle + Manifest                                  |                     |      |          |                                  |
| AC22 | `Generate` 响应形状不变；存量 0 回归                                            |                     |      |          |                                  |
| AC23 | AIAgent 工具范围含 `design-system`                                              |                     |      |          |                                  |
| AC24 | 版本 2.8.0 三处一致；`meta.capabilities`/`agentTools`                           |                     |      |          |                                  |
| AC25 | `web/**`、`Model.xml`、`ForgeSelf.Api/**` 无 diff                               |                     |      |          |                                  |
| AC26 | e2e 直连网关全链路                                                              |                     |      |          |                                  |
| AC27 | 技能/README/ROADMAP/036/AGENTS §2.4                                             |                     |      |          |                                  |
| AC28 | Build 0 error（新增 0 warning）；过滤集总数==发现数；web 三件；AIAgent 回归     |                     |      |          |                                  |

## Build

Command:

```bash
```

Result: PASS / FAIL（来源等级：）

```text
```

## Unit Test

Command:

```bash
```

Result: PASS / FAIL（来源等级：）；总数 / 发现数 / 失败数：

```text
```

## Integration Test

<!-- 网关往返在 McpGatewayDesignToolsTests（进程内）；写明用例数与结果 -->

Result: PASS / FAIL / N/A（依据：）

## E2E

<!-- design-system-agent.spec.ts + 既有 e2e/plugins/design-system 回归；网关端口来源（FORGESELF_MCP_GATEWAY_PORT）也要记 -->

Result: PASS / FAIL / N/A（依据：）

## Static Analysis

<!-- 插件 web：pnpm run check；后端：dotnet build 的 warning 数（新增代码须 0） -->

Result: PASS / FAIL / N/A

## Screenshots

<!-- M1 无界面改动，N/A；e2e 日志留证路径（如 ForgeSelf.Web/screenshots/e2e/design-system/…log）写在这里 -->

## 反向探针记录（自查表 #24：新判据必须造反例证明会响）

| 探针         | 操作                                                        | 预期                                              | 实际（贴原文） |
| ------------ | ----------------------------------------------------------- | ------------------------------------------------- | -------------- |
| 硬编码色必响 | 在零假警报语料里临时插入 `color:#7c3aed`                    | 出现 `hardcoded-color`                            |                |
| 近似名建议   | `var(--ds-semantic-brnd)`                                   | `unknown-token-ref` 且建议 `--ds-semantic-brand`  |                |
| 写开关       | 关写后调 `design_edit set_token`                            | 被拒且文案含 `PUT api/design-system/agent-access` |                |
| 干跑零写库   | `design_create apply=false` 前后项目数与 `DesignToken` 行数 | 不变                                              |                |

## 预设参数调整记录（03-plan §D 微调规则）

<!-- 无调整写「无」；有调整逐条写：预设 id / 原值 → 新值 / 原因（审计 critical 摘要） -->

## 形状基线（`Generate` 响应键序列，重构前取）

<!-- 重构 GenerationService 之前，把现有 Generate 响应的 JSON 键序列贴在这里；GenerateShapeTests 与它对账 -->

## Plan 偏差汇总

<!-- 条数 + 03-plan「Plan 偏差记录」位置；无写「无」 -->

## Known Limitations

## Unresolved Issues

<!-- 失败时如实记录：FAIL + 原因 + 已尝试 1./2./3. + 最终状态 BLOCKED -->

## 阻塞

<!-- 无写「无」 -->
