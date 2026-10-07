---
name: plugin-publish-verify
description: 宿主/插件「发布 + 验证」闭环（2026-09-27 起主路径 = 打 tag 自动发布 + 页面自动更新）。用于「发插件」「发布新版本」「验证更新生效」「只发插件不重启宿主」「跑发布流程」。发布动作交给 tag→CI→GitHub Release（或本地 release-local.ps1 打包 + 页面自动更新）；**禁止 agent 停/启/杀用户运行中的宿主进程**；开发期验证走 e2e 隔离实例；确需在运行实例侧载插件（版本化更新，不重启宿主）须先获用户同意。
---

# 发布与验证（2026-09-27 新规范：打 tag 自动发布 + 页面自动更新）

## 何时用

- 完成了一个宿主/插件功能，要把新版本交付出去
- 要验证「打 tag 后 CI 打包的 Release 资产」能被页面自动更新链路正确检查/下载
- 要确认发布产物（zip / 版本号 / SHA256SUMS）正确

> 🔁 **本技能只管"发布之后"**。用户表露"先看看效果 / 别发布"时走的是 `plugin-development` §四 的
> **👀 本地预览**（不发布、不碰运行实例）与 **🚶 走查**（门禁绿之后、发布之前先在 dev 预览实例用真浏览器走一遍；
> 双绿不能替代它）。本技能的「运行实例只读复验」是那条链的**最后**一步，不能被预览或 dev 预走查顶替。

## 发布路径：主路径 vs 可选

| | 方式 | 地位 |
|---|------|------|
| **主路径（推荐）** | **打 tag 自动发布**：`git tag -a v<X.Y.Z> -m "..."` → `git push github v<X.Y.Z>` → CI（`.github/workflows/release.yml`）自动构建打包并创建 GitHub Release → 用户/页面在「设置-版本更新」点「检查更新 → 下载 → 重启并更新」完成升级 | 一切交付的默认路径；宿主由 update-agent 自更新，**无人停宿主** |
| **本地离线发布** | `pwsh scripts/release/release-local.ps1 -Version v<X.Y.Z> -UpdateDir <目录>` → 设置页「更新源 = 本地目录」填该目录 → 页面点「检查更新 → 下载 → 重启并更新」 | 内网/离线/不想推 GitHub 时；等价于主路径的本地版 |
| **开发期侧载（可选）** | `scripts/publish-plugin.ps1 -Plugin <X> -PluginsRoot <运行实例>/plugins -Force`（stage 快照）→ `POST /api/plugin/update/{id}` 版本化切换，宿主不重启。**`run-plugin-publish-verify.ps1` 是旧布局产物**（2026-09-29 实测：plugin.json 路径仍指 `ForgeSelf.Api/Plugins/`，且其第 2 步会 `Stop-Process` 杀非 publish 实例 = 违反铁律 1），只可当验证清单参考，不可直接跑 | 仅插件自身 DLL 变更且**用户同意触碰运行实例**时；宿主二进制（ForgeSelf.Api/Web）变更一律走 tag 发布，不走此路径 |

## 铁律（先记住这 4 条）

1. **禁止 agent 停/启/杀任何用户运行中的宿主进程**（含 `D:\src\tools\ForgeSelf`、`:51888` publish 实例、托盘实例等）。
   发布 = 打 tag 自动发布 + 页面自动更新（update-agent 换文件并重启宿主，属宿主自更新）。
   历史教训：旧流程「确保宿主从 publish/ 运行，非 publish 实例杀掉重起」曾误杀用户运行实例，被用户明确叫停。
2. **活动插件目录只放「插件自己的程序集」**：`<Dir>.dll` + `plugin.json` [+ `web/dist`]（版本化后根扁平为回退，实际生效在 `versions/<current>/`）。目录结构与备份生命周期真源 = `docs/04-standards/packaging-upgrade-backup.md`（目标去 `_backups`/插件备份，§4-R4）。
   绝不能把 `XCode.dll` / `NewLife.Core.dll` / `ForgeSelf.*.dll` 等**宿主共享 DLL** 拷进去——宿主启动即崩（实测踩坑）。
3. **验证分两段，都不许省**：
   - **开发期 = e2e 隔离实例**：`playwright.config.ts` globalSetup 自动构建宿主 → 起隔离实例（`FORGESELF_INSTANCE_ID`）→ 解密真实 token → 跑用例 → 回收。**不碰用户运行实例**。
   - **交付后 = 运行实例只读复验**：用户启用/更新到新版本之后，agent **必须**在运行实例（如 `:51888`）做一次**只读**走查（见下「运行实例只读复验」）。铁律1 禁的是停/启/杀与不可逆写，**不含只读走查**——别把它读成"别去看"（2026-09-28 我就因此漏走查，被用户指出）。
4. **发布产物核对**：打 tag 后等 CI 完成，用资产 API 直链下载 zip 核对 SHA256（`package-release.ps1` 写 `SHA256SUMS.txt`）；页面自动更新链路已由 spec 036 单测 + e2e 覆盖。

## 一键跑（本地打包，可选）

```powershell
pwsh scripts/release/release-local.ps1 -Version v0.2.5 -Sign -UpdateDir D:\updates
```

常用参数：`-Version v<X.Y.Z>`（打 tag 时的版本，缺省 `0.0.0-local`）、`-UpdateDir <目录>`（拷贝 zip+SHA256SUMS+更新说明到本地更新目录）、`-SkipFrontend`（复用已有 web dist 快速迭代）、`-FrameworkDependent`、`-Sign`。**⚠️ 执行发布脚本一律用 `pwsh`，禁止 `powershell`（5.1）**（2026-10-04 输入12 立，AGENTS §2.3 / agent-workflow §B6）：5.1 按 GBK 码页写重定向日志与解码 `git` 输出，中文会乱码并可能被烤进 RELEASE-NOTES 这类交付物（实测：更新说明乱码 = 输入11；`release-local -Sign` 的签名段日志整段 GBK）。**签名策略（2026-10-04 输入11 定稿）：本地发布必带 `-Sign`，流水线默认不签**——**只要这版宿主是给人装的**（出到本地更新源 `-UpdateDir`、或交给用户点「检查更新 → 下载 → 重启并更新」）**就必须显式加 `-Sign`**，并且**出完要自己验签名有效再交**：`Get-AuthenticodeSignature <exe>` 必须回 `Status=Valid`（自签证书本机未受信时会显示 NotTrusted，此时按真源 §1.1 的 certutil 静默信任步骤处理，别把"签了但无效"的包交付）；而 **CI 流水线默认不传 `-Sign`**（自签证书生成会卡死 runner，实测 run 36664225915 两次 20min+，故 2026-09-30 输入2 定为不签）。一句话：**本地=签名交付，CI=不签**，两边都用同一条 `release-local.ps1` 通道，差别只在有没有传 `-Sign`。Authenticode 签名走 `scripts/sign-publish.ps1`（自签证书自动生成/复用 + DigiCert 时间戳；商业证书 `-PfxPath/-PfxPassword` 可插拔），签名在 zip 打包前（`package-release.ps1` 的 `if ($Sign)` 段），递归签顶层根启动器 + `versions/<ver>/` 全部 exe——漏签业务层会破坏多版本回滚的签名一致性。**CI 流水线默认不传 `-Sign`**（release.yml），签名策略真源 = `docs/04-standards/packaging-upgrade-backup.md` §1.1。

## 流程骨架（主路径）

| 步 | 动作 | 关键命令 / 路径 |
|----|------|-----------------|
| 1 | 门禁 | 前端 `pnpm run check && pnpm run test`；后端 `dotnet build && dotnet test`；插件层 e2e 绿 |
| 2 | 汇总变更，向用户请示 git 提交（用户审批后才 commit） | 见 AGENTS.md §10 / 用户偏好（同一任务一次性提交） |
| 3 | 打 tag 自动发布 | `git tag -a v<X.Y.Z> -m "发版说明"` → `git push github v<X.Y.Z>` → CI 自动打包 → GitHub Release（OpenForgeSelf/OpenForgeSelf，公开仓库） |
| 4 | 通知用户页面自动更新 | 用户（或用户明确同意后代点）在设置-版本更新页「检查更新 → 下载 → 重启并更新」；宿主 update-agent 换文件并重启，全程无人停宿主 |
| 5 | 验证发布产物 | `gh release view v<X.Y.Z>` + 下载 zip 核对 SHA256SUMS；更新检查/下载链路由单测 + e2e 覆盖 |

> 第 3 步的 tag 是**唯一发布动作**。本地迭代若不想推 GitHub，用本地离线发布（`-UpdateDir` + 页面本地目录更新源）。

细节见：
- 发布治理硬规则与坑：[`docs/04-standards/agent-workflow.md`](../../../docs/04-standards/agent-workflow.md) B10
- 版本化布局说明：[`docs/02-features/035-plugin-versioned-layout.md`](../../../docs/02-features/035-plugin-versioned-layout.md)
- 自动更新链路（spec 036）：`ForgeSelf.Api/Services/StagedUpdateService.cs`、`UpdateChecker.cs`、`UpdateController.cs`

## 走查与产物验证标准步骤（2026-09-24 起内置，禁止每次现写探针）

**背景**：宿主每次重启轮换 `ApiToken` + `init-token` 一次性 → 浏览器旧 token 立即失效；
DLL 字符串是 UTF-16LE（strings/grep 漏检）。这两件事历史上反复手工（现写解密探针 +
手工注入 + 现写字节探针），2026-09-24 已固化为仓内工具与 e2e 步骤，**不要再发明**：

| 场景 | 正规入口 | 说明 |
|------|---------|------|
| 插件页走查（推荐） | `node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugin-store.spec.ts --output=<空目录>` | globalSetup 自动构建宿主→起隔离实例→解密真实 token→注入 localStorage；断言版本徽标/启用标签/卡片渲染 + 截图存档（`screenshots/e2e/plugin-store/plugin-store-walkthrough.png`）。**必须给 `--output=<空目录>`**（防 safe-delete 拦截器） |
| 运行态宿主（51888）手工通道 | `node scripts/get-forge-token.cjs` → 拿明文 token → 注入 `localStorage['forge_api_token']` → 导航 | 默认读 `%USERPROFILE%\.forgeself\Config\ForgeSetting.config`（当前运行实例真源）；`--config <path>` / `FORGE_SETTING_CONFIG` 可覆盖。⚠ 触碰运行态宿主前必须获用户同意；新规范优先用 e2e 隔离实例走查 |
| DLL 字符串验证（发布产物） | `node scripts/probe-dll-string.cjs <dll> <目标串> [--expect-absent]` | UTF-8 + UTF-16LE 双检；`--expect-absent` 用于验证「已删除实现不在产物」；FOUND→exit 0 / ABSENT→exit 1（expect-absent 反转） |
| e2e 全自动 | 直接写/跑 `e2e/plugins/<id>/<id>.spec.ts` | `e2e/helpers/real-auth.ts` 的 `injectRealApiKey` 已内置解密+注入 |

**铁律**：走查 token 一律来自上述入口；验证产物一律用 `probe-dll-string.cjs`。
发现新场景缺正规入口 → 先补工具/用例再走，不现写一次性脚本（AGENTS §5.0）。

## 出包后必查的三条布局不变量（宿主升级链，2026-10-04 输入18/19 立）

**为什么单独列**：这两条缺陷都是「包能出、签名有效、校验和 MATCH、页面能起，但升级后插件整批消失 / 版本目录逐代嵌套」——
四层门禁里只有「按包内容验真」才抓得到，靠 exit code 和「看起来装上了」都抓不到。

| # | 不变量 | 机器判据（不靠肉眼） | 违反时的现场形态 |
|---|--------|----------------------|------------------|
| L1 | 内置插件随版本走：包内必须有 `versions/<ver>/plugins/<Id>/plugin.json` | 列包内条目数（`Expand-Archive` 到临时目录后计数），或跑常驻守卫 `RepositoryScriptTests.PackageRelease_MustKeepBundledPluginsInsideVersionDirectory`；`package-release.ps1` 自身在缺 `versions/<ver>/plugins` 时**当场 throw** | 升级后 `GET /api/plugin` 返回 `data:[]`，宿主日志 `插件目录不存在: …\versions\<ver>\plugins` |
| L2 | 安装根 = 公共层所在层：更新链路交给代理的 `-InstallDir` 必须是安装根，不是业务层版本目录 | 常驻判据 `HostInstallRootTests`（含**实跑 `pwsh` 比对宿主侧与代理侧归一化结果**，以及真跑代理的沙箱用例：断言新版本落真根、`current` 切过去、`versions/<ver>/versions` 不存在） | 嵌套形态：versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0\ForgeSelf.exe 这种逐代嵌套；代理日志里 `InstallDir=` 指向版本目录 |
| L3 | 两路插件根（内置 + 数据根 `~/.forgeself/plugins/`），安装根 `plugins/` **不是**扫描点 | `PluginRootsTests`（两路各一插件都发现／同 Id 按版本裁决／只有 `*.db` 的数据子目录不当插件／一路缺失另一路仍生效） | 把插件手工放到安装根 `plugins/` 却"装不上"，或反过来把内置插件外置到安装根导致版本目录被掏空 |

**规则**：① 判定看日志正文（`通过数/失败数`、代理 `agent-*.log` 的落点行），不看 exit code；
② 用户已装的**嵌套实例升级一次即回正**（新版本落真根 + `current` 指过去 + 重启根启动器），但**存量多余层与历史 `plugins/` 残留不会被自动清理**——那是不可逆面，必须用户在场另批处理，不得在发布流程里顺手删；
③ 版本串规则不变（§4-R10：三段号 + 10 位时间码；老宿主需先跳号才重新进入更新链）。

## 发布链的两种「跑到没跑完」形态（2026-10-06 输入9/10 实证）

**A. 签名段是整条链的最后一道闸，它一挂就没有包**（不是"包有瑕疵"）：
`release-local.ps1` 的次序是 前端 → publish-host → publish-bootstrapper → **package-release（组布局 → 签名 → zip → SHA256SUMS）** → make-release-notes → 拷 `-UpdateDir`。
签名在 **zip 之前**，所以时间戳失败时**前面 8 分钟的构建全部作废**，更新源里仍是上一轮的旧 zip——
现场判据：日志出现 `Number of files successfully Signed: 0` + `SignTool Error: The specified timestamp server either could not be reached or returned an invalid response`，
且 `artifacts/release/` 里**没有**本轮版本串的 zip。
⇒ 汇报口径只能写「未出包」，不得写「本地发布已通过」。

**B. 单点时间戳已改为多点回退**（`scripts/sign-publish.ps1`）：候选链 `sectigo → digicert → globalsign → comodoca`，
一家不行自动换下一家（实测：故意把第一家设成不可达域名，日志打 `时间戳不可用，换下一个:` + `（回退命中）时间戳:`，最终 `校验通过 1 / 1`、exit 0）。
`-TimestampServer` 只把某家排到最前，`-TimestampFallbacks` 自定义顺序。
**禁止用 `-NoTimestamp` 交付给人装的包**（无时间戳＝证书 2029-09-26 到期即签名失效）。
另：校验口径看 `Status=Valid` + 有时间戳，**不要断言特定 TSA 名**（回退后哪家都可能）。

**C. 停一条后台发布链 ≠ 它停了**：`TaskStop` 杀的是 Git-Bash 包装进程，**pwsh 子进程会继续跑并往同一批 `artifacts/` 写**；
随后再起一轮就是**两条链并发同一目录**（互踩 DLL/zip/layout，报错形态看起来像代码缺陷）。
⇒ 停链后必须实证复核，别凭通知判定：
`Get-CimInstance Win32_Process | ? { $_.CommandLine -match 'release-local' }` 看还有没有活口 + 看旧日志 mtime 是否已冻结。
顺带一条同源教训：**pwsh 重定向到文件是块缓冲**，日志"停在几十行"不代表进程停了（本次就是这样误判过一次），
判进度要同时看 `size/mtime` 是否在长与 `==> <段名>` 标记。

## 只发插件（side-by-side 落位）：生效路径与"别手改顶层清单"（2026-10-07 实证）

**先把"生效"讲死（读代码定案，别再靠目录名猜）**：

- **加载与目录名无关**——`PluginManager.cs:316-328` 只遍历根的直接子目录，唯一硬条件＝**该子目录顶层有 `plugin.json`**，
  身份取自清单 `Id`。⇒ 生效问题从来不是 PascalCase/kebab 之争，而是「顶层有没有清单」。
- **宿主代码约定＝kebab `metadata.Id`**：`PluginInstallerService.cs:60` 建目录用 `Path.Combine(_pluginsDirectory, metadata.Id)`
  且首次安装**直接扁平解包到该目录**（顶层就有清单）；`PluginVersionService.cs:126` 同样按 kebab 拼。
- **内置根里为什么是 PascalCase**：`ForgeSelf.Api.csproj` 的 `ProjectReference`/`Content Include="Plugins\**\plugin.json"` glob
  按**工程名**产出；`package-release.ps1:71-78` 只把**外层** `Plugins → plugins` 归一，**插件子目录名没人管** ⇒ 历史遗留，非规范。
- ⇒ **两个已知缺陷**（遇到"版本历史空/回滚按钮没出现"先想它们，别怀疑自己放错文件）：
  ① `PluginVersionService.cs:126` 硬拼 `_pluginsDirectory + pluginId`，不用 `metadata.PluginDirectory`、不跟随两路根
  ⇒ PascalCase 内置目录与数据根插件都查不到版本目录（只剩 `:146` 用内存元数据补一条）；同文件的更新/激活却用
  `metadata.PluginDirectory` ⇒ 一个服务两套口径，表现就是「检查更新看得见、版本历史看不见」。
  ② `AppBuilder.cs:335-336` + `PluginVersionService.Initialize(pluginsPath)` 只喂**内置根** ⇒ 数据根那一路无版本化能力。

**放哪里才生效**：放进**该插件实际所在的那个目录**（= `metadata.PluginDirectory`；内置根里就是 PascalCase `ToolBridge\`），
形态 `<pluginDir>\versions\<新版本>\`，内含 `plugin.json` + `<Entry>.dll` + `<Entry>.deps.json` + `web/dist/*`。
插件根 = **业务层 exe 旁边的 `plugins/`**（`AppBuilder.cs:326` 用 `AppContext.BaseDirectory`；`Get-Process ForgeSelf` 实测路径才算数，
别拿安装根那个 09-29 的旧 `plugins/` 当现场）。数据根 `~/.forgeself/plugins/<id>/` 是第二路，跨版本持久，两份可并存
（同 Id 同版本按"内置根先扫、保留先者"裁决 `PluginManager.cs:336-346`）。`deps.json` 必须与入口 DLL 同目录——
`PluginLoadContext.cs:29` 的 `AssemblyDependencyResolver` 吃**入口程序集旁边那份**。

**⚠️ 不要拿 `publish-plugin.ps1 -PluginsRoot <内置根>` 直跑**：它按 kebab 新建 `<root>/tool-bridge/` 且**只写 `versions/<ver>/`、不写顶层清单**
⇒ 那个目录冷启动被跳过（缺顶层 plugin.json），而更新判定看的是 `metadata.PluginDirectory`（PascalCase 的 `ToolBridge`）⇒ **两边都不靠，成为孤岛**。
它在"数据根 + 宿主 update 流程接管"的场景才是对的（那是宿主自己的命名口径）。内置根场景请手工放进已存在的 `ToolBridge\versions\<ver>\`。

**光放文件不生效**：`publish-plugin.ps1` 头部原文＝文件系统监视器 2026-09-24 已移除，staged 副本只在
「显式切换」或「冷启动」后生效。不重启的三条口子：

| 口子 | 判据/条件 | 现场形态 |
| --- | --- | --- |
| `POST /api/plugin/update/{id}` | 需要 `versions/` 里最高 staged 版本 **>** 内存 `metadata.Version`（`PluginVersionService.cs:167-173`） | 若顶层清单已被手改成新版本，这里直接判"已经是最新版本"，**切换不发生** |
| `POST /api/plugin/rollback/{id}` `{"version":"X"}` | 只校验 `versions/X/` 存在（`:200-205`），**不比版本号** ⇒ 最稳的强制激活 | 页面等价：插件市场「版本」→ 版本历史 → 该行点「回滚」 |
| `disable` → `enable` | 启用时重走 `ResolveEntryAssemblyPath`，`versions/<current>/` 优先 | 页面：插件卡片上的「禁用」→「启用」；current 已指向新版本时可用 |

**⚠️ 教训：不要手工覆盖顶层活动清单 `plugin.json`。** 同步顶层清单是宿主 `ActivateVersion` 第 5 步 `SyncActiveManifest`
该做的事（`PluginVersionService.cs:248-249`）。我 2026-10-06 手改它之后，实测连带关掉两条不重启的口子：
`GET /api/plugin/updates` 返回空（`update` 判"已最新"），且 `PluginStore.vue:491-495` 的版本历史按钮因
`v.version === activePlugin?.version` 而**置灰**，用户在页面上无路可点。
⇒ 正确顺序：只写 `versions/<ver>/`（+ `current`），然后走 `rollback`/`enable` 让宿主自己同步清单；
若已经手改过，就靠 `rollback`（不比版本号）或 `disable→enable` 收口。

## 运行实例只读复验（交付后必做，2026-09-28 补）

**触发**：用户已在运行实例（如 `:51888`）启用/更新到新版本之后 —— 这一步**不属于**开发期 e2e 的替代，
而是「发布/侧载 → 生效」之间的**闭环确认**。历史漏点：AGENTS §0 门禁第④步把「走查」写在 e2e 隔离实例上，
我据此认为跑完 e2e 就算走完，用户启用 1.1.1 后我没有去运行实例复验（被用户当场指出）。

**步骤（全部只读）**：

0. **先验 token 有效性，再谈结论**（不可跳过）：注入前用 `node scripts/get-forge-token.cjs` 取当前配置解出的明文，
   在页面里打一个**需要鉴权的只读端点**（如 `GET /api/plugin/detail/<id>`）确认 **200**；
   出现 **401** 就**立即停下上报**，绝不把「页面空白 / 主区没渲染」当成插件缺陷。
   实测坑：宿主每次重启轮换 `ApiToken`，浏览器里存的旧 token 会静默 401，页面只剩导航条（`body` 只有几十字节），
   极易误判成「新版本坏了」。
1. `localStorage.setItem('forge_api_token', <第0步验过的明文>)` → 重新导航到 `plugin.json.frontend.route`（如 `/file-tools`）。
2. **核版本徽标**：`.view-title` 旁应显示 `v<新版本号>`（铁律13 的产物，正好当"跑的是哪一版"的自证）。
3. **走一条主链路**：填真实输入 → 点主操作 → 采**中间态**一帧（`Running`/进度）→ 等终态。
   中间态必须看数值自洽（占比 ≤100、合计闭合、无守恒告警）—— 1.1.1 修的就是只在中间态暴露的分母缺陷。
4. **截图存档并读图**：存 `ForgeSelf.Web/screenshots/live-<端口>/<插件id>-<版本>-<场景>.png`，按 `e2e-testing` Level 3 清单逐项核对。
5. 需要独立基准时，用 `find <目录> -type f -printf '%s\n' | awk '{s+=$1} END{print s}'` 之类**只读**测量对账，别信界面自说自话。

**边界（越界即违规）**：不点不可逆 / 无 UI 回退的动作（删除、清空、覆盖配置）—— 需要验证写路径就在**隔离实例**做；
不启停宿主；不调 `POST /api/plugin/update/{id}` 等改运行实例状态的端点，除非用户明确要求；
截图/日志之外的任何文件都不写进用户安装目录。

## 前端改动后的发布必做（2026-09-27 新规范）

插件管理/宿主前端（`ForgeSelf.Web/src`）改动后，**dev/e2e 通过 ≠ 发布态生效**：
必须走发布主路径——打 tag → CI 自动 `build-frontend.ps1` 重建 wwwroot + 插件 dist → 打包进 zip → 页面自动更新。
本地验证走 e2e 隔离实例（globalSetup 自动构建宿主，不碰用户运行实例）。
PluginController 鉴权后，前端必须走 `authFetch`（src/services/authFetch.ts）带 token，
否则插件管理页/远程视图 401 空态（曾误判为环境问题）。
## 关键事实速查

- 端点前缀是**单数** `api/plugin/...`（`[Route("api/[controller]")]`，控制器 `PluginController`）。
  `publish-plugin.ps1` 结尾打印的 `/api/plugins/...` 是**错的**，会 404。
- 插件目录：`AppContext.BaseDirectory/plugins` → publish 实例即 `publish/plugins`。
- 两层命名：目录/程序集 PascalCase（`AIAgent`），运行时 id kebab-case（`ai-agent`）。
  `-Plugin` 传**目录名**，`_backups` 下用 **id**。
- 默认端口 7102（`ForgeSetting.config` 的 `PortNumber`），本环境长期 publish 实例用 **51888**。
- **前端资源版本化读取（2026-09-24 已修复）**：`PluginFrontendFileMiddleware` 从
  `versions/<current>/web/dist/` 读取静态资源（current 指针存在且版本 web 存在时），
  `ComputeWebVersion` 指纹同样基于版本快照 → 版本切换后 `?v=` 变化、浏览器拉新版。
  核验：`curl http://localhost:<port>/plugins/<id>/web/dist/index.js`，比对**字节数**与版本快照一致（可用探针标记区分根扁平）。
- **跑 e2e 必须给 `--output`**：`node ForgeSelf.Web/node_modules/@playwright/test/cli.js test --config=playwright.config.ts <spec> --output=<空目录>`；
  否则 Playwright 启动前清理 `test-results` 会被沙箱 safe-delete 拦截器拦下，整跑失败。
- **存量迁移脚本**：scripts/migrate-plugin-versions.ps1（幂等）把扁平插件目录迁移为版本化布局（`versions/<ver>/` + current）；build 覆盖 publish/plugins 后可重复跑。
- **宿主二进制变更发布顺序铁律（2026-09-27 新规范）**：改宿主后端/前端后**不要手动停宿主去覆盖二进制**——
  一律走打 tag 自动发布（或本地 `release-local.ps1 -UpdateDir` + 页面本地目录更新源），宿主由 update-agent 自更新。
  旧教训（2026-09-24）：`build.ps1` 覆盖 publish/ 时运行中宿主锁住 `ForgeSelf.dll` 被静默跳过、脚本仍报成功；
  这条在 tag 发布路径下由 CI 全量重建天然规避。
- **PowerShell 相关（2026-10-05 实测更正，旧文已作废）**：本机 `pwsh` 可用且为 **7.6.6**；发布链**必须**用 `pwsh`（`AGENTS.md` §2.3 / `agent-workflow.md` §B6：5.1 按 GBK 写重定向日志、从 Node spawn 时没有 `Cert:` 提供程序致签名必失败）。旧文「本环境 `pwsh` 是 `...\WindowsApps\pwsh.exe` 残桩（静默不执行），可用的是 PS 5.1」**与今天实测相反，不要再据它行事**。
  另：PowerShell 工具可能**不回显 stdout**（2026-10-05 再现）—— 跑脚本时务必 `*> <log>` 重定向后读日志，
  否则会误判"没跑"而重复执行（曾因此重跑发布脚本，实际第一次已成功）。
  ⚠ 但在 **git-bash** 里调 PS 脚本时**不要用 `*>`**：`*` 会被 bash 通配展开成当前目录文件名、把多余参数喂给 PS
  （2026-09-29 实测：`-Configuration` 收到 `"AGENTS.md"` 触发 ValidateSet 报错）→ 用 `> <log> 2>&1`。
- **侧载 ≠ 生效（分层，2026-09-29 运行实例实测）**：手工把产物铺进 `plugins/<X>/versions/<ver>/` 并写 `current`，
  **只热切前端资源**——`PluginFrontendFileMiddleware` 按 `current` 取 `web/dist`，实测同一指针一改
  `GET /plugins/<id>/web/dist/index.js` 伺服字节数立刻随之变（40184↔30220）；
  而 `GET /api/plugin` 仍报旧版本，**后端程序集/控制器/MCP 工具不变**（要 `POST /api/plugin/update/{id}` 或宿主重启才换）。
  → 只铺文件 + 切 `current` = **新前端打旧后端**的半升级态（新端点 404）。因此：
  ① 插件若依赖本次宿主侧改动，铺完**不要把 `current` 指向新版本**，留旧版本号当回滚位；
  ② 激活动作交给 `POST /api/plugin/update/{id}`（或页面插件更新源），别把手工铺当交付；
  ③ `publish-plugin.ps1` 的共享程序集过滤只匹配 `.dll`，**`.pdb` 会漏进版本目录**（2026-09-29 实测混进
  `ForgeSelf.Abstractions.pdb`/`ForgeSelf.Core.pdb`），铺完需手删或先修脚本。
- **e2e 地址一律取 `e2e/helpers/e2e-env.ts`**（PILOT-050）：spec 内不得硬编码 `localhost:7102/7002`，
  宿主端口现由 `FORGESELF_PORT` 动态派生、运行目录固定 `.temp/e2e/wt-<hash8>`（`current.json` 为跨进程真源）。
