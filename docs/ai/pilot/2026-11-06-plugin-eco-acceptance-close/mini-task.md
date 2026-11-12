# Mini-Task：插件生态验收收尾批（2026-11-06-plugin-eco-acceptance-close）

> 轻量单文件工件（§11.5 裁剪：Intent/Spec/Plan/Task 五要素合并）。闸门1 = 用户 2026-11-06 08:04 输入「按你推荐的来，继续推进」，对验收报告四条推荐的执行授权（含推荐1 的插件仓库发布动作）。证据/审查见同目录 05-evidence.md / 06-review.md。
> 适用问题类：无已登记 PC 命中（本批为验收收尾 + 模板缺陷预防性守卫；缺陷类归档不适用，见 06）。

## Intent（为什么/做什么/到什么程度）

验收（同会话上一轮）判定 P3/P4/P5 未真机/未达成。本批按用户批准的推荐补齐：
1. **推荐1（发布链）**：修复 CI 必红的骨架/模板缺陷 → 建 `OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld` 真实仓库 → push + tag v1.0.1 → GitHub Actions 绿 → Release 产 zip。
2. **推荐2（真机演练）**：隔离实例完成 12 步中的 6/10/11/12：ZIP 升级安装→指定版本激活→制造激活失败自动回滚→卸载保留 src/。
3. **推荐3（立项+对账）**：进程隔离门槛立 TODO；中档全量重跑对账。
4. 跨环境 GitHub Release 真实安装（P4 闭环；NuGet 真实上传被 NUGET_API_KEY 缺失阻断，如实报告）。

## Spec（边界与契约）

- 骨架（`​.temp/dev-stack/data/plugins/hello-world/src/`）为发布源；版本统一走 **3 段 + tag**（v1.0.1 = plugin.json Version = Release zip 内清单 = versions 目录名；NuGet 版本三段合法，时间码只存在于本地 package-plugin.ps1 产物——纠正 v2 会话把时间码写进源码清单的偏差，对齐 FR17）。
- CI workflow（模板与骨架同步修）：无 `${{ secrets.* }}` 出现在 `if:`（GitHub 不允许；GITHUB_TOKEN 恒在 → release 步去 if）；密钥仅经 step `env` 映射进 nuget 推送（未配置则打包后跳过上传，CI 仍绿）；test/publish/pack 显式工程路径（仓库无 sln）；打包步把根 `plugin.json` 拷进产物并剔除宿主共享程序集（`ForgeSelf.*`/`NewLife.*`/`Microsoft.Extensions.*`/`XCode`/`MX`），保证 ZIP 根=完整插件且可被宿主安装。
- csproj 引用策略：废弃 8 级相对 Exists ProjectReference（CI 上必 CS0246），改为提交 `lib/ForgeSelf.Abstractions.dll`、`lib/ForgeSelf.Core.dll` + `<Reference HintPath Private="false">`——仓库自包含，本地/CI 同一套。
- 模板（`PluginSrcScaffold.cs`）与骨架同类修复：workflow、csproj、lib 拷贝、manifest 写入工程目录（不再写根）、补 .gitignore；不改 `PluginScaffolderService` 字节包契约。
- 宿主运行面只碰隔离 dev-stack 实例（禁碰 :51888 与用户宿主）。

## Interaction Design / 无界面交互（显式声明）

本批**零界面代码改动**（宿主 `src/views`、插件 `web/` 均未改），属「无界面交互」任务：交付物为 CI workflow 模板、骨架 csproj/manifest 与守卫测试。
操作入口全部为 HTTP 端点，供后续批次与人工调用：`POST /api/plugin/install/{zip|source}`、`POST /api/plugin/{id}/activate`、`POST /api/plugin/{id}/uninstall`、`GET /api/plugin/list`。
入参示例：`{"kind":"github","locator":"https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld"}`；错误提示形如 `{code:"plugin_version_not_found"|"plugin_activation_failed"|"plugin_package_invalid", message}`（前端 `PluginsPanel.vue` 原样展示，本批未改其交互）。
验收侧唯一的浏览器动作是「PluginStore 走查截图」，属**验收手段**而非本批功能变更。

## Plan（真实文件）

1. `ForgeSelf.Api.Tests/Plugins/PluginScaffoldReleaseTemplateTests.cs`（新增，TDD 红）：守卫 G1-G6（模板 workflow 无 secrets-in-if / 显式路径 / 拷 plugin.json / 剔共享程序集；csproj lib 引用；manifest 落工程目录；.gitignore 存在）。
2. `ForgeSelf.Api/Plugins/Services/PluginSrcScaffold.cs`（改）：模板修复 + lib 拷贝 + .gitignore。
3. 骨架文件（改/增）：csproj、release.yml、lib/*.dll、plugin.json+manifest → 1.0.1、.gitignore 校对、清理根级陈旧 bin/obj。
4. 验证命令：`dotnet build ForgeSelf.Api` + 过滤集（PluginEcosystem|PluginInstaller|PluginVersionUpdateSource|PluginPackager|PluginRoots|PluginVersionService|PluginScaffoldReleaseTemplate）+ 骨架 `dotnet build`（证明 CI 可编译）+ `scripts/package-plugin.ps1 -Source <骨架>` 实跑出 zip + 中档全量（后台）。
5. 发布链：git init → commit → GitHub REST 建仓（git credential 凭据，不回显）→ push → tag v1.0.1 → 轮询 CI → 核对 Release 资产。
6. 演练：dev-stack（默认数据根，先快照 versions/）→ API 序列（install zip → activate 1.0.1.<tc> → 坏 DLL v1.0.2 激活 → 断言回滚 → uninstall → 断言 src/.forge 留存）→ PluginStore 浏览器走查（截图）。
7. 跨环境：第二隔离实例（`-DataRoot .temp/dev-stack-acceptance/data`）→ `POST /api/plugin/install/source {kind:"github", locator:"https://github.com/OpenForgeSelf/OpenForgeSelf.Plugin.HelloWorld"}` → activate。

## Task（Scope / AC）

**Allowed**：上列文件与命令；HelloWorld 骨架仓库；隔离实例。
**Forbidden**：主仓宿主业务逻辑改动（本批仅模板）；:51888 与用户宿主进程；NuGet 真实上传（无 key）；删除骨架 src/；主仓 git commit（闸门3 未批，本批不提交主仓）。
**AC**：① 守卫测试红→绿 ② 骨架本地构建绿（CI 等价证明）③ CI 全绿且 Release 含 zip+SHA256SUMS ④ 跨环境 GitHub 安装+激活成功 ⑤ 演练四步断言全过 ⑥ PluginStore 走查截图 ⑦ 全量对账（新增红=0）⑧ TODO 立项 ⑨ 凭据零回显。
