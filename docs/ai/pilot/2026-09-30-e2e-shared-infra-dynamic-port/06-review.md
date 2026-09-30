# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> Task：PILOT-050（2026-09-30 e2e 共享基建改造）

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。动态端口 + 稳定目录 + 多 worktree 并行三条主诉求全部落地且有真实运行证据（第三轮深档 e2e：7002/7102 被占自动顺延 7003/7103、目录 `wt-b26d4625` 稳定、全链跑通 34.1m）。
2. **实现是否符合 Spec？** 符合。FR-1~FR-7 逐条对应：FR-1 StartupPortResolver（env 优先 + CLI + 落盘）；FR-2 FORGESELF_DATA_ROOT 隔离（current.json.dataDir 实证）；FR-3 wt-<hash8> 稳定目录；FR-4 三通道注入 + 认领注册表；FR-5 代码级硬编码清零（11 处）；FR-6 port-config 端口无关 + e2e-published 修复；FR-7 文档回写。
3. **是否超出了 Scope？** 超出两处，均为 Plan 运行期暴露的必要最小改动并已记入 03-plan 偏差表：① 宿主 `FORGESELF_NO_TRAY` 守卫（托盘崩溃打宿主，深档阻断项）；② 单测 env 注入式重构（env 外泄致全量 70 败）。两处均有实证数据支撑，非范围蔓延。
4. **是否修改了不应该修改的文件？** 否。后端仅 Program.cs（托盘守卫一行守卫块）/ AppBuilder.cs（接入一行）/ 新增 StartupPortResolver；未触碰业务逻辑、鉴权、DB、插件。前端仅 e2e 基建与 spec 地址来源。
5. **测试是否覆盖 Acceptance Criteria？** AC-1 单测 7/7 绿；AC-2 深档实证（宿主绑定注入端口）；AC-3 目录稳定（wt-b26d4625 复用）；AC-4 动态贯通实证（7003/7103）；AC-5 硬编码清零（grep 复核代码级 0 残留）；AC-6 port-config 6/6 过 + e2e-published 修复；AC-7 三档门禁跑毕并判责（详见 05-evidence）；AC-8 文档回写完成。**AC-7 判责依赖「基线红清单未落盘」下的计数+域分布推理，置信度略降（见 Finding Minor-1）。**
6. **是否存在明显回归风险？** 低-中。风险点：① spec 改用 env 级联后，若有人不经 playwright config 直接跑单 spec（无 E2E_BACKEND_URL）会回落 current.json → 默认 7102，行为可预期；② rmDirOS 用 `cmd /c rmdir` 属 Windows 依赖（e2e 本就依赖 Windows，平台限制已上报）；③ `FORGESELF_NO_TRAY` 只影响 --console 模式托盘，服务模式不经此路径。
7. **是否存在架构不一致？** 无。端口真源仍是 ForgeSetting（resolver 只是启动期覆盖源，写回同一真源）；e2e 地址真源收敛到 e2e-env 单点，与「消灭散落硬编码」方向一致。
8. **Evidence 是否足以证明任务完成？** 足以。三档门禁均有真实命令输出；关键机制（端口顺延/托盘守卫/绕 shim 清理）各有失败→修复→复跑的闭环实证。

## Requirement Check

PASS

## Scope Check

PASS（两处偏差已记录且有实证必要性）

## Test Check

PASS（三档门禁跑毕；判责依据真实报错原文）

## Architecture Check

PASS

## Risk

L2（e2e 基建为全项目共享面，改动波及所有 spec 的地址来源；已用全量深档回归对表基线持平）

## Findings

### Critical

（无）

### Major

（无）

### Minor

1. 基线红清单（`project-baseline-test-reds`）仍缺失，本轮判责采用「计数 + 域分布 + TODO 在册」组合证据；建议落盘基线清单（已记 05-evidence Unresolved）。
2. token-init:64（/settings 卡片地址展示）疑既有失败待单独归因；动态 locator 本身正确（:55 同断言通过）。
3. e2e 侧注释仍保留 7102/7002 字样（描述默认回落值），如需彻底清字面可后续一轮处理。

## Final Decision

APPROVED

<!-- 依据：三档门禁真跑 + 判责证据链完整 + 两处 Scope 偏差有实证必要性且已入偏差表；无 Critical/Major。 -->
