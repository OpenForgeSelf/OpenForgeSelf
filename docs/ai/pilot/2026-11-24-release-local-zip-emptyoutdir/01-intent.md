# 01 Intent — 发布本地 zip 命令排障

## Problem
用户执行 `pwsh scripts/release/release-local.ps1` 打本地 zip 包失败。真实失败点：插件 web 构建清空 dist 时触发沙箱 safe-delete 批量删除守卫（count 50 = threshold 50），发布链在第一个插件（AgentHub）即中断，zip 不产出。

## Why
宿主 web 已通过 `emptyOutDir: false` 规避该 shim，但 15 个插件配置未同步，形成「宿主能过、插件必挂」的不一致。不修则本机无法复现 CI 发布链、无法打本地包验证。

## Expected Outcome
`pwsh scripts/release/release-local.ps1` 本地全链跑通，产出 zip；同类遗漏一次性补齐；有类级守卫防止新插件再次写 `emptyOutDir: true`。

## Constraints
- 不修改 safe-delete shim 本体（宿主环境，仓库外）。
- 不全局关 `CODEBUDDY_SAFE_DELETE_ENABLED`（绕过安全守卫，且仅本会话有效、CI 无此变量）。
- 不改 csproj 版本行、不 git commit/push（用户未授权）。
- 代码注释中文；PowerShell 一律 pwsh。
- 同根因 15 处命中属本任务范围；异根因进 TODO。

## Success Criteria
1. 发布链实跑产出 zip（Verified，日志 + 产物路径）。
2. 15 个插件 vite.config 全部 `emptyOutDir: false`（G2 守卫断言）。
3. PC-047 + case 归档，三处索引更新，`check-problem-classes.ps1` 绿。
4. 无新增 TODO 悬空教训。
