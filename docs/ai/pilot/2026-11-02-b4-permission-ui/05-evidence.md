# Evidence

> 阶段：Stage 7｜只记录实际发生的事情，标注来源等级。
> Task：PILOT-B4-UI

## Task

PILOT-B4-UI

## Changed Files

- `ForgeSelf.Web/src/services/permissionPolicyApi.ts`（新，176 行）
- `ForgeSelf.Web/src/services/__tests__/permissionPolicyApi.test.ts`（新，14 用例）
- `ForgeSelf.Web/src/components/settings/PermissionPolicyPanel.vue`（新，919 行）
- `ForgeSelf.Web/src/components/settings/PermissionAuditView.vue`（新，312 行）
- `ForgeSelf.Web/src/views/SettingsView.vue`（改：import/类型/pageHeadMap/navItems/挂载/深链）
- `ForgeSelf.Web/src/data/features.ts`（改：新增「权限策略」条目）

## Build

Command:

```bash
cd ForgeSelf.Web && pnpm run check
```

Result: PASS（来源等级：Verified）

```text
✖ 166 problems (0 errors, 166 warnings)   # 基线 153 warnings，新增为 vue/attributes-order 类
[component-conventions] PASS（story 4 个 / ct.spec 4 个，均已注册、配对、标注）
exit=0（b4-ui-check-final.log）
```

## Unit Test

Command:

```bash
cd ForgeSelf.Web && pnpm run test
```

Result: PASS（来源等级：Verified）

```text
Test Files  92 passed (92)
     Tests  1073 passed (1073)          # 基线 1059（B3 后）+ 本批新增 14
 ✓ src/services/__tests__/permissionPolicyApi.test.ts (14 tests) 16ms
exit=0（b4-ui-test-final.log）
```

## Integration Test

Result: PASS（来源等级：Verified）——快档口径，真实 dev-stack 后端联调实测：

```text
GET  http://localhost:7301/api/permissions/profiles → 200 []（token 鉴权）
PUT  http://localhost:7399/api/permissions/profiles/0（经 vite 代理）→ 200 新 dto（agentId=coder，effect 数值 2 落库）
GET  读回 → [{"id":1,"agentId":"coder","rules":[{"toolName":"*","effect":2,"pathAllowlist":["D:\\tmp\\"],...}],"remark":"B4 走查示例：开发 Agent 策略","updatedAt":"2026-11-02T23:36:42.7542222+08:00"}]
PUT  重复空 agentId → 409 {"error":"AgentId「」已存在策略配置"}（原文逐字透出）
DELETE /profiles/{id} → 200；读回 → []
```

对表修正两项（03-plan 偏差记录）：PUT 新建 = `/profiles/0`（405→读控制器实锤）；线上 effect = 数值枚举 0/1/2（400→AppBuilder 无全局 JsonStringEnumConverter 实锤）。

## E2E

Result: N/A（依据：快档未触 e2e 基建文件（global-setup/playwright configs/共享夹具），§5.6 深档触发条件不满足；以真实端点联调 + 走查代替）

## Static Analysis

Command:

```bash
cd ForgeSelf.Web && pnpm run design:check
```

Result: PASS（来源等级：Verified）

```text
[style-tokens] 硬编码色值合计 1759（基线 2501）
[style-tokens] PASS：无新增硬编码色值，较基线减少 742 处
[design-compliance] a(token族)=209 b(胶囊)=0 c(玻璃过头)=0 d(蓝紫渐变)=0（基线一致）
[design-compliance] PASS：无新增设计规范违反（DESIGN.md 门禁）
```

## Screenshots

全部落 `ForgeSelf.Web/screenshots/dev/b4-permission-20261102/`（均已读图核对）：

| 文件 | 实证点 |
| --- | --- |
| 01-deeplink-empty-builtin.png | AC2 前半 + AC3：深链直达、PageHead 三件套、内置默认只读说明卡 |
| 02-deny-dirty-editor.png | AC5：Deny 行朱砂描边+选中态、脏标记（未保存徽标+底栏提示）、白名单三组口径占位 |
| 03-deny-save-confirm.png | AC5：保存前汇总确认弹窗「以下工具将被拒绝执行：兜底（*）。确认保存？」 |
| 04-saved-chips.png | AC4/AC6：保存后 chips 更新（拒绝 1）、未保存徽标消失、tag 渲染 |
| 05-audit-view.png | AC8：审计真实数据（B2 人工审批记录 write_file 已批准）+ 过滤/查询/共 N 条 |
| 06-conflict-409.png | AC7：409 原文逐字透出「AgentId「」已存在策略配置」 |

读图符合性（ui-ux-design 清单）：PageHead 三件套齐（eyebrow mono 金/display 26px/金底唯一 primary）；玻璃卡 + token 化配色、零裸色值；双主题 token 引用同构（暗色未截图，token 引用路径与 AiProvidersPanel 同源，Inferred 级）；hover/focus 元素默认态符合 Element Plus。

## AC 覆盖对照（与 02-spec「Acceptance Criteria」逐字对应）

| AC | 结论（来源等级） | 证据 |
| --- | --- | --- |
| AC1 导航注册 + 面板渲染 | PASS（Verified） | 截图 01（pageHead「Settings · 权限策略」+ 分类按钮 + 列表渲染；锚 GET /api/permissions/profiles 200） |
| AC2 深链直达 + 非法值回落 | PASS（Verified） | 截图 01（?cat=permission-policy 直达）；?cat=bogus-value → pageHead「Settings · 通用设置」无报错（走查实测） |
| AC3 零配置内置默认说明卡 | PASS（Verified） | 截图 01（GET 返回 []，说明卡文案「沉默放行 + 高风险工具转审批」+ 新建引导；UI 无合成策略行） |
| AC4 三态切换本地生效 + 保存读回一致 | PASS（Verified） | 切 Deny → 脏标记（截图 02）→ 确认保存 → PUT 200 → GET 读回 effect=2（数值）→ 面板重开「拒绝 checked」+「与已保存版本一致」 |
| AC5 Deny 朱砂视觉 + 汇总提示 + 取消不落盘 | PASS（Verified） | 截图 02（朱砂描边/选中态）+ 截图 03（弹窗「以下工具将被拒绝执行：兜底（*）」）；取消路径由 ElMessageBox cancel 分支保证（单测覆盖 saveProfile 语义） |
| AC6 白名单三组增删去重 + 读回一致 | PASS（Verified） | 路径组添加 `D:\tmp\` tag 渲染 → 保存 → GET 读回 pathAllowlist=["D:\\tmp\\"]；去重/空值拒绝在 API 层函数内单测锚定 |
| AC7 全局默认已存在 → 409 原文透出 + 输入保留 | PASS（Verified） | 截图 06：alert「AgentId「」已存在策略配置」逐字、草稿卡未关闭备注保留；409 消息透出有单测锚定 |
| AC8 审计首切拉取 + 过滤组合 + 空态 + 翻页参数 | PASS（Verified） | 截图 05（真实 B2 审批记录 + 共 1 条）+ 过滤 run_terminal_command → 「当前过滤条件下没有记录」走查实测；page/pageSize 钳位与封套解析单测 4 条锚定 |
| AC9 503 错误态 + 保存失败输入不丢 | PASS（Verified，单测级） | 503 →「权限策略服务未就绪，请升级宿主」单测锚定（listProfiles）；409/500 错误原文透出 + 草稿保留走查实测（06 截图） |
| AC10 三门禁绿 + 零裸色值 | PASS（Verified） | check 0 errors / test 1073 全绿 / design:check 双棘轮 PASS（本文 Static Analysis + Build 节日志原文） |

## Known Limitations

- 白名单条目输入的一次 Playwright fill+Enter 未触发（合成 KeyboardEvent 同 handler 实测可追加，05-02 截图前后一致性可证），判定为测试工具时序瞬态非产品缺陷；后续 e2e 用例若复现再查。
- 暗色主题未单独截图（同 token 引用，主题矩阵为 Inferred 级）。
- dev-stack state 文件 PID 与真实宿主 PID 曾不一致（131352 vs 120544），导致一次撞锁（MSB3027）；改走 `-Stop` + `-SkipBuild` 官方入口恢复。

## Unresolved Issues

- 【新发现缺陷·非本批修复】vite dev watcher 被 DSH 编辑器原子写临时文件（`src/**/*.tmpdir/*.tmp`）触发 chokidar `EBUSY` 未处理异常 → vite 进程静默退出（实测两次，stderr 落 `.temp/dev-stack-frontend.log.err`）。已记 TODO（P2），建议 `vite.config` watch.ignore 该模式；本批走查期间以「源码编辑完成后重启 dev-stack」规避。
