# 05-evidence：McpCenter 网关配置 500/401 修复（2026-10-16）

## 变更清单（实际落地）

| 文件 | 改动 | 对应计划 |
|------|------|----------|
| `Plugins/McpCenter/McpCenterPlugin.cs:49-51` | 装配期 `ctx.Get<IToolRegistry>()` 缓存 → `McpService.WithDeferredRegistry(() => ctx.Get<IToolRegistry>())` 运行期惰性解析 | Task 3（R3） |
| `Plugins/McpCenter/McpCenterPlugin.cs:71` | `AddSingleton(new DshMcpConfigWriter(config))` → `AddSingleton(new DshMcpConfigService(config))` | Task 1（R1） |
| `Plugins/McpCenter/Services/McpService.cs` | 字段改持 `Func<IToolRegistry?>`；新增**私有** Func 构造 + `WithDeferredRegistry` 静态工厂（公开 Func 重载会使 12 处 `new McpService(null)` 测试歧义，实施时纠偏为工厂）；`SyncToolsFromToolRegistry` 每次现取 | Task 2（R3，与 mini-task 差异：工厂代替公开重载） |
| `Plugins/McpCenter/McpCenter.csproj` / `plugin.json` | 版本 2.3.0/2.3.1 → **2.3.2**（裁决比的是程序集版本，两处同步） | 发布前置 |
| `ForgeSelf.Web/e2e/mcp-tools.spec.ts` | 路由 `/mcp-tools` → `/mcp-center`、heading「MCP 工具管理」→「MCP 中心」（视图已随 034 v2.0.0 迁入插件，spec 系存量失效，4 条全红为既有失效非本次引入） | 冒烟 e2e 修复 |

## 验证记录（全部实跑，来源等级：Verified）

| # | 检查 | 命令/方式 | 结果 |
|---|------|-----------|------|
| V1 | 插件编译 | `dotnet build Plugins/McpCenter/McpCenter.csproj` | **0 错误**（1 警告既有 CS0414） |
| V2 | 后端测试 | `dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~McpCenter` | **108/108 通过** |
| V3 | 冒烟 e2e | `pnpm exec playwright test e2e/mcp-tools.spec.ts`（globalSetup 隔离实例、零 mock） | **4/4 绿**（1.2m） |
| V4 | 接口复验（修复前 500） | curl 带 token `GET /api/mcp-center/dsh?profile=desktop` | **200**，data 含真实 configPath/configExists/defaultUrl |
| V5 | 鉴权未削弱 | curl 不带 token 同端点 | **401**（回归口径通过） |
| V6 | 工具同步（原每查刷 Warn） | dev-stack 后端日志 | `[McpService] 从 ToolRegistry 同步 116 个真实工具`；仅装配期一次性 Warn（InitializeData 构造内），运行期不再刷 |
| V7 | 浏览器现场验收 | Browser 走查 :7399 `/mcp-center` | 「网关配置」页签**无红条**；dsh 区显示真实状态（尚未写入/desktop/路径）；来回切页签两次 toast 为空；控制台无 error；ForgeSelf Local 加载 120 行真实工具。截图 `screenshots/mcp-gateway-fixed.png`、`screenshots/mcp-tools-fixed.png` |

## 发布证据（本地目录更新源，主路径）

- 命令：`pwsh scripts/release/release-local.ps1 -Version 2.4.6 -Sign -UpdateDir D:\src\my-proj\OpenForgeSelf\updates`（exit 0，日志 `.forgeself/tmp-release-local-2.4.6-20261016.log`）
- 产物：`OpenForgeSelf-2.4.6.2610162203-win-x64.zip`（163,280,423 B）+ `SHA256SUMS.txt`（`594e6346…64a820`）+ RELEASE-NOTES 已落更新源
- 签名：`sign-publish` 段 **校验通过 3/3**（sectigo 时间戳命中第一候选）
- L1 不变量：包内 `versions/2.4.6.2610162203/plugins/` 含 20 个插件清单；`plugins/McpCenter/plugin.json` **Version=2.3.2**（修复 DLL 随包）
- 未做：打 tag / GitHub Release（非用户要求）；运行实例只读复验（待用户在 :51888 页面点更新后进行，见 §0 门禁⑤）

## 偏差记录

- mini-task Task 2 原定「公开 `Func` 构造重载」，实施改为「私有构造 + `WithDeferredRegistry` 静态工厂」——公开双重载在 `new McpService(null)` 调用点产生 CS0121 歧义（编译实证），工厂方案使 12 处测试零改动。
- 走查新发现 3 个插件前端视觉/指令缺陷（选中态对比度、标签竖排、v-loading 未注册）——超范围，已按 §1.3 记 TODO.md P2 不顺手修。
