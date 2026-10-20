# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 审查人：本会话（与实现同一 agent，按 §0 出口清单自审 + CodeReview 复核）；结论只报事实。

## 审查八问（逐项回答）

**1. 实现是否真正满足 Intent？**
满足。Intent 三条用户诉求逐条对上现场证据：① 三模式（跟随系统/手动配置/直连）radio 已渲染并可切换（e2e ①③ + 走查 A1/A2/A5）；② 手动模式出现地址表单 + 「测试」按钮，点击后有真实反馈且判据=收到任何 HTTP 响应即代理可用（现场 `HTTP 200 · 789ms` / `积极拒绝` 双向验证）；③ 「不走代理的条目」一行一条可增可删、system 模式也显示并生效（走查 A6/A7 + AC-6 的 D1/D2/D3 翻转）。

**2. 实现是否符合 Spec？**
符合。BR-1 决策顺序（回环/旁路 → direct → manual → system）与代码 `ModelProxy.GetProxy` 逐分支一致；BR-4「测试零配置副作用」由 `SettingsControllerTestProxyTests` 的前后元组相等断言 + e2e ④ 的 `proxyMode` 仍为 system 双重覆盖；BR-5 前后端同规则（域名/IPv4、1-65535、旁路字符集/128 字符/50 条）且后端 400 带行号；FR-5 契约移除 `proxyEnabled` 有负面断言把守（`TryGetProperty("proxyEnabled").Should().BeFalse()`）。
一处**超出原 Spec 描述的实现细节**：`TryBuildProxyUri`（Regex 放行的边界串在 `new Uri` 处仍可能抛）——Spec 只写「无效则 Warn 兜底直连」，实现把它落到具体 try/catch，属**符合 BR 意图的必要补强**，非偏离。

**3. 是否超出了 Scope？**
基本未超。本批额外做了 3 项 UI 修复（控件定宽、错误左对齐、placeholder 缩短），属 Spec 的 AC-UX 视觉对齐范畴（走查发现即本批责任），不算扩界；但**在同一批里改了 e2e 选择器**，需在提交说明中点明。
未做（明确边界）：不改宿主 FAB/toast/滚动行为、不动 SOCKS5、不做配置迁移、不提交不发布。

**4. 是否修改了不应该修改的文件？**
无越界。改动集中在网络域 3 个后端文件 + 3 个测试文件 + 5 个前端文件；`ForgeSetting.cs` 只动代理字段。
**风险提示（非本会话造成）**：工作区同时存在并行会话的未提交改动（AIModel 元数据、ModelProxy 早期修复等），提交时**必须按文件精确 stage**，禁止 `git add -A`。

**5. 测试是否覆盖 Acceptance Criteria？**
覆盖 AC-1~AC-9：AC-1/2/3（三模式与旁路矩阵）→ ModelProxyTests；AC-4（契约/400 行号/密码语义）→ SettingsControllerNetworkTests；AC-5（test-proxy 语义）→ SettingsControllerTestProxyTests；AC-6（旁路闭环）→ **现场 Verified**（非仅单测）；AC-7（门禁）→ build/check/vitest/e2e 全绿；AC-8（e2e 四用例）→ 4/4 两次；AC-9（前端面板行为）→ vitest 21 例。
缺口：AC-6 的「system 模式下我方旁路叠加生效」只有单测 + 逻辑推演，现场只测了 manual 模式叠加（system 模式的系统豁免与我方豁免叠加在同一 host 上不可分辨，因两者都直连）。判为**可接受**：BR-1 ① 在模式分支之前，与模式无关，且单测覆盖了 system + 我方旁路的组合。

**6. 是否存在明显回归风险？**
两处需盯：
- 契约移除 `proxyEnabled`：上一批未发布未提交 ⇒ 无存量用户配置；旧 XML 残留字段解析时被忽略、下次保存不再写出（Spec BC 已交代）。风险低。
- `ConfigureHttpClientDefaults` 把 `ModelProxy` 装到**所有** HttpClient 上（含插件与更新器）：默认值 `system` = 旧 .NET 原生行为，等效不变；`direct` 会让全部出站请求直连（含更新下载）——这是设计意图（界面文案已说明），但需在发布说明中显式提醒。风险中低。
- `ForgeSnapshot.Restore` 的落盘重试只是**缓解**并发句柄争抢，未消除根因（多个测试类共享同一个全局 `ForgeSetting.config`）。见 Minor-2。

**7. 是否存在架构不一致？**
无。分层沿用 Controllers/Services；代理决策仍集中在 `ModelProxy`（唯一 IWebProxy 实现），未旁落到控制器；前端沿用 `settingsApi` + 面板组件 + `--el-*`/Tailwind 样式体系。
一处**惯例纠偏**：EP 组件定宽必须用内联 `style` 而非 Tailwind class（本批踩到并已按项目惯例 `UpdatePanel.vue:265` 改正）——该规则应沉淀（见 Minor-1）。

**8. Evidence 是否足以证明任务完成？**
足以证明**功能与验收**完成（全部标 Verified 且有原文输出/截图/几何数值）。不足以证明的部分也已如实标注：修复后宽视口截图未产出、system 模式叠加仅单测覆盖、全量后端测试存在 30 例非本域基线红。

## Requirement Check

**PASS** — AC-1~AC-9 全部有对应测试或现场证据；Unknown① 已由现场钉死（GetSystemWebProxy 应用系统 ProxyOverride）。

## Scope Check

**PASS** — 未触碰用户运行实例（:51888）、未提交、未发布、未停启任何宿主进程；额外 3 项 UI 修复属 AC-UX 范围并已记录。

## Test Check

**PASS（本域）/ 有条件** — 本域后端 74/74、前端 995/995、e2e 4/4 两次；全量后端存在 30 例非本域基线红（逐条归因 + 已登记 TODO），不构成对本批的否定，但**提交前的 CI 若跑全量仍会红**，需在闸门2 说明。

## Architecture Check

**PASS** — 决策单点（ModelProxy）、契约单点（SettingsController）、样式与组件惯例一致；`direct` 全局影响的语义已在界面与文档交代。

## Risk

**L2**（公共 API 契约变更 + 影响全部出站 HTTP 的默认代理装配；但默认值等效旧行为，且有 e2e/现场双向证据）

## Findings

### Critical

无。

### Major

无。

### Minor

1. **EP 组件宽度不能用 Tailwind class**（本批实证踩坑）：`class="w-[240px]"` 会被 Element Plus 自身 `width` 规则覆盖（同特异性、EP 样式后加载），导致同列控件宽度失控且 placeholder 被裁。项目惯例是内联 `style="width: …"`。→ 应回写 `docs/04-standards/agent-workflow/README.md` Part B 与 `ui-ux-design` 走查清单，避免下次重踩。
2. **`ForgeSnapshot.Restore` 只缓解未根治**：网络域测试与并发集合争抢同一全局 `ForgeSetting.config`。真修方向=该域测试改用独立 `FORGE_SETTING_CONFIG` 路径或整域串行化（现靠 `[Collection("SharedGlobalState")]` + 退避重试）。已可复现地红过一次（全量并发时），属不稳定测试源。
3. **视觉证据形式降级**：修复后宽视口截图因内嵌浏览器视图不可用而未产出，改以几何实测 + e2e 截图支撑。若闸门2 要求肉眼复核，需在有浏览器的环境补图。
4. **`direct` 模式的全局影响面**：`ConfigureHttpClientDefaults` 使更新下载/插件出站同样直连。行为符合"直连·不走代理"字面语义，但对使用公司代理的用户是"选了直连就什么都连不上外网"——界面文案已提示，建议在功能文档 042 显式写一句。

## Final Decision

**APPROVED**（进入闸门2：由用户批准提交；本批未提交、未发布）

前置交代事项（须在提交说明中写明）：① 全量后端 30 例非本域基线红已登记 TODO，不属本批修复；② 提交须按文件精确 stage（工作区存在并行会话改动）；③ 发布（闸门3）默认走本地目录更新源，且宿主更新由用户在页面点，agent 不停启任何进程。
