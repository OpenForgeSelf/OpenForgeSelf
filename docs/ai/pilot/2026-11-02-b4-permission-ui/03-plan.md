# Plan

> 阶段：Stage 3｜具体到真实文件路径
> Task ID：PILOT-B4-UI

## Files To Change

- file: `ForgeSelf.Web/src/services/permissionPolicyApi.ts`（新）
  reason: API 层——profiles CRUD + audit 查询；线上格式差异（数值枚举、裸封套、409/503 原文）集中映射。
- file: `ForgeSelf.Web/src/components/settings/PermissionPolicyPanel.vue`（新）
  reason: 权限策略面板（segmented 双视图：策略配置卡片/规则矩阵/白名单编辑 + 审计挂载 + 内置默认说明卡）。
- file: `ForgeSelf.Web/src/components/settings/PermissionAuditView.vue`（新）
  reason: 审计流水只读视图（过滤 + 分页 + 五态）。
- file: `ForgeSelf.Web/src/views/SettingsView.vue`（改）
  reason: 分类注册（联合类型/pageHeadMap/navItems/v-show 挂载）+ `?cat=` 深链（onMounted 读一次，防御无 router 环境）。
- file: `ForgeSelf.Web/src/data/features.ts`（改）
  reason: 「权限策略」功能入口（path=`/settings?cat=permission-policy`）。
- file: `ForgeSelf.Web/src/services/__tests__/permissionPolicyApi.test.ts`（新）
  reason: API 层单测（路径拼装/数值枚举映射/错误原文/参数钳位/封套兜底）。

## Implementation Steps

1. API 层：类型（PermissionProfile/ToolPermissionRule/Audit）+ fetch 封装 + effect 数值↔字面量映射 + 503/409 语义。
2. 审计视图组件（独立，避免面板超体量）。
3. 策略面板：草稿/脏标记 fingerprint、规则矩阵、规则级白名单（三组 tag）、Deny 破坏性确认、新建/保存/删除流。
4. SettingsView 注册 + features.ts 条目 + 深链。
5. 单测 → 快档三门禁 → dev-stack 走查（Playwright MCP 截图读图）→ 工件收口。

## Test Plan

1. `permissionPolicyApi.test.ts`：14 条（列表解析/空数组/503 文案/409 原文/PUT 路径与数值 effect/DELETE 幂等/审计参数钳位/封套缺失兜底）。
2. 既有测试回归：`SettingsView.test.ts` 等 92 文件全量（vitest）。
3. 真实走查：Playwright MCP 按交互规格 12 行逐点操作 + 截图 6 张读图。

## Verification

### Build

```bash
cd ForgeSelf.Web && pnpm run check
```

### Unit Test

```bash
cd ForgeSelf.Web && pnpm run test
```

### Integration Test

N/A——本批无 e2e 配置变更；走查以真实 dev-stack + 真实后端端点（GET/PUT/DELETE 实测 200/409）代替，属 §5.6 快档口径。

### E2E

N/A——快档不跑全量 e2e（未触 global-setup/playwright config/共享夹具，触发条件不满足）。

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run design:check
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 23:24 | U6：PUT 新建路径 405 | 假设 PUT /profiles（无 id 段）新建 | 控制器路由实为 `PUT profiles/{id:long}` 必填，id=0 新建；API 层已改并锚单测 |
| 23:26 | U2：PUT body 400（effect 字符串不可转换） | 假设线上契约 = 字符串字面量 | 宿主 MVC 无全局 JsonStringEnumConverter，线上 = 数值枚举（Allow=0/Ask=1/Deny=2）；API 层集中双向映射 |
| 23:26 | 走查截图目录名 | permission-20261102 | 按 lead 指令改名 b4-permission-20261102 |
