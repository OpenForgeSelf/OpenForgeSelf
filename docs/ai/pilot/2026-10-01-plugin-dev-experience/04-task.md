# Agent Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元。
> Task ID：PILOT-plugin-dev-experience

## Objective

做完后仓库达到的可验证状态：dev 开关开启的新起宿主上，插件 C# 改动可在**同版本号**下经 `POST /api/dev/plugin/{id}/reload` 秒级生效（shadow-copy 装载，无垃圾版本号）；插件 UI 改动经源码 dist 直读 + 内容指纹即时可见（可选 opt-in 真 HMR）；插件错误在 API/UI/诊断端点可见完整原因；插件日志带 `[plugin:<id>]` 维度并按插件分文件；dev 关闭时宿主行为与改动前完全一致。

## Scope

### Allowed

- 新增：`ForgeSelf.Api/Plugins/Dev/{DevMode,PluginShadowCopy,PluginErrorStore,PluginLogScope}.cs`、`ForgeSelf.Api/Controllers/DevController.cs`、`scripts/dev-plugin.ps1`、`scripts/dev-plugin-web.ps1`、5 个测试文件。
- 修改：`PluginManager.cs`、`AppBuilder.cs`、`PluginInfoDto.cs`、`PluginController.cs`、`PluginAwareControllerActivator.cs`、`ForgeSelf.Web/src/types/plugin.ts`、`ForgeSelf.Web/src/views/PluginStore.vue`。
- 文档：6 处漂移文件（见 03-plan）+ `docs/04-standards/agent-workflow.md` 增补 dev 回路小节。
- pilot 工件目录 `docs/ai/pilot/2026-10-01-plugin-dev-experience/` 全部 8 件。

### Forbidden

- 生产环境 / DB 结构 / 鉴权权限支付核心逻辑。
- `PluginVersionService.UpdatePlugin` 严格递增语义、`PluginVersionLayout` 现有分支、`publish-plugin.ps1` 行为。
- 18 个插件源码目录的任何改动（含 `web/vite.config.ts`、`package.json`）。
- 177 处 `XTrace.Log` 静态调用的机械替换。
- 停/启/杀用户运行中的宿主进程（:51888 等）；自起验证实例除外（用后即停）。
- 新 NuGet/npm 依赖；git commit / push（未获授权）。

## Acceptance Criteria

- [ ] AC-1 `dotnet build ForgeSelf.Api -c Debug` 0 错误（基线 0）。
- [ ] AC-2 `dotnet test ForgeSelf.Api.Tests`（verbose）失败 ⊆ 基线 118 名单，无新增失败名。
- [ ] AC-3 dev off：`GET /api/dev/diagnostics` 404。
- [ ] AC-4 同版本 reload ≥3 次成功（响应 state=Running）。
- [ ] AC-5 reload 生效性：改代码→build→reload→新行为出现，版本号未变。
- [ ] AC-6 shadow 无泄漏（reload ≥3 次后每插件 1 个 hash 目录）。
- [ ] AC-7 异常可见：坏 EntryType 假插件 → `GET /api/plugin` 含 Error 消息+类型；UI 展示。
- [ ] AC-8 日志 `[plugin:<id>]` 前缀 + `log/plugins/<id>/` 分文件。
- [ ] AC-9 dev 直读源 dist + no-store 生效。
- [ ] AC-10 `pnpm run check`/`pnpm run test` 不劣于基线。
- [ ] AC-11 文档 6 处修正，无 watcher 矛盾残留。
- [ ] AC-12 2B opt-in 模块级可验证；浏览器级如实标注。

## Expected Files

见 03-plan.md「Files To Change」全表（新增 11 + 修改 13 + 测试 5）。

## Verification Commands

```bash
# 1. 构建
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug --nologo -v m

# 2. 定向单测
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~Dev|FullyQualifiedName~ShadowCopy|FullyQualifiedName~ErrorStore|FullyQualifiedName~TaggedLog" --logger "console;verbosity=detailed"

# 3. 全量测试（基线对照）
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --logger "console;verbosity=minimal"

# 4. 前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 5. dev 宿主实跑（自起自停）
FORGESELF_DEV_MODE=1 FORGESELF_PORT=<动态> FORGESELF_DATA_ROOT=<临时> FORGESELF_NO_TRAY=1 \
  dotnet ForgeSelf.Api/bin/Debug/net10.0-windows/ForgeSelf.dll --plugins-dir <repo>/Plugins
# → curl /api/dev/diagnostics、POST /api/dev/plugin/<id>/reload ×3、读日志核 [plugin:xxx]
```
