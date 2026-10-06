# scripts — OpenForgeSelf 项目脚本目录

本目录集中存放 OpenForgeSelf（铸己匣）的开发与发布脚本：插件开发/调试/发布、宿主打包发布、代码签名、自更新代理、git 门禁与内容审计等。

## 运行环境

- **PowerShell 脚本一律用 `pwsh`（PowerShell 7）执行**，禁止 `powershell`（Windows PowerShell 5.1，见 AGENTS.md §2.3）；个别示例已含完整写法。
- Node 脚本（`.cjs`）用 `node` 执行。
- 新 clone / 新环境第一步：安装 git pre-commit hook → `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1`。
- `-Plugin` 参数一律传 **PascalCase 目录名**（如 `AIAgent`），不是 kebab-case 插件 id。

## 目录结构

| 路径 | 内容 |
|---|---|
| `scripts/` | 插件开发/发布、签名、门禁、审计、更新等独立脚本 |
| `scripts/release/` | 宿主打包发布链（`release-local.ps1` 一键编排，`release-lib.ps1` 共享函数） |
| `scripts/hooks/` | git pre-commit hook 源文件（由 `install-git-hooks.ps1` 安装） |

---

## 一、插件开发与发布

| 脚本 | 功能 | 用法 |
|---|---|---|
| `dev-plugin.ps1` | 插件 C# 开发态热重载：改代码后不动版本号直接 `dotnet build` + `POST /api/dev/plugin/{id}/reload` 生效（需 `FORGESELF_DEV_MODE=1` 的 dev 宿主） | `pwsh scripts/dev-plugin.ps1 -Plugin FileTools [-Configuration Release] [-HostUrl <url>] [-Token sk-xxx] [-All]` |
| `dev-plugin-web.ps1` | 插件前端真 HMR dev server（dev-only，opt-in）：生成 `vite.config.dev.mjs` 把五个共享包 alias 到宿主 shim，启动后写 `web/.dev-server.json` 供宿主指向 | `pwsh scripts/dev-plugin-web.ps1 -Plugin AIAgent [-Port 5311] [-HostFrontend http://localhost:7002] [-Stop]` |
| `build-plugin-web.ps1` | 插件前端「出树构建」：把插件 `web/src` + vite 配置暂存到宿主树内，用宿主完整 node_modules 驱动 vite 构建（沙箱兜底路径，产物仅 `index.js` + `style.css`） | `pwsh scripts/build-plugin-web.ps1 -Plugin FileTools` |
| `publish-plugin.ps1` | 发布单插件到 `<PluginsRoot>/<id>/versions/<version>/`（side-by-side 版本化布局）；由宿主 `POST /api/plugin/update/{id}` 切换 `current` 指针与热重载 | `pwsh scripts/publish-plugin.ps1 -Plugin AIAgent [-Configuration Release] [-PluginsRoot <dir>] [-Force] [-DryRun]` |
| `publish-plugin-full.ps1` | 单插件一键发布闭环：先构建插件前端（web/dist），再调 plugin-publish-verify（全量发布 → 单插件发布 → 触发热重载 → 验证） | `pwsh scripts/publish-plugin-full.ps1 -Plugin AIAgent [-BumpVersion] [-PluginsRoot <dir>] [-Port <n>]` |
| `package-plugin.ps1` | 打包单插件为 `<id>-<ver>.forgeself-plugin` 更新源包（plugin.json + 入口 DLL + 依赖 + web/dist，排除宿主共享程序集），供设置页「插件更新源」离线更新 | `pwsh scripts/package-plugin.ps1 -Plugin AIAgent [-OutDir <dir>] [-Configuration Debug] [-Force] [-DryRun]` |
| `migrate-plugin-versions.ps1` | 一次性存量迁移：扁平布局插件 → 版本化布局（补 `versions/<ver>/` 快照 + `current` 指针），只新增文件、不删不改活动目录 | `pwsh scripts/migrate-plugin-versions.ps1 [-PluginsRoot "<repo>\publish\plugins"]` |
| `get-forge-token.cjs` | 解密 `ForgeSetting.config` 里的明文 API token（v2 = PBKDF2 + AES-256-CBC，与 AesSecretEncryptionService 同算法），走查注入用；宿主每次启动轮换 token，需指向当前运行实例 | `node scripts/get-forge-token.cjs [--config <path>]` |
| `probe-dll-string.cjs` | 二进制字符串探针：.NET 元数据字符串是 UTF-16LE，`strings`/`grep` 会漏检；用于验证新代码进了 publish 产物 / 已删实现不在产物里 | `node scripts/probe-dll-string.cjs <文件> <目标字符串> [--expect-absent]` |

## 二、宿主打包发布（`scripts/release/`）

| 脚本 | 功能 | 用法 |
|---|---|---|
| `release-local.ps1` | 一键发布编排：`build-frontend → publish-host → publish-bootstrapper → package-release → make-release-notes`；`-UpdateDir` 同时产出本地目录更新源（离线/内网更新） | `pwsh scripts/release/release-local.ps1 [-Version 2.3.0] [-SkipFrontend] [-UpdateDir <dir>] [-Sign]` |
| `build-frontend.ps1` | 构建宿主 SPA（ForgeSelf.Web → wwwroot）+ 全部插件前端（web → dist）；本机与 CI 同一入口 | `pwsh scripts/release/build-frontend.ps1 [-HostOnly] [-PluginsOnly -Plugin AIAgent]` |
| `publish-host.ps1` | dotnet publish 宿主业务层：FDD 单文件 `ForgeSelf.exe`（托管程序集内嵌、原生/内容外置）+ 外置 `System.Data.SQLite.dll`；发行串经 `FORGESELF_RELEASE_VERSION` 注入 exe 文件版本 | `pwsh scripts/release/publish-host.ps1 [-Version 2.3.0.2609161125] [-SelfContained]` |
| `publish-bootstrapper.ps1` | 产出安装根公共层：薄壳启动器 exe（~170KB）+ .NET 运行时 DOTNET_ROOT 结构（host/fxr + shared 三框架），跨版本共享、每版本只存一份 | `pwsh scripts/release/publish-bootstrapper.ps1 [-OutputDir artifacts/layout-root]` |
| `package-release.ps1` | 组装 QQNT 布局发行 zip：公共层（根）+ 业务层（`versions/<ver>/`，含内置 `plugins/`）+ `versions/current` 指针 + 更新代理脚本 | `pwsh scripts/release/package-release.ps1 -Version 0.1.0` |
| `make-release-notes.ps1` | 生成 RELEASE-NOTES：tag 注释 → 上一 tag 至本 tag 的提交标题（无 tag 时取最近 20 条）；已做 UTF-8 防乱码处理 | `pwsh scripts/release/make-release-notes.ps1 -Version v0.1.0 [-OutFile <path>]` |
| `new-version.ps1` | 版本串辅助：三段号（`2.3.0`）自动补 10 位时间码 → 完整发行串 + 可执行的 `git tag` 命令；**只打印，不做任何 git 写操作** | `pwsh scripts/release/new-version.ps1 -Version 2.3.0` |
| `publish-release.ps1` | 用 gh CLI 创建 GitHub Release 并上传 zip + SHA256SUMS + 发布说明（本地需 gh 认证，CI 用 GITHUB_TOKEN）；版本带 `-` 后缀自动判为 prerelease | `pwsh scripts/release/publish-release.ps1 -Version v0.1.0 [-Prerelease] [-Overwrite] [-DryRun]` |
| `release-lib.ps1` | release 脚本共享函数库（版本规范化/工具定位/步骤日志等），被各脚本 dot-source 引入，**勿直接运行** | — |

## 三、代码签名

| 脚本 | 功能 | 用法 |
|---|---|---|
| `sign-publish.ps1` | 给发布产物做 Authenticode 代码签名（资源管理器显示「已验证的发布者」）：默认自签证书（自动生成/复用），`-PfxPath` 可插拔商业/云证书；带 RFC3161 时间戳 | `pwsh scripts/sign-publish.ps1 [-Thumbprint <指纹>] [-PfxPath a.pfx -PfxPassword xxx] [-AllAssemblies] [-Force] [-NoTimestamp]` |

## 四、自更新与 git 门禁

| 脚本 | 功能 | 用法 |
|---|---|---|
| `update-agent.ps1` | 宿主自更新代理（由宿主 StagedUpdateService 拉起，不手动调用）：等宿主进程退出 → 新版本落 `versions/<ver>/` → 切 `current` 指针 → 重启根启动器；清理暂存与多余版本 | `pwsh scripts/update-agent.ps1 -HostPid <pid> -InstallDir <dir> -StagedDir <dir> -ExeName ForgeSelf.exe [-ArgList <args>]` |
| `install-git-hooks.ps1` | 安装 git pre-commit hook（幂等）：复制 `scripts/hooks/pre-commit` 到 `.git/hooks/`；新 clone / 新环境第一步执行，hook 源更新后重跑即同步 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1` |
| `hooks/pre-commit` | pre-commit hook 本体（sh，由 git 自动调用）：提交触碰 `docs/ai/pilot/<task>/` 时校验 00-07 八件工件，缺失拒绝提交；校验逻辑在 `verify-pilot-artifacts.ps1` | — |

## 五、质量门禁与审计

| 脚本 | 功能 | 用法 |
|---|---|---|
| `verify-pilot-artifacts.ps1` | PILOT 工件链门禁：核验 `docs/ai/pilot/<task-id>/` 下 00-07 八件工件齐全且关键节存在（防止「文件在但内容空转」）；CI 的 artifact-gate 也跑同一脚本 | `pwsh scripts/verify-pilot-artifacts.ps1 [-TaskId <task-id>] [-RepoRoot <repo>]` |
| `check-git-content.ps1` | git 内容审计：扫描「不希望提交」的内容——黑名单路径（.env/.pem/*.db/specs 等）+ 高置信敏感内容（sk-xxx/AKIA/ghp_/私钥/JWT 等）+ 低置信 key=value；`-CheckHistory` 扫全部历史提交 | `pwsh scripts/check-git-content.ps1 [-CheckHistory] [-ReportFile <path>] [-SkipLowConfidence]` |

---

> 版本号与发布/升级/备份布局的唯一真源：`docs/04-standards/packaging-upgrade-backup.md`（发行串 = `<major>.<minor>.<patch>.<yyMMddHHmm>`；目录命名统一小写）。脚本细节与踩坑记录见各脚本头部注释。
