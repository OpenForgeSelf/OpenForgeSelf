# 走查问题 → 开发规范回流路由

走查问题的**默认动作**：能当场小改则修；须写进纪律的，按下表改**唯一真源**（禁止只在 SKILL 里复述而不改规范）。

| 问题类型 | 典型症状 | 回流文档（优先级） | 代码/测试守卫 |
|----------|----------|-------------------|---------------|
| 元数据必填漂移 | `required:true` 无星号、前端不校验 | `docs/constitution/testing.md` §6 | 控制器 `AddFormFields` 设 `Required`+`Nullable=false`；可选 meta 集成测 |
| 前后端字段裁剪不一致 | 表单仍出现已废弃字段 | 对应 REQ/DEC + `WorkOrderController` AddFormFields | AC 自动化 HTTP/UI |
| 启动/种子竞态 | 首启崩溃、缺列 | `docs/constitution/architecture.md` §组合根 | `MembershipSchemaReadyGate`；启动日志 |
| 术语/占位文案 | 「生效区域」未改「板块」 | `docs/requirements/glossary.md`、FEAT 正文 | 前端 placeholder/label |
| 摘要/列表展示码值 | `板块#5` | CON-产品对外材料 + 页面 Lov 翻译 | 复用 `ensureLovLabels` / `translateCodes` |
| 仅自动化可复现 | click 不触发 Vue | 本技能 `automation-blockers.md` | 可选 `data-test` + e2e |
| 新拒绝对/新业务规则 | 新 code/message | REQ/AC + DES API | `[Trait("AC",…)]` 单测/集成 |
| 契约/枚举 | OpenAPI 与 UI 不一致 | `docs/constitution/contracts.md` | `pnpm api:build` + gen:api |

**合并纪律**：同一根因只回流一处；跨层冲突按 宪法 > 需求 > 设计 > 代码。

**DEC 留痕**：改 `docs/constitution/*` 行为面时，在 `docs/decisions/DECISIONS.md` 追加简短 DEC（或由用户授权后 Agent 追加）。
