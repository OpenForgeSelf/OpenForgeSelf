# Part C - 变更记录（agent-workflow 规则库自身的演进台账）

> 本文件是 `agent-workflow.md`（2026-10-07 起拆分）的**一个分册**。§编号（A1-A10 / B1-B12）与规则文字**未作任何改动**，
> 索引与「§编号 → 文件」地址表见同目录 [`README.md`](README.md)；旧路径 `../agent-workflow.md` 保留为薄指路文件。

<!-- ===== 以下为原文（自 docs/04-standards/agent-workflow.md 按行区间拆入，未作任何改写） ===== -->
## Part C — 变更记录

| 日期 | 变更 |
|------|------|
| 2026-10-04 | **PowerShell 执行口径统一（输入11/12）**：B6 新增两条铁律——① 执行脚本一律 `pwsh`（7.x）、禁止 `powershell`（5.1），唯一例外是"专门复现 5.1 专属缺陷"，并登记 `scripts/hooks/pre-commit` 的既有偏差（改法待拍板，入 TODO）；② 捕获 git 等外部命令 UTF-8 输出前必须先切 `[Console]::OutputEncoding`（乱码曾被烤进 RELEASE-NOTES，守卫 = `RepositoryScriptTests.GitLogCapturingScripts_MustSwitchConsoleToUtf8First`）。同批：签名策略定稿「本地发布必带 `-Sign`、CI 默认不签」（真源 `packaging-upgrade-backup.md` §1.1、AGENTS §2.3、`plugin-publish-verify` 同步）。 |
| 2026-10-02 | **dotnet test 数据根自动隔离（输入4）**：修复「本地开发污染真实宿主根 `~/.forgeself`」架构缺陷——`ForgeSelf.Api.Tests` 新增 `TestDataRootIsolation`（`[ModuleInitializer]` 兜底数据根到仓库内 `.temp/dotnet-test/<ts>-<pid>` + 重定向 `XTrace.LogPath`/`Setting.LogPath`/全部已知 `Config<T>.FileName`）+ `TestDataRootIsolationGuardTests`（4 守卫）；B12 第 830 行由「手工前缀必须设 `FORGESELF_DATA_ROOT`」改为「已自动隔离，手工仅用于覆盖」。同时记录宿主侧关联缺陷（`Program.cs:38`/`AppBuilder.cs:89` 的 `Save()` 早于 `ConfigUnifier`）另立 TODO。 |
| 2026-10-01 | **e2e 宿主签名 shell 选择实证（design-system M1 验收中发现）**：从 Node spawn 的 `powershell.exe`（5.1）无 `Cert:` 提供程序（`drive=False certs=0`，签名必失败）；`pwsh` 同语境正常（`drive=True certs=1`）。`e2e/global-setup.ts` 宿主签名固定 `pwsh`；B6 增补「Node→PowerShell 证书/签名操作只用 pwsh」规则；`e2e-testing` 技能同步。 |
| 2026-09-30 | **e2e 共享基建改造（PILOT-050）**：① 宿主新增启动端口覆盖 `FORGESELF_PORT`（env 优先）/`--server-port`（`StartupPortResolver`，覆盖即落盘 ForgeSetting.config，重启一致）；② e2e 运行目录按 worktree 稳定派生 `wt-<hash8>`（去时间戳，消除 Windows 防火墙弹窗根因）+ 残留宿主保护 + SQLite 无条件覆盖；③ 前后端端口动态认领（tmpdir 注册表跨 worktree 互斥）+ 三通道注入（`E2E_BACKEND_URL`/`E2E_FRONTEND_URL`/`FORGESELF_PORT`），e2e 地址真源统一 `e2e/helpers/e2e-env.ts`，spec 硬编码 7102/7002 清零（代码级 11 处）；④ port-config.spec 端口无关化；⑤ e2e-published 修 `publishDir` 越级 bug；⑥ AGENTS.md 收口唯一开发流程（specs/speckit 弃用、§0 强制读规范）、pilot 目录加日期前缀。B2/统一 e2e 体系/B4 已同步。 |
| 2026-09-28 | dsh 对齐专项 B8（六闸门）+ B9（退役与清理）收官：新增 B12 小节沉淀——XCode 原生 SQL 片段绕 NotLike、Known Folder 不读 USERPROFILE 环境变量、`[..N]` 必须 clamp、插件 vitest 归宿主收集、多 call FIFO 配对契约、grep 守门模式、前端全量抖动定性法、.NET 测试环境绕法固化。 |
| 2026-09-27 | 用户指令（seq17）发布规范改写：更新地址支持**本地目录**（`UpdateConfig.Provider=local` + `LocalDir`、`UpdateSettingsService` 运行时可变配置、UpdateChecker 本地分支、`release-local.ps1 -UpdateDir`、设置页更新源配置卡片）；发布规范改为**打 tag 自动发布 + 页面自动更新**，**禁止 agent 停/启/杀用户宿主**（AGENTS.md §0 门禁、§2.3/§2.4、plugin-development/plugin-publish-verify 技能、B5/B10 同步；run-plugin-publish-verify.ps1 降级为插件侧载可选路径、须用户同意）。 |
| 2026-09-27 | AI-Native 闭环规范回炉（用户指令 seq14「不与既有体系映射，完全按新规范走」）：规范升 **v1.1.0**——删除与 Loop/speckit/plugin-team-sop 的映射节，改为「开发流程唯一依据 + 冲突以本规范为准 + 闸门1/2/3 自含定义（§1.1）」；AGENTS.md 头部/红线/§11 同步去映射；群 SOP `ai-native-engineering-loop` 升 **1.1.0**（自含闸门/熔断/汇报，去除 plugin-team-sop 依赖）并重绑本群。 |
| 2026-09-27 | 用户指令（群 seq10）：AI-Native Engineering 九阶段闭环固化为强制流程规范——新增 `docs/04-standards/ai-native-engineering-workflow.md` v1.0.0 + 模板 `docs/18-templates/ai-pilot/`（00~07 八份）+ 产物落点 `docs/ai/pilot/<task-id>/`；AGENTS.md 新增 §11 与 §0 红线引用；群 SOP 新增 `ai-native-engineering-loop` 并发布绑定（与 plugin-team-sop 并列）。 |
| 2026-09-27 | spec 037 R2（用户指令改整合进 AgentHub）：B11 补 ACP 整合路径实测结论（qoderclicn 无原生 ACP、@agentclientprotocol/sdk 包装验证、session/new 必填 mcpServers）与「通用 JS 运行时归宿主 IJsBridge」架构决策。 |
| 2026-09-27 | 新增 B11 Qoder CN Agent SDK 接入（spec 037 协议验证）：postinstall 手动补跑、受信目录审批陷阱与 PreToolUse ask 强控链路、私有 JSONL 非 LSP/ACP、interrupt 正常终态、认证退出码 41。 |
| 2026-09-26 | spec 036 自动更新落地：B6 补 update-agent BOM 实弹代价与守卫测试；B9 新增「真机走查」小节（live 配置必须显式 E2E_API_TOKEN、破坏性用例双门控）；B10 补 git push 代理绕行与私有仓库资产 API 直链下载两条硬规则。 |
| 2026-09-26 | spec 036 端到端验收全绿（live 一次性 49.5s，0.1.0→v0.2.4）：B10 补「并发写路径下状态机每侧写入都要守卫在途状态」硬规则（D-036-5，6b09654）。 |
| 2026-09-26 | 更新源仓库转公开：移除 `UpdateConfig.GitHubToken`、`FORGESELF_UPDATE_TOKEN` 环境变量回退与 `CreateGitHubRequest` 的 Bearer 头（UpdateChecker 保持匿名）；同步移除 `UpdateController.githubTokenConfigured` 与前端提示；B10 补「匿名访问私有仓库返回 404 而非 401」诊断硬规则。 |
| 2026-09-26 | 任务3 交付纠偏（seq31/34）：新增 A10 群协作 SOP 对齐（命中判定第一动作、放权≠免闸门、markdown 正文汇报、工时超断点、缺陷复现分层、串岗禁令）；群 SOP plugin-team-sop 升 **v1.2.0** 已发布并绑定本群；GitHub 主远程与更新源切换至组织仓库 OpenForgeSelf/OpenForgeSelf（B10 同步）。 |
| 2026-09-28 | 输入30 建立打包/升级/备份/缓存唯一真源 `docs/04-standards/packaging-upgrade-backup.md`（QQNT 式目标目录结构：宿主 `versions/` + `plugins/` 并排 + 去插件备份/`_backups` + 更新缓存清理）；AGENTS.md §2.3 与本文 B5/B10 改为引用真源 |
| 2026-09-24 | 本文档创建：Part A 承接 AGENTS.md 触发式细节；Part B 承接原 `.forgeself/memory/MEMORY.md` 项目不变规则归档（随 docs 入库）；AGENTS.md 瘦身为「每次必守 + 引用本文」；MEMORY.md 改为会话级索引。

