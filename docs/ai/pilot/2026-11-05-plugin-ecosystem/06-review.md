# Review

## 审查八问（逐项回答）

1. 实现是否真正满足 Intent？满足安装落点、ZIP/DLL 契约、指定版本激活与回滚、宿主工具、GitHub/NuGet 解析进同一安装器、隔离 HelloWorld 骨架。公网发布按用户禁令未做。
2. 实现是否符合 Spec？安装包只认 `.zip`；DLL 用元数据读 `ForgeSelf.PluginManifest.json`；发行串比较走 `UpdateChecker.CompareSemVer`；用户目录走 `GetPluginDataDirectory`；内置扫描根未改。
3. 是否超出了 Scope？没有拆仓内插件，没有进程沙箱，没有改 `DataLocationService` 默认分支，没有改 VersionPrefix。
4. 是否修改了不应该修改的文件？没有改 `.env`。`scripts/README.md` 只改了打包脚本那一行，避免和 §1.9 相反。
5. 测试是否覆盖 Acceptance Criteria？过滤集 57 通过，覆盖数据根、旧扩展名拒绝、zip slip、打包排除 src、卸载保留 src/.forge、激活回滚、无 token 发布失败、DLL 清单不加载程序集。浏览器与隔离宿主未覆盖。
6. 是否存在明显回归风险？再次安装改为只暂存，不再顺手激活。`POST /api/plugin/update/{id}` 仍激活最高暂存版本。本地更新源不再认 `.forgeself-plugin`。
7. 是否存在架构不一致？`plugin.*` 注册在宿主 `ToolRegistry`，不是可卸载插件。内置插件仍从版本目录 `plugins/` 扫描。
8. Evidence 是否足以证明任务完成？过滤测试、前端三门禁、宿主编译、HelloWorld 编译是 Verified。隔离实例走查与脚本实跑是 Unknown。

## Requirement Check

PASS

## Scope Check

PASS

## Test Check

PASS

## Architecture Check

PASS

## Risk

L2

## Findings

### Critical

无。

### Major

无运行实例走查。PluginStore 的安装与激活按钮没有在浏览器里点过。

### Minor

`scripts/package-plugin.ps1` 未对真实插件执行一次打包。§1.8.1 表格仍保留 2026-11-04 的旧扩展名记录，现行契约在 §1.9，节首已加指向。

## Final Decision

APPROVED
