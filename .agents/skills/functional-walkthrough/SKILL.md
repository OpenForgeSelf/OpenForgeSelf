---
name: functional-walkthrough
description: >-
  MindHub 项目级「功能走查」技能：读 REQ/AC/DEC/TASK → 起前后端 → 浏览器实点与接口证据 → 按走查清单逐项填核对表写入报告。
  汇总阻塞项与自动化坑；问题按类型回流开发规范。用户说「功能走查 / 走查一遍 / 浏览器验收」时使用。
version: 1.0.0
---

# 功能走查（MindHub · 浏览器 + 接口实证的验收走查）

> **依据**：`AGENTS.md` 证据口径 / 任务完成汇报 · `docs/constitution/standard.md` F2 · `docs/constitution/testing.md` · [references/norm-feedback-routing.md](references/norm-feedback-routing.md)。
> **与** `guide-screenshot-audit` **分工**：本技能验**真实环境行为**（点、调、看响应）；截图核技能验**已落盘手册**是否自证。

## 何时触发

- 用户说「功能走查」「走查一遍」「控制浏览器验收」「按 DEC/REQ 验一下」。
- 功能已落地或待发布，需要**一份带清单的走查报告**（免反复问「查了啥、有啥问题」）。

## 开工前（只读）

1. `AGENTS.md`、`CONTEXT.md`（若有）、`docs/planning/CURRENT.md`。
2. 本次真源：**FEAT / REQ / AC / DEC / TASK**（用户指定或从 CURRENT/TODO 推断）。
3. 读本技能 [references/checklist-template.md](references/checklist-template.md)，按范围复制一份**本次走查清单**（可先占位项，再在报告里填结果）。
4. 向用户确认是否：**启动服务 / 跑测试 / 写共享库**（默认：共享测试库写操作须汇报）。

## 环境与服务

| 组件 | 默认命令 | 地址 |
|---|---|---|
| 后端 | `pnpm api:dev` 或 `dotnet run --project src/MindHub.Api` | `http://localhost:9201` |
| 前端 | `pnpm web:dev` | `http://localhost:9280` |
| API 代理 | `src/frontend/.env.development.local`（不入库） | `VITE_APP_BASE_API=http://localhost:9201` |

**纪律**

- 9201 被旧进程占用时**先停再启**，勿用改前构建冒充本版。
- 登录：Cube 登录页；种子账号见 `scripts/h5-sdk/README.md`（**报告里禁复述密码**）。
-  stale `refresh_token` → 401 工作台：清 `localStorage` 中 `refresh_token` 等 Cube 键后重登。
- 接口成功：**HTTP 2xx 且 `code==0` 且 JSON**（本系统信封）。

## 浏览器自动化工作流（cursor-ide-browser）

```
browser_tabs(list) → browser_navigate → browser_lock → 操作 → browser_unlock（收尾必做）
```

**Vue / Element Plus 红线**（实测 2026-11-10）

| 现象 | 处理 |
|---|---|
| `browser_click` 不打开弹窗/不进编辑 | `browser_cdp` `Runtime.evaluate` 遍历 `#app` 子树找 `setupState.openCreate` / `openEdit` 等 |
| 密码 `browser_fill` 长度为 0 | `HTMLInputElement` 原型 `value` setter + `InputEvent('input')` + `requestSubmit()` |
| `fetch` 钩子抓不到 POST | Cube 用 XHR：挂 `XMLHttpRequest.prototype.open/send` 或看 Network（CDP） |
| 工单详情 40401 | 台账点「查看」→ **侧拉抽屉**，勿猜 `Detail2` URL |
| Loading 壳 | 等 `redirect=` 跳完再 snapshot |

长操作：`browser_lock` 期间用户可 Take Control；结束必须 `browser_lock` `action: unlock`。

## 走查执行顺序（默认）

1. **元数据 / 表单**：`GET …/meta` add 桶（或无 UI 时读控制器 `AddFormFields`）— 字段集、必填、nullable。
2. **正向路径**：按 AC 填最少必填 → 提交 → 记录请求体（仅应有字段）与响应 `code/message/traceId/data`。
3. **负向路径**：AC 列出的每条拒绝场景各点一次（空 rule、不存在、无处理人…）。
4. **列表 / 详情**：新行是否出现；详情字段、状态、时间线（分派异步时以详情为准）。
5. **关联页**：DEC 点名的配置页（如规则编辑：板块单选、来源单选、占位文案）。
6. **关键步骤** `browser_take_screenshot`（不代替接口证据）。

抓包：优先在 UI 提交；必要时经 Vue `submitForm()` 并登记「与真人点击路径差异」。

## 报告（必须用模板，清单逐项有结论）

照 [references/report-template.md](references/report-template.md) 输出，**第二节必须是走查清单核对表**（每行：项 ID | 预期 | 结论 通过/失败/待核实 | 证据）。

并含：`AGENTS.md` 任务完成汇报字段（任务/状态/操作/结果/验证/风险/证据）；**自动化受阻表**（凡拦过你的点）；**问题清单**（P0–P3）。

结论四档：已核实 / 待核实（写解锁条件）/ 未知 / 已被推翻。

## 问题回流开发规范（与修代码同批或下一批，禁止只写报告不修规范）

走查结束后，对**可沉淀为纪律**的问题，按 [references/norm-feedback-routing.md](references/norm-feedback-routing.md) 更新对应文档（先文档后代码若触及行为契约）：

| 问题类型 | 回流目标 |
|---|---|
| Cube 表单 `required` 与 `nullable` 不一致 | `docs/constitution/testing.md` §6 + `AGENTS.md` 技术组件（元数据与前端星号） |
| Api 种子与 XCode 迁移竞态 | `MembershipSchemaReadyGate` + `docs/constitution/architecture.md` 启动顺序指针 |
| 文案/术语与 REQ 不一致 | `docs/requirements/glossary.md` 或对应 FEAT |
| 浏览器 E2E/走查可重复性 | 本技能「Vue 红线」表；可选补 `data-test` |
| 新验收维度 | FEAT 增 AC + `scripts/trace.config.json` + 单测 `[Trait("AC",…)]` |

**回流完成标准**：规范中有可检索的一句纪律 + 代码或测试有守卫/用例；走查报告「规范回流」节列文件路径。

## 走查后自检

- [ ] 清单是否在操作前就从模板复制（不是事后只写失败的）？
- [ ] 每项是否有可验证证据（响应 JSON / 抽屉文案 / 截图路径）？
- [ ] 是否区分「产品缺陷」「环境/数据」「自动化限制」？
- [ ] 写库是否明示单号/环境？
- [ ] 浏览器已 unlock？
- [ ] 问题是否已路由到 norm-feedback 表中的文档？

## 附加资源

- [references/checklist-template.md](references/checklist-template.md) — 通用清单 + WO-178 样例
- [references/report-template.md](references/report-template.md) — 报告骨架
- [references/norm-feedback-routing.md](references/norm-feedback-routing.md) — 问题类型 → 规范路径
- [references/automation-blockers.md](references/automation-blockers.md) — 历史阻塞明细（2026-11-10 建单规则）
