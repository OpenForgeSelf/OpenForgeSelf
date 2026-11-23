# 05 Evidence

## 命令

工作树 `wt-mock-llm`（仓库内独立 worktree），分支 `feat/mock-llm`，基线 `8f07e8f`。

`dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter FullyQualifiedName~MockLlmTests -m:1 --disable-build-servers`

临时目录改到工作树 `.temp\tmp`。第一次并行还原把 MSBuild 打到 `OutOfMemoryException`（MSB1025 / MSB4166），进程停掉后用 `-m:1` 重跑。

## 结果

测试运行成功。测试总数 5，通过数 5，总时间 2.5814 秒。`TEST_EXIT:0`。

通过的用例：

- `Match_最长关键字优先_相同长度保留先出现的`
- `Load_按文件返回规则_缺文件与坏JSON有明确错误`
- `Normalize_空端点补mock本地地址_空模型补mock`
- `Chat_命中key返回content_流式同一段正文`
- `Reload_Mock无端点也注册_且为默认提供方`

等级：Verified。

构建警告是仓库里既有的 NU1510、NU1903、MemorySystem 空性警告，不是本批新增错误。

## 前端

本批两个文件 `eslint`：0 error，30 warning。warning 在原有模板行上，含接入地址输入框第 685 行，不是新的 error。等级：Verified。

`pnpm run check`（`vue-tsc -b`）退出码 134，日志里没有 `AiProvidersPanel.vue` 或 `aiProvider.ts`。先打印的错误是 `SmartTerminal.story.ts` 引用了 `types.ts` 里不存在的 `SmartTerminalFixture`，以及 DbAdmin / SmartTerminal 插件页。这些文件不在本批 diff 里。等级：Verified（退出码与日志），本批文件无类型错误为 Inferred（全量进程中止，没有单独再跑一遍 vue-tsc）。

`pnpm run design:check` 失败在 `Plugins/SmartTerminal/web/src`：硬编码色值 0→35。`check-design-compliance.mjs` 失败在同一目录：token 族 0→11。设置页本批只用既有 `--el-*` 和 Tailwind 字号，文件内没有十六进制色。等级：Verified。

## 工件门禁

`pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-12-mock-llm -EnforceHeader -EnforceAcCoverage -EnforceInteraction` 输出 `PASS`，`PILOT_EXIT:0`。等级：Verified。

## 没跑的

- 浏览器走查：正在跑的 `:51888` 不是这个工作树的程序，没有新的「Mock（测试）」下拉。没有另起一套 dev-stack。等级：Unknown。
- 没有对真实聊天接口发 HTTP。单测直接打 `MockLlmProvider` 和 `AIProviderRegistry.ReloadAsync`。

## 范围

`git status` 只含 Mock 提供方、设置页类型、样例 `config/mock-llm.sample.json`、gitignore、四份文档和本目录。没有带入主工作区未跟踪的 `Services/AI/Proxy`。
