# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。
> 本工件覆盖批次 1（输入31）+ 批次 2（输入34）全链终态。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。Intent 的 Expected Outcome 五项（真源唯一 F1 输入30；插件去 `_backups` F2；宿主升级去整目录备份+缓存清理 F3；图片缓存 TTL F4；宿主 QQNT 式目录结构 F5=批次2 输入34）**全部落地**：批次1 已提交 aed3789；批次2 代码全部实施（根启动器 + 发布脚本 QQNT 布局 + update-agent 版本化应用 + 008 冻结 + 插件装包版本化），端到端演练通过。
2. **实现是否符合 Spec？** 是。F2.1-F2.9、F3.1-F3.3、F4、F5 与实施逐一对应；B1-B6 业务规则在代码中体现（版本基准统一、EnsureStaged 早退检查、PruneVersions 保留当前+上一版、失败占位不写缓存）；批次2 新增约束（版本号合法正则 `^\d+(\.\d+){0,3}$`、同级/降级拒绝、AssemblyName 避开宿主 ForgeSelf.dll、ALC 版本目录解析）与设计一致。
3. **是否超出了 Scope？** 否。批次1 改动文件全部在 04-task Task A Allowed 清单内；批次2 文件均在 04-task Task B 清单（宿主路径基准/启动器/自更新/发布脚本/008 冻结/插件装包版本化）内；未引入依赖（Bootstrapper 的 WindowsDesktop.App FrameworkReference 为运行所需）；未改对外 API 契约面。
4. **是否修改了不应该修改的文件？** 否。文档同步均为真源与既有 `_backups` 描述修正；040-B1 在飞区文件零触碰（git add 历次精确隔离）。
5. **测试是否覆盖 Acceptance Criteria？** 是。AC1 build 0 error ✅；AC2 过滤集 56/56（批次1）+ 23/23（批次2 终态）✅；AC3 grep 残留分类核对 ✅；AC4/AC5 代码实查 ✅；AC6 文档同步命中核对 ✅；批次2 端到端演练（QQNT 冒烟 + update-agent 8 项 + 重启段 + 发布管线 zip 65.3MB）全部 Verified ✅；AC7 工件链校验见下。
6. **是否存在明显回归风险？** 低～中。对外 API 面零变化；兼容存量扁平布局（versions/ fallback 保留、update-agent 扁平存量迁移清理）；根启动器为新增入口不影响既有运行实例；全量测试 9 失败全部为既有四族 + 040-B1 在飞区，零交集。中风险点 = 宿主路径基准改动（已在隔离实例冒烟验证 ContentRoot/插件目录正确）。
7. **是否存在架构不一致？** 无。QQNT 结构（公共层 + versions/<ver>/ + current + Plugins 并排）与真源 §3 T1-T6 一致；版本多共存即回滚，符合设计裁决；008 冻结后 036 为唯一更新链路。
8. **Evidence 是否足以证明任务完成？** 是。Build/过滤集/端到端演练/发布产物均为 Verified 实测输出（05-evidence 逐项记录）；未提交项（git 状态）如实标注。

## Requirement Check

PASS（批次1 + 批次2 全部 AC 达成；真源状态行/§3/§5/变更记录已标记批次2 实施完成）

## Scope Check

PASS

## Test Check

PASS（批次1 过滤集 56/56；批次2 终态过滤集 23/23；端到端演练 8/8 + 冒烟 + 发布管线；全量失败归因明确零交集）

## Architecture Check

PASS（QQNT 结构落地与真源/设计一致；008 冻结收口双链路；无隐式耦合变更）

## Risk

L2（批次1 L1 + 批次2 L3 高影响面；已通过隔离实例冒烟 + update-agent 演练 + 发布管线实测降低至 L2；**未触碰用户运行实例 51888 / D:\src\tools\ForgeSelf**；批次2 未提交 git，待用户审核）

## Findings

### Critical

无

### Major

无

### Minor

- `DataStoragePanel.vue`「清除缓存」按钮未见后端 API 接线（P2 TODO 已登记）。
- 全量测试 9 条既有失败（WorkflowPlanning 404×6 等）与 040-B1 在飞区并存，dsh 批次收口时一并处理（已有 TODO 登记）。
- C 盘 4GB 剩余：测试 TEMP 走 D 盘 `.forgeself\test-tmp` 绕开，根因未修（环境问题）。
- 冒烟遗留僵尸 ForgeSelf 进程（Access denied 杀不掉）：不监听不占 mutex，不影响后续。

## Final Decision

APPROVED（批次1 + 批次2：代码 + 证据齐备，工件链完整；闸门2 由用户审核本工件链后确认；闸门3 提交须用户明确指示——批次1 已提交 aed3789 并推送双远程，批次2 未提交、等用户指示）

---

## 输入36 单文件化（2026-09-28 追加评审）

**状态**：PARTIALLY_COMPLETED（公共层已落地并 Verified；业务层 publish 被 040-B1 在飞区编译错误阻断待验）。

- **实现方向**：FDD 单文件（公共层薄壳启动器 ~170KB + 业务层每版单文件 exe）+ DOTNET_ROOT 结构运行时公共一份；**自包含单文件弃用**（解压缓存 + BaseDirectory/ProcessPath 指向提取目录，实测踩坑，与省空间目标相悖）。
- **已验证**（05-evidence 输入36 节）：公共层组装 627 文件 + 启动器 0.16MB；进程拉起链路（启动器→versions/current→子进程→退出码 7 透传 + 业务层 BaseDirectory=versions/<ver>）；组装 zip 83.9MB（dummy 模拟业务层）；artifacts 清理 918MB。
- **未验证（阻断）**：业务层真实 publish 全量构建被 040-B1 `AgentHubPlugin.cs(57) CS0104/CS0311 IAgentRegistry 二义` 挡（并行会话在飞区，本任务不碰）；TerminalCommandGuard 断言修复同被挡。待 040-B1 收口后跑 `release-local.ps1` 全量验证（真实业务层单文件 + wwwroot/插件 targets 交互 + update-agent 端到端升级演练）。
- **风险**：进程拉起模式变更启动链形态（ALC → 子进程），HostPid 语义 = 业务层进程（update-agent 时序：业务层退出 → 根启动器随之退出 → update-agent 重启根启动器拉起新版本，已按此设计，需端到端复验）；未提交 git，等用户指示。
- **Final Decision（输入36）**：CHANGES_REQUIRED——仅缺「业务层全量验证」一环（040-B1 阻断，非本任务范围），公共层部分可先行交付演示；收口后复评 APPROVED。


---

## 输入37 目录命名统一小写（2026-09-29 追加评审）

**状态**：COMPLETED（代码 + 脚本 + 测试断言 + 文档全量落地并验证；未提交 git，等用户指示）。

**审查要点**：

- **实现是否符合 Intent？** 是。用户输入37 三点全部落地：① 专注本次任务需求、其他发现只记 TODO（全量测试 Temp 环境问题、DataStoragePanel 清除缓存接线、端口真源等均登记 TODO 未越界处理）；② 安装目录命名统一小写——`Plugins→plugins`（安装根 + 数据根插件数据）、`Data→data`（开发态数据根）、`Log→log`（日志外置数据根/log）、`Config→config`（配置目录），源码工程目录（`ForgeSelf.Api/Plugins/<X>`、命名空间绑定）按真源 R9 保持 PascalCase；③ NewLife.Core 相关设置查 DeepWiki（NewLifeX/X）+ 官网 + 独立探针实测：`XTrace.LogPath`/`NewLife.Setting.Current.LogPath/DataPath` setter 可用，日志外置 `{数据根}/log` 落地。
- **是否符合真源？** 是。真源 `packaging-upgrade-backup.md` 补 R9/§1.5/§3-T8/§5/变更记录；AGENTS.md §2.3 引用行补输入37 登记；agent-workflow/035/038/034/032/052/configuration/Plugins README/DesignSystem/plugin 技能按真源收敛目录事实。
- **是否超 Scope？** 否。仅目录命名小写相关代码/脚本/测试断言/文档；未顺手修无关问题（全量测试环境问题只记 TODO）。
- **测试覆盖？** 目录相关单测 6/6 绿（DataLocation 5 + ForgeConfig 1）；全量 597 失败归因系统 Temp 拦截（XCodeTestFixture 共享夹具构造失败，git status 确认该文件未被触碰，与本任务零交集）；TMP 重定向 testhost 崩溃进一步证实环境级不稳定。
- **回归风险？** 低。路径基准语义未变（`plugins`/`data`/`log`/`config` 均为既有目录的改名/新建，Windows NTFS 大小写不敏感兼容存量大写目录）；update-agent 带幂等目录名规范化兜底存量；日志外置为启动早期单点设置（AppBuilder 首个 NewLife 调用前）。
- **架构一致？** 是。小写化不改变 QQNT 结构语义（公共层 + versions/<ver>/ + plugins 并排 + 去备份）；真源 R9 明确「源码工程目录不变」边界。
- **Evidence 充分？** 是。05-evidence 输入37 节全 Verified：build 0 errors / 单测 6/6 / 探针实测 / 脚本 AST 7/7 / 规范化 dummy 含幂等 / 仓库目录改名。

**Final Decision（输入37）**：APPROVED（未提交 git，闸门3 等用户明确提交指示；提交时须与批次2+输入36 汇总一次性提交并精确隔离 040-B1 文件）。


---

## 输入37 发布复验与日志外置修正（2026-09-29 二次追加评审）

**状态**：COMPLETED_WITH_RISK（发布全链路验证通过 + 日志外置修正验证通过；两个已知风险：运行态 XCode 探测残留记 TODO P2、040-B1 并行会话破坏全量编译为外部阻塞）。

**审查要点**：

- **发布是否合理？** 是。`release-local.ps1 -Version v2.2.11` 全链路成功（297s）：真实业务层 FDD 单文件（ForgeSelf.exe 15.66MB）+ 公共层 DOTNET_ROOT 运行时 + 18 插件 + wwwroot → zip 101.8MB；QQNT 小写布局与真源 §3 完全一致（顶层 host/shared/plugins/versions，无大写残留）；zip 内版本目录干净（无 Plugins/data/log/config）。
- **日志外置是否真生效？** 是（修正后）。探针对照实测：`Setting.LogPath` 不联动 `XTrace.LogPath`（独立静态属性）→ 改 Program.cs 顶部 + AppBuilder 直接设 `XTrace.LogPath` → 冒烟验证日志落 `data/log/`、程序目录 Log/ 不再生成。此为输入37 目录小写/空间优化的关键一环，已闭合。
- **运行残留（versions/<ver>/Plugins SQLite ~15MB）怎么办？** 归因 = NewLife.XCode 库探测行为（宿主代码无大写 Plugins 字面量；DeepWiki 无配置项）。安装包不含（组装清理 ✓）、当前版本运行必需；跨版本累积为空间浪费 → 登记 TODO P2（update-agent 升级后清理非当前版本运行残留候选），未扩改本次范围（符合输入37「专注任务」约束）。
- **阻塞是否如实记录？** 是。040-B1 并行会话改动 Abstractions/Core 接口导致 AIAgent 编译断（CS0019，非本任务文件）；本任务代码经 `-p:BuildProjectReferences=false` 编译 0 errors 验证。全量 `dotnet build` 待 040-B1 收口后可复验。
- **Evidence 充分？** 是。05-evidence 二次追加节全 Verified：发布 zip/布局/冒烟/探针/修复后落点逐项实测。
- **Scope 控制？** 是。仅修 LogPath 一处（输入37 既有目标）；运行残留与编译阻塞均只记录不扩改。

**Final Decision（输入37 二次追加）**：APPROVED（COMPLETED_WITH_RISK）。未提交 git（等用户明确指示；提交时汇总批次2+输入36+输入37 全部改动并精确隔离 040-B1 文件）。

---

## 输入38：SQLite 驱动正式依赖化——Review（2026-09-29）

**八问**：

- **输入38 目标达成？** 是。`XCode.SQLite` 11.24.2026.302 已入 csproj，驱动成为正式依赖；探针 + dev-bin 冒烟 + 单文件最终冒烟三级验证：SQLite 本地加载、零外网下载、运行态不再生成 `Plugins/`（输入37 遗留 ~15MB/版本运行残留根除）。
- **单文件兼容是否解决？** 是。内嵌 + 落盘 LoadFrom 同 identity 冲突（FileLoadException already loaded）→ csproj Target 从 `FilesToBundle` 剔除 + publish-host 外置复制 → exe 16,424→16,036KB，探测命中落盘文件（`加载 ...\System.Data.SQLite.dll 版本v2.0.2.0`），8 库全部连接成功。
- **发布脚本是否同步？** 是。`package-release.ps1` inject 块删除；仓库 `build/runtime/plugins` 移 `.trash/`（可回滚）；全仓无残留引用（除说明注释）。
- **文档是否统一？** 是。唯一真源 packaging-upgrade-backup.md（§1.1 注入行 + §5 变更记录）+ agent-workflow.md（L611/729/736/451）同步为「包依赖 + 单文件剔除外置」新认知；035 无 SQLite 相关行无需改。
- **包版本选型是否稳健？** 是。11.24 线不触发 XCode 主版本升级；12.2 配套（XCode 12.2 + XCode.SQLite 12.x）记为未来选项未扩改。
- **阻塞/风险？** 040-B1 并行会话历史阻塞已收口（本次单文件 publish 全量构建成功，无需 -p 绕过）；XCodeConfigTests 4 失败 = 既有系统 Temp 拦截环境问题（记 TODO，非本任务引入）。托盘线程 H.NotifyIcon 异常 = 本环境无桌面托盘（宿主服务正常）。
- **Scope 控制？** 是。仅输入38 目标范围（包依赖化 + 单文件兼容 + 脚本清理 + 文档同步）；未来项（XCode 12.2 升级）只记录。
- **Evidence 充分？** 是。05-evidence 输入38 节全 Verified：探针/冒烟/发布体积/脚本/文档逐项实测。

**Final Decision（输入38）**：APPROVED。全部改动未提交 git（等用户明确指示；提交时汇总批次2+输入36+37+38 并精确隔离 040-B1 文件）。

## 输入39 Review（版本号生成 + 文件图标 + 包信息）

- **输入39 目标达成？** 是。版本号自动生成（CrazyCoder 式日期版本）+ 两个 exe 文件图标 + 包信息全部落地并实测。
- **版本策略是否自洽？** 是。csproj 日期版本（2.2.<yyyy.MMdd>）= 程序集/文件版本（构建事实）；发布 tag v<X.Y.Z> = 更新语义（update-agent 用），两者并存不冲突；AssemblyVersion 通配 2.2.*（每次构建 build/revision 递增）+ Deterministic=false。
- **图标是否到位？** 是。Bootstrapper（公共层根 ForgeSelf.exe，用户双击对象）此前无图标 → 复用 Api 的 Assets\ForgeSelf.ico；两 exe ExtractAssociatedIcon 实测均 32x32。
- **包信息是否合理？** 是。AssemblyTitle/Description/Company/Product/Copyright 已设（OpenForgeSelf / 铸己匣 / ©2026），文件属性实测可见。
- **影响面/风险？** 低。仅元数据属性，无逻辑改动；win-x64 旧 obj 缓存已隔离（.trash 可回滚）；插件不引用宿主 AssemblyVersion，无版本绑定影响。
- **Scope 控制？** 是。仅两个 exe 项目；McpCenter 人工固定版本（插件语义）与 Core/Abstractions 未动（需要时再统一）。
- **Evidence 充分？** 是。05-evidence 输入39 节全 Verified（文件属性逐项实测 + 图标 API 双验 + build 0 errors）。
- **遗留？** win-x64 历史残留 bin 目录（无害构建产物，发布不依赖）；XCodeConfigTests 4 失败 = 既有环境问题（记 TODO）。

**Final Decision（输入39）**：APPROVED。未提交 git（等用户明确指示；提交时汇总 028 全批次并隔离 040-B1 文件）。

