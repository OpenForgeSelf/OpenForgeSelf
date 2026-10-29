# 05 · Evidence（验证记录）

> 只记实际发生的验证。来源等级：Verified=本会话亲跑；Unknown=未验证。

| # | 验证项 | 命令/方式 | 结果 | 等级 |
|---|--------|-----------|------|------|
| E1 | 全量编译（含 4 个插件工程 + 测试工程） | `dotnet build ForgeSelf.slnx` | **0 错误**（构建 8s） | Verified |
| E2 | 守卫 + 迁移 + 换载具测试过滤集 | `dotnet test --filter "FullyQualifiedName~ToolNamespace|PluginReload|PluginLifecycle|PluginEventBus|PluginServiceLifecycle|ProjectWorkspaceDefaultRoot|UniversalTool|ReactLoopAgent|SearchTools|TerminalCommandGuard|ToolBridgeGuardParity|RunTerminalCommand|FileTools"` | **183/183 绿**（含 S1-S5 守卫对 124 工具新格局 + 4 个换 SamplePlugin 载具测试 + lifecycle 断言 ISampleGreetingService） | Verified |
| E3 | 宿主前端类型+lint | `pnpm run check`（ForgeSelf.Web） | 0 errors | Verified |
| E4 | 宿主前端单测 | `pnpm run test`（vitest） | **1035/1035** | Verified |
| E5 | AIAgent 插件前端构建 | `pnpm run build`（Plugins/AIAgent/web） | dist/index.js 275.39 kB ✓（composerContext 名册更新编译绿） | Verified |
| E6 | mcp-center 冒烟 e2e | `npx playwright test e2e/plugins/mcp-center/mcp-center.spec.ts --workers=1` | **8 passed (2.0m)**（三件套迁出后 tools/list 与网关断言全过；calculate 透传场景含在内） | Verified |
| E7 | 行为等价核对（text_format_json vs dev_format_json） | 读码对比 ParametersJsonSchema + 服务签名 | 均支持 indentSize 默认 2、FormatJsonAsync(text, indentSize) 签名一致 → text 版退役无功能损失 | Verified |
| E8 | 宿主「文本工具」页面零改动 | features.ts 仅 signals.plugins 改 ['DevTools']；TextToolsView/router/store/4 组件未动（页面纯前端实现，不调后端 API——textToolsApi.ts 为本地 JS 计算） | 页面功能不受插件合并影响 | Verified（代码面） |
| E9 | 残留扫描 | 全仓 grep TextTools/text_stats/text_base64（cs/csproj/json/ts） | 代码面 0 残留（仅新文件注释引用历史与 .trash 归档） | Verified |

**已知未跑**：全量 `dotnet test`（testhost 存量崩溃缺陷，TODO P1 在案）；全量 e2e（本批触发条件不含，冒烟档足够）；file-tools 无独立 spec（维持现状）。
