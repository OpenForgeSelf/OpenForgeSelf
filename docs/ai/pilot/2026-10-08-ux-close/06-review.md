# Review

> 阶段：Stage 8 — Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实：CHANGES_REQUIRED 时须指明退回到哪个阶段。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？**
   ✅ 用户三缺陷：① 项目选择（新建下拉 + 详情双模式 + 选中自动带项目地址 + 创建自动关联 projectId）已实现并走查；② 委派禁用原因可见（四态常驻文案 + agents 空态引导跳 /agent-hub）已实现并走查；③ 下发后进度可见（列表实时徽标 + taskKey 短显 + 详情 agent 名 + 15s 轮询 + 终态 toast + 执行记录锚点）已实现，e2e E1 真实委派全链路验证。流程机制化（ui-ux-design skill / Spec 交互设计节 / 走查 UI 符合性清单）已落盘。

2. **实现是否符合 Spec？**
   ✅ 除 FR-1.1 label 格式一处（规格「项目名（地址短显 · N 任务）」→ 实现「项目名 · 地址短显（N 任务）」），其余 FR-1~FR-4 全部按 02-spec 落地；偏差已记录于 05-evidence 并同步更新 02-spec。

3. **是否超出 Scope？**
   ⚠️ 两处超界，均有正当理由并记录：
   - 修改 `ForgeSelf.Web/e2e/global-setup.ts / global-teardown.ts`（deep dives 基建缺陷：delete-pending 锁导致 e2e 三跑/四跑无法清理）。属 e2e 基建，为让本批 e2e 可跑通而修，未动测试语义。
   - 修下拉文案一致性（走查发现 R 重复性问题）——用户诉求「不仅要能做出来，还要好用」，属本批走查闭环内。

4. **是否修改了不应该修改的文件？**
   ✅ 无。业务文件均限 TodoTracker 插件 + 本批 e2e/流程资产；未碰宿主源码、未碰用户运行实例、未 commit/push（用户未授权）。

5. **测试是否覆盖 Acceptance Criteria？**
   ✅ AC-1~AC-11 逐条有证据（见 Requirement Check）。新增 U1-U5 交互用例 + E1 增强 + 契约测试 5 条；15/15 e2e 两轮全绿。

6. **是否存在明显回归风险？**
   ⚠️ 低。下拉文案改动影响 U1/U2 断言已由八跑回归确认（15/15）；批量接口为新增端点（契约 5/5）；列表轮询仅对已委派任务启动（未委派不轮询，U5 断言无徽标）。风险见 Risk 段。

7. **是否存在架构不一致？**
   ✅ 无。批量端点沿用既有 gateway 单查路径（Task.WhenAll 并行），未新依赖；前端沿用既有 store/actions 分层；导航桥沿用既有 `forgeOpenPage` 注入模式。

8. **Evidence 是否足以证明任务完成？**
   ✅ 五类证据齐备：后端编译 0 err + 契约 5/5 + 定向 238/238（Verified）；宿主 check 0 err + vitest 742/742（Verified）；插件 build ×2（Verified）；todo e2e 15/15 ×2（Verified）；走查隔离实例 10 项清单逐项核对 + 截图存档（Verified）。

## Requirement Check

PASS（AC 逐条对照，来源等级见 05-evidence）

| AC | 判据 | 证据 |
|----|------|------|
| AC-1 | 新建项目下拉列项目（含地址与任务数）+ 创建带 projectId | ✅ U1 e2e + 走查（下拉「OpenForgeSelf · …OpenForgeSelf（1 任务）」+ title 地址） |
| AC-2 | 详情选择项目显示名+完整地址、落库一致、输入路径可用 | ✅ U2 e2e + 走查（.td-proj-line/.td-mono + 后端一致） |
| AC-3 | 委派四态可见文案（非 title） | ✅ U3 e2e + 走查（delegate-hint 常驻） |
| AC-4 | agents 空态引导跳 /agent-hub | ✅ U4 e2e + 走查（点击后 URL=/agent-hub） |
| AC-5 | 列表徽标 + taskKey 短显 + 阶段兜底 + 未委派无徽标 | ✅ E1/U5 e2e |
| AC-6 | 15s 轮询刷新徽标 + 详情 agent 名 + 终态停止 | ✅ E1 e2e |
| AC-7 | 记录成功后「查看执行记录」引导 + 滚动 | ✅ E1 e2e（goRecords + timeline 可见） |
| AC-8 | 后端定向测试绿（契约 + AgentName） | ✅ 5/5 + 238/238（Verified） |
| AC-9 | 宿主 check/vitest + 插件 build + todo e2e 全绿 | ✅ 742/742 + build×2 + 15/15×2（Verified） |
| AC-10 | 走查隔离实例点一遍 + 截图读图 | ✅ live-7201 截图 + 清单 10 项（Verified） |
| AC-11 | 流程落盘（tpl/workflow/plugin-development/AGENTS/本批 spec 交互节） | ✅ 五处全部落盘（Verified） |

## Scope Check

PASS（两处超界已记录并论证，见八问 3）

## Test Check

PASS（AC 全覆盖 + 回归：八跑 15/15 确认下拉修复无回归；e2e 基建修复经五跑/七跑/八跑连续验证）

## Architecture Check

PASS（见八问 7）

## Risk

L1（低）

- 隔离实例无真实 agent，agents 空态分支走查复现；真实委派终态依赖 e2e E1（opencode 真实进程，终态 Failed(timeout) 系外部模型端点 400，非链路缺陷）
- FR-1.4 下拉空态（无档案）未在走查复现（代码路径与「不选项目」并列，低风险）
- 窄屏布局未在走查复现（既有 V1 e2e 覆盖响应式）
- 本批改动未打包发布（下一步），:51888 运行实例尚未包含本批 UX 改动

## Findings

### Critical

无

### Major

无

### Minor

- FR-1.1 label 格式规格/实现偏差：已同步 02-spec 更新（实现更优：地址出括号 + title 承载完整地址）
- 下拉文案一致性（新建 vs 详情）走查发现并修复，e2e 八跑回归确认
- FR-1.4 下拉空态未在走查复现（隔离实例有档案）；建议后续走查补此分支或由 U1 在干净环境覆盖

## Final Decision

**APPROVED**

（依据：AC-1~11 全部有 Verified 证据；两轮 e2e 15/15；走查 10 项清单全过；偏差均已记录或修复。下一步：打包发布本批 UX 改动 + :51888 运行实例只读复验。）
