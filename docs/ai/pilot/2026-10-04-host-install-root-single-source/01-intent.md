# Intent

任务：`2026-10-04-host-install-root-single-source`（内容＝**插件根两路解析**）｜闸门1 已由用户 2026-10-04 19:2x 口头下达（「先改一下，尽快增加解析支持」）⇒ 已实施；本文件记录被推翻的第一版与最终裁定。

## Problem
宿主升级后整台实例 0 插件（现场：:51888 已升 `2.7.2.0`，`GET /api/plugin` → `data:[]`，宿主日志 `插件目录不存在: …\versions\2.7.2.0\plugins`）。根因不是运行时猜错根，而是**发布布局与运行期解析分叉**：

- 运行期：插件根 = 业务层旁边（`AppBuilder.cs:319-320`，`AppContext.BaseDirectory/plugins`）——这与"内置插件随版本走"的语义**本来就是一致的**。
- 发布期：`scripts/release/package-release.ps1:54-65` 把 publish 产出的 `plugins/` 复制到安装根，再 `Remove-Item versions/<ver>/plugins`（注释「插件公共外置，避免每版本复制」）⇒ 版本目录被掏空，业务层旁边再也没有插件。
- `update-agent.ps1` 无嫌疑（`robocopy /E` 只复制，不剪切）。

同时，用户要求插件还要有**第二来源**：数据目录（与日志/配置同一个根）下的插件目录也要能加载。

## Why
1. **正常单层布局也已分叉**（包里 `versions/<ver>/` 无 `plugins/` 是实测事实），只是老扁平布局与 dev 的 `--plugins-dir` 掩盖了；不修，每次升级都可能复现"插件整批消失"。
2. **插件随版本走才是可回滚的**：版本快照 = 业务层 + 它依赖的内置插件；把插件外置到安装根会让"回滚宿主版本"回滚不掉插件，产生版本/插件错配。
3. **用户明确否决临时方案**：常驻设 `FORGESELF_PLUGINS_DIR` 只是把分叉藏进每个启动入口，换机器/换安装即复发（原话「我不要临时解决，我要正常、长远解决的做法」）。
4. **静默失败**：目录不存在时 `DiscoverPlugins()` 直接 `return` 空列表，界面/API 一起显示"空"，谁都不知道自己扫错了地方。

## Expected Outcome
1. 内置插件**随版本发布**，落 `versions/<ver>/plugins/`；打包脚本取消"外置 + 删除"，并在版本目录缺 `plugins/` 时**当场 throw**（不许静默出空包）。
2. 宿主支持**两路插件根**并按优先级合并：内置根在前，数据根 `{数据根}/plugins`（`PluginDataRootName = "plugins"`，与"插件数据"同树，靠 `plugin.json` 区分）在后；同 Id 冲突由版本号裁决（不可解析一律视为相等 ⇒ 保留内置份，畸形清单不得夺走生效插件）。
3. **每份插件的生效来源可见**：发现日志逐条带来源目录，扫描完成行汇总"插件根 N 个：a | b"；缺某一路只 WARN 不中断另一路。
4. 契约变常驻测试：两路合并、版本裁决、数据子目录不误认、内置根缺失仍能加载、发布布局守卫；每条新判据都有反向探针证明会响。
5. 用户那台实例经一次正常升级即可看到 design-system 3.1.0（不需要手工搬目录）。

## Constraints
- 不改 `update-agent.ps1` 的复制语义与版本保留策略；不改发布包"根公共层 + `versions/<ver>/`"的大框架（只是 `plugins/` 归位）。
- 兼容三种启动形态零回归：dev（源码树 + shadow-copy）、e2e（publish 宿主）、根启动器正常拉起（`WorkingDirectory`/`DOTNET_ROOT` 不动）。
- `--plugins-dir` / `FORGESELF_PLUGINS_DIR` 覆盖优先级保持最高；`PluginManager.PluginsDirectory` 兼容属性仍指内置根（安装/版本服务语义不变）。
- 本批**不搬迁存量目录**、不启停/写用户宿主；**未授权不提交、不打 tag、不推远程**。
- 零新依赖；碰宿主源码 ⇒ §5.6 **中档**（后端全量 `dotnet test`）为验收硬条件。

## Success Criteria
1. 新布局产物：`versions/<ver>/plugins/DesignSystem/plugin.json` = 3.1.0 且 `versions/<ver>/plugins` 存在（按包内容验真，不是按脚本"应该如此"）。
2. 单测：两路合并 / 版本裁决 / 数据目录不误认 / 内置根缺失仍可加载 全绿；布局守卫用例**先红后绿**（旧写法插回必红，已做实）。
3. 中档全量 `dotnet test`：新增红为 0（基线红按 §5.6 先对表再判责，非我的红也要给原文与归属）。
4. design-system 插件层 e2e 不新增稳定红（判据不放宽）。
5. 真源 §1.6 ③/④ 与 §1.1 更正为"两路 + 随版本"；`04-standards` 与技能不残留"安装根 plugins 并排"的旧结构事实。

---

## 第二批（输入19，2026-10-04 19:4x）：版本目录逐代嵌套

用户原话：「还有版本目嵌套的问题也解决，安装根里面的版本目录下的版本里面应该就是程序了，不应该嵌套」。

### Problem
现场（只读复核 `:51888`）业务层跑在 `versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0\ForgeSelf.exe`——三层。读码定因：`StagedUpdateService.ApplyStaged` 把 `Path.GetDirectoryName(Environment.ProcessPath)`（业务层版本目录）当 `-InstallDir` 交给 `update-agent.ps1`，于是代理把新版本落进 `versions/<ver>/versions/<new>/`、又把公共层合并进版本目录；两条同源错置连带发生：步骤 6 的"扁平残留清理"删的是版本目录里的 `wwwroot/appsettings.json`，步骤 9 重启的是版本目录里的业务层 exe（公共层运行时在它上面两层，DOTNET_ROOT 无从传递）。

### Why
1. 版本目录是**不可变快照**，里面再长出 `versions/` 就同时破坏了 T1（快照）与 T5（公共层唯一）两条既定要点。
2. 只改宿主 C# **修不好用户那台**：触发嵌套的那一次是「老宿主 + 随包新代理」，老 exe 已在用户机器上；唯一能在该次生效的位置是随包分发的代理脚本。
3. 用户已否决临时通道（「我不要临时解决」）⇒ 必须是"安装根"的**唯一解析口径**，而不是再开一个环境变量。

### Expected Outcome
1. 安装根解析一处定义、两侧同源（宿主 `HostInstallRoot` ＋ 代理 `Resolve-ForgeInstallRoot`），并有一条**实跑代理脚本**的比对判据钉住"只改一侧必红"。
2. 代理在归一化后仍处于 `versions/<ver>/` 内时**当场 throw、不碰盘**（宁可更新失败也不产生新层）。
3. 下一次升级即自愈：新版本落真安装根的 `versions/<新ver>/`、`current` 指过去、重启根启动器。
4. 存量嵌套层的清理**不在本批**（不可逆面，需用户在场，见 02 U4 与 TODO）。

### Success Criteria（追加）
6. `HostInstallRootTests` 全绿，且反向探针（代理不上跳 + 宿主退回 ProcessPath）能让两条守卫**同时实红**。
7. 真源新增一条生命周期规则（§4-R11）：安装根口径 + 禁止嵌套 + 为什么代理侧也必须做。
