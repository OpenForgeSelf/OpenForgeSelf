# Review

> 阶段：Stage 8｜Reviewer 视角八问重查。
> Task ID：PILOT-plugin-dev-experience ｜ 日期：2026-10-01

## Requirement Check（是否真正满足 Intent）

**PASS**。三块痛点均有可运行解与实跑证据：
- 后端迭代回路：dev 宿主上 `dotnet build Plugins/<X>` + `POST /api/dev/plugin/{id}/reload`，**同版本号**重载成功 ×3（V7），改码重建后 shadow 内容哈希变化且新行为在日志可见（V8/V9）——R1（版本强耦合）与 R2（DLL 锁）同时解决。
- 前端迭代回路：dev 宿主直读源 dist + no-store 实测 200（V12）；既有内容指纹机制天然破缓存（实施偏差 #2，比原方案更简）——R3 的"产物断链"在 dev 态消除。
- 可观测性：坏插件完整错误（消息+类型+修复提示）在 API 与 diagnostics 双通道可见（V11）；装载/请求期日志带 `[plugin:<id>]` 前缀（V9）。

## Scope Check

**PASS（含 2 项经记录的主动裁剪）**。Allowed 清单外零改动；18 个插件源码目录除 SamplePlugin 临时验证标记（已还原并重建）外零触碰；`PluginVersionService.UpdatePlugin` 严格递增语义、`PluginVersionLayout`、`publish-plugin.ps1` 行为均未改。主动裁剪：3B 分文件日志管线未实施（Known Limitations #3）、2B HMR 仅 opt-in 且浏览器级验证 Unknown——两项均已在 Spec/Evidence 显式记录，无静默缩水。

## Test Check

**PASS**。新增 49 用例（含 RepositoryScriptTests 复验）3 连跑全绿；全量干净轮 27 失败全部 ⊆ 基线名单或已修复（V14）；前端 check 0 错误 + vitest 568/568。Acceptance Criteria 12 条：AC-1~11 Verified，AC-12 模块级 Verified / 浏览器级 Unknown（如实标注）。

## Architecture Check

**PASS**。dev 能力以"平行新路径"实现而非改造既有链：`DevMode` 单点判定 + action 首行短路 + `UseWhen` 式隔离；`ReloadPlugin` 复用既有 `Destroy→Collect→Refresh→Enable` 编排，未复制逻辑；shadow 副本严格复用 publish-plugin.ps1 的共享 DLL 排除名单（R8② 无违反）；Production 零行为变化由 V15 实机证明（401/404 分层正确）。

## Risk

| 级别 | 风险 | 缓解 |
| --- | --- | --- |
| L1 | dev 宿主高频 reload 可能累积被锁 shadow 陈旧目录（句柄释放延迟） | Prepare 剪枝 + 启动 `CleanupUnlocked` 清扫双保险；实测每插件当前目录恒 1 |
| L1 | `PluginTaggedLog` 包装全局 XTrace.Log，若 NewLife 升级新增 ILog 成员会编译期报错（好事，不会静默） | 成员清单已按 11.17.2026.701 核对；编译期暴露 |
| L2 | 2B HMR 的 alias 方案浏览器级行为未验证，可能静默双 Vue | 默认关闭（FORGESELF_DEV_WEB_HMR 需显式 =1）；脚本/文档双处警示；回退零成本（关开关用 2A 回路） |
| L2 | 并行会话环境劣化（系统 Temp 拒访）会放大全量测试失败数，掩盖真实回归 | 证据链已记录 TMP 重定向办法与基线对照方法；日记留痕 |

## Findings

- **Minor**：`ForgeSelf.Web/src/types/plugin.ts` 的 PluginState 枚举注释（Error=9）与后端实态（坏插件 state=10，PluginState.cs 实有 11 值）不一致——既有偏差，非本任务引入，建议另开工单。
- **Minor**：`ForgeConfigTests.ForgeSetting_配置文件归一` 在基线即失败（环境性），本次对照中两份名单的时长后缀差异曾造成误判，对比脚本须剥离时长与截断参数（已在证据中说明）。
- **Process**：全量测试期间必须停掉一切自起宿主实例（ConfigUnifier .tmp 争抢，TODO.md:86 + 本次两轮实证）——建议后续把该坑升格进 agent-workflow.md 门禁清单。

## Final Decision

**APPROVED（附条件）**——条件：① 2B HMR 浏览器级走查在使用时补做；② 3B 分文件日志按需另立工单；③ 未获用户闸门2 验收与提交授权前不 git commit。

八问结论：实现满足 Intent（PASS）｜符合 Spec（PASS，偏差已记录）｜未超 Scope（PASS）｜未改不该改的文件（PASS）｜测试覆盖 AC（PASS）｜回归风险可控（L1-L2）｜架构一致（PASS）｜Evidence 充分（PASS，2 项 Unknown 如实标注）。
