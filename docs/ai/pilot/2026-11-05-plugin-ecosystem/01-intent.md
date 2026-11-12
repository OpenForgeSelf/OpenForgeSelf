# Intent

> 阶段：Stage 1｜`2026-11-05-plugin-ecosystem`

## Problem

插件安装落点没有接到已经存在的数据根。发行物仍写 `.forgeself-plugin`，版本比较用 `System.Version`，第 4 段 10 位时间码比不了。更新包会立刻激活，激活失败不把指针写回去。宿主没有一组锁定在插件 `src/` 的 `plugin.*` 工具。GitHub Release 与 NuGet 进不了同一个安装器。市场页不能按指定发行串激活。

## Why

独立插件要和宿主用同一套发行串、同一种 ZIP 根，并且落在 `IDataLocationService` 已经解析好的数据根 `plugins/` 下。内置插件继续从版本目录扫描。HelloWorld 作为唯一新建仓库，放在隔离数据根，不进本仓库，也不在本任务里推送或发布。

## Expected Outcome

- 新安装的用户插件出现在 `{数据根}/plugins/{pluginId}/versions/{发行串}/`，并写根 `plugin.json` 与 `current`。
- 再次安装只暂存。`POST /api/plugin/activate/{id}` 按指定版本激活；失败时指针和活动清单回到旧版本。
- 卸载只删运行面（根 `plugin.json`、`current`、`versions/`），保留 `src/` 与 `.forge/`。
- 安装包只认 `.zip`。单文件 DLL 是第二种发行物（内嵌 `ForgeSelf.PluginManifest.json`），用元数据读取，不 `Assembly.Load`。
- `.forgeself-plugin` 不再读取、不再生成。契约写在 `packaging-upgrade-backup.md` 对应小节。
- `plugin.*` 工具注册在宿主 `ToolRegistry`。构建/测试/打包只跑该插件 `src/`。任务记入 `.forge/jobs`。`publish.*` 无授权或无密钥时失败，不打印密钥。
- 插件市场可填 GitHub 地址、NuGet 包名、上传 ZIP/DLL，并在版本列表激活指定版本。
- HelloWorld 骨架在隔离数据根 `plugins/hello-world/src`，含 GitHub Actions 工作流。不 push、不打 tag、不发 Release/NuGet。

## Constraints

- 不重写 `DataLocationService` 默认分支。
- 不拆仓内既有插件，不建进程沙箱。
- 不改 `VersionPrefix`，不改 `.env`，不 commit。
- 不停 :51888，不杀用户正在跑的宿主。
- 脚本用 pwsh。
- NuGet `PackageVersion` 用宿主已有的合法修订规避（三段号加 `.0`）；清单与目录名用完整发行串。

## Success Criteria

- 过滤测试覆盖：数据根落点、拒绝 `.forgeself-plugin`、ZIP 穿越、DLL 只读元数据、首次安装进 `versions/`、卸载后 `src/` 仍在、指定版本激活、激活失败指针回旧版、`1.0.0.2611052130` 高于 `1.0.0.2611051200`、`publish.*` 无密钥失败。
- 改前端后 `pnpm run check`、`pnpm run test`、`pnpm run design:check` 有真实日志。
- 十二步里「推送并发布」不做，原因是用户禁止 push/tag/Release/NuGet。
