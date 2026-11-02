# Specification

> 阶段：Stage 2｜战役批 B4 · UI 主攻（taskcenter-dev）。后端契约已由 verify-dev 冻结并知会（2026-11-02 22:5x，`ForgeSelf.Abstractions/PermissionProfiles.cs`，camelCase JSON；全套单测 21/21 绿）。本 Spec v1.1 按冻结契约修订；契约前版本的关键假设差异（小写字面量/名称字段/种子行/三组 profile 级白名单）已全部纠正。
> Task ID：PILOT-B4-UI

## Functional Requirements

1. 设置页新增「权限策略」分类面板 `PermissionPolicyPanel.vue`（`ForgeSelf.Web/src/components/settings/`），注册进 `SettingsView.vue`（分类键 `permission-policy`）。
2. 面板内双视图（segmented 切换）：**策略配置**（默认）与 **审计日志**。
3. 策略配置：
   - 策略（PermissionProfile）卡片列表：身份 = `agentId`（空串 = 全局默认，非空 = 按 Agent；全局默认至多一张，重复 → 后端 409）；头部展示 agentId（空则「全局默认」）、`enabled` 开关、规则统计 chip（Allow/Ask/Deny 计数）、`remark`；展开编辑。
   - 规则矩阵（profile 内 `rules`）：逐规则行 = `toolName`（`*` = 兜底规则行，固定存在可编辑 effect）× `effect` 三态 segmented（`Allow` 放行 / `Ask` 转审批 / `Deny` 拒绝）；可添加/移除规则行（工具名搜索选择，降级手动输入）。
   - 白名单编辑**在规则行内**（契约：`ToolPermissionRule.pathAllowlist/domainAllowlist/commandAllowlist`）：每行展开「白名单」子区，三组条目 tag 增删（路径=前缀、命令=前缀、域名=相等或子域——口径后端解释，UI 不本地解释）。
   - 显式保存：编辑仅本地态，点「保存策略」→ `PUT /api/permissions/profiles/{id}`（id>0；新建走 PUT 无 id 段，语义见 Unknown U6）；409 → 行内展示后端错误原文；dirty 脏标记 + 切换卡片未保存确认。
   - 删除策略：非展开中卡片「删除」→ 确认弹窗（文案注明「删除后该 Agent 回落内置默认策略」）→ `DELETE /api/permissions/profiles/{id}`。
4. 内置默认说明卡：策略列表为空或无全局 profile 时，固定渲染一张只读「内置默认」说明卡（不可编辑不可删）：沉默放行 + 高风险工具经 MAF 包装转审批 + 白名单未命中时浏览器类拒绝/文件终端转审批/参数解析失败转审批（fail-safe）——与后端决策语义逐字一致（verify-dev 契约）。
5. 审计日志视图：`GET /api/permissions/audit` 流水表（time / agentId / toolName / kind / status / source / reason），按 `toolName`、`kind`（policy-decision|tool-approval）过滤 + 刷新 + 后端分页（裸封套 `{items,total,page,pageSize}`）；`argsHash` 展示为 mono 摘要（不落原始参数，UI 不伪造）。
6. `features.ts` 新增「权限策略」功能入口（指向 `/settings?cat=permission-policy`），`SettingsView.vue` 支持 `?cat=` 深链（onMounted 读取一次）。

## Input

- **后端契约（冻结）**：`PermissionProfileDto{id,agentId,enabled,rules,remark?,updatedAt}`；`ToolPermissionRule{toolName("*"=兜底),effect:"Allow"|"Ask"|"Deny",pathAllowlist[],domainAllowlist[],commandAllowlist[]}`；`PermissionAuditDto{id,kind:"policy-decision"|"tool-approval",time,sessionId,agentId,toolName,argsHash,status(策略=Allow/Ask/Deny；审批=Approved/Denied/Expired/Timeout),source("default-policy"|"profile:{id}"|风险级),reason,approvalId?}`。
- **端点**：GET `/api/permissions/profiles` → 裸数组（服务缺席 503）；PUT `/api/permissions/profiles/{id}?` → 200 新 dto / 409 `{"error":"AgentId「X」已存在策略配置"}`（id>0 不存在 = upsert 新建）；DELETE `/api/permissions/profiles/{id}` → 200 `{"deleted":true}` / 404；GET `/api/permissions/audit?toolName&sessionId&kind&from&to&page=1&pageSize=50(1..200)` → `{items,total,page,pageSize}`（非法 kind/pageSize 400）。
- 既有范式：`AiProvidersPanel.vue`、`SettingsView.vue`；token 头沿用 `getStoredToken`。

## Output

- `ForgeSelf.Web/src/components/settings/PermissionPolicyPanel.vue`（新）
- `ForgeSelf.Web/src/components/settings/PermissionAuditView.vue`（新，审计视图独立组件）
- `ForgeSelf.Web/src/services/permissionPolicyApi.ts`（新，API 层；契约对齐 verify-dev 冻结版）
- `ForgeSelf.Web/src/services/__tests__/permissionPolicyApi.test.ts`（新）
- `ForgeSelf.Web/src/views/SettingsView.vue`（改：分类注册 + `?cat=` 深链）
- `ForgeSelf.Web/src/data/features.ts`（改：新增条目）
- 工件：本目录 00-07（UI 侧）

## Business Rules

1. **兼容红线**：零配置（无 profile 行）时行为与现状等价由后端内置默认保障（沉默放行 + 高风险 MAF 包装转审批）；UI 不本地合成策略数据，以「内置默认」只读说明卡如实呈现兜底语义。
2. 决策三值封闭（契约字面量）：`"Allow"` / `"Ask"` / `"Deny"`（PascalCase）；审计 status 按 kind 分域（策略=Allow/Ask/Deny，审批=Approved/Denied/Expired/Timeout）；UI 不发明第四态，API 层集中映射。
3. `Deny` 为破坏性决策：行内即时生效仅本地；落盘前对「非 Deny → Deny」的规则行汇总提示「将拒绝对应工具执行」。
4. 审计为只读视图不提供任何改操作（与 B3 走查纪律一致）；`argsHash` 原样展示为 mono 摘要。
5. 白名单条目非空校验 + 去重；条目为字面量/通配串，语义（路径/命令=前缀、域名=相等或子域）由后端解释，UI 仅在占位文案中转述口径。
6. profile 身份 = `agentId`：全局默认（空串）至多一张（后端 409 兜底）；`enabled=false` 的 profile 不参与决策（后端语义），UI 在卡片头明示「已停用」。

## Boundary Conditions

- 策略数为 0（无自定义 profile）→ 内置默认说明卡 + 「新建策略」引导（不假造默认策略行）。
- 审计流水为 0 → 空态文案「尚无策略决策记录」。
- 工具清单接口不可用/不提供 → 规则行添加降级为手动输入工具名（保留能力，不白屏）。
- 单条策略超长（>50 规则行 / 单行白名单 >30 条）→ 编辑区内部限高滚动（DESIGN.md §新建页面 5）。
- 后端权限服务缺席（GET profiles 503）→ 面板整体错误态「权限策略服务未就绪，请升级宿主」+ 重试。

## Error Handling

- API 失败 → `el-alert` 错误条留痕（后端错误原文透出，409 的 `{"error":…}` 逐字展示）+ 重试入口；保存失败 → 表单内错误 + 不关编辑区（输入不丢）。
- 审计非法参数（kind/pageSize）→ 后端 400 原文入错误条；UI 发请求前先做本地参数钳位（page≥1、pageSize 1..200）。
- 深链 `?cat=` 值不存在 → 回落 `general`，不报错。

## Compatibility

- 兼容红线（契约原文）：默认匹配序 = Agent 精确 profile(Enabled) → 全局 profile(AgentId=空,Enabled) → 内置默认（沉默放行，高风险工具审批由 MAF 包装承担）→ 零配置行为不变；UI 仅渲染后端数据与只读说明，不合成策略。
- 不改动既有设置分类行为；`?cat=` 深链为纯增量。

## Non-functional Requirements

- token 全对齐（`--el-*` + 设计稿 token，禁裸色值）；主操作金底唯一；Deny 相关破坏性用 `--cinnabar-400`。
- 面板加载不阻塞设置页其它分类（v-show 惰性首载）。
- 策略保存成功 toast + 卡片原位刷新（不整页闪动）。

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 视图切换 | 点击 segmented「策略配置 / 审计日志」 | 右侧内容区切换两视图；审计首切时拉取第一页 | 两态互斥 | 切换无闪动（保持容器高度策略） | 各视图自有空态 | `?cat=` 深链可直达（含审计子态 `?cat=permission-policy&tab=audit`） |
| 策略卡片 | 点击卡片头部 | 展开/收起编辑区（风琴单开 expandedId） | 展开/收起 + 180ms 过渡 | 无 | 策略 0 条 → 内置默认说明卡 + 「新建策略」引导 | 展开态有未保存变更时点另一卡片 → ElMessageBox.confirm「放弃未保存变更？」 |
| 新建策略 | 点「新建策略」 | 行内新建草稿卡：agentId 输入（留空=全局默认，placeholder 说明）+ 默认一条 `*` 兜底规则（Ask） | 草稿态 | agentId 非空校验由后端 409 兜底，前端仅非空提示 | — | 全局默认已存在 → 前端预检提示「全局默认策略已存在」（仍允许提交，后端 409 逐字透出） |
| 工具决策行 | 点击行内三态 segmented | 该规则 effect 切为 Allow/Ask/Deny；本地生效（脏标记亮起） | 三值封闭 | 行尾「未保存」点标记 | 矩阵无规则行 → 引导「添加规则」（`*` 兜底行始终保留可编辑） | 切 Deny 时行变朱砂描边（破坏性视觉） |
| 添加规则 | 点「添加规则」+ 工具名输入（降级手动输入） | 矩阵末尾追加规则行，默认 Ask，白名单三组为空 | 规则集合 | toast「已添加」 | — | 重复 toolName → 行内红字「已存在」不追加 |
| 移除规则 | 点行尾移除 icon | 规则行消失（本地；`*` 兜底行不可移除仅可改 effect） | 规则集合 | toast | — | 该行有未保存改动 → 直接移除并在脏汇总中体现 |
| 规则白名单 | 规则行内「白名单」展开钮 → 三组输入回车/点添加 → tag 追加；点 tag × 移除 | pathAllowlist/domainAllowlist/commandAllowlist 各自增删 | 三列表 × 每规则 | 非空校验 + 去重提示 | 组空 → 占位「暂无条目（路径/命令=前缀，域名=含子域）」 | 条目为字面量/通配串，匹配语义后端解释 |
| 保存策略 | 点「保存策略」 | 全量保存该 profile → 成功 toast「策略已保存」+ 统计 chip 刷新；新建成功后卡片取得后端回传 id | dirty/saving/saved | saving 时按钮 loading | — | 失败（含 409 AgentId 冲突）：错误条逐字原文，输入保留 |
| 恢复内置默认 | （说明卡内无动作）删除某策略即回落内置默认：点「删除」→ ElMessageBox.confirm「删除后该 Agent 将回落内置默认策略（沉默放行+高风险转审批），确认？」→ 确认后 DELETE | 确认流 | toast「已删除，已回落内置默认」 | 删至 0 → 内置默认说明卡 + 新建引导 | 取消无副作用 |
| 删除策略 | 点卡片「删除」 | 同上（所有 profile 可删，含全局默认；删除即回落内置默认） | 确认流 | toast「已删除」 | — | 展开中有未保存变更 → 先弹「放弃未保存变更？」再走删除确认 |
| 审计过滤 | 选工具名输入 / kind 下拉（policy-decision / tool-approval） | 表格按条件重查第一页 | filter 状态 | 查询中骨架行 | 无匹配 → 「当前过滤条件下没有记录」 | 后端 400（非法 kind）→ 错误条原文 |
| 审计翻页/刷新 | 点页码 / 刷新按钮 | 拉取对应页/重查当前页 | page 状态 | loading 条 | — | 页码越界 → 回落最后有效页（后端语义，Unknown U4） |
| 审计入口（跨页） | 任务中心/其它页后续接入 | 本批仅设置页内入口；跨页入口不做（NOT-to-do） | — | — | — | — |

### 视觉规格（目标态 · 涉 UI 批必写）

- **对齐线与几何**：面板内容左缘 = settings-content-card padding 20px；矩阵行高 40px（`--el-border-radius-base` 行内控件）；三组白名单 tag 列表同一左缘；统计 chip 与卡片标题同行右对齐（CRAP 对齐线单一）。
- **主题矩阵**（改动面 × light/dark）：

| 表面 | bg | fg | border |
| --- | --- | --- | --- |
| 策略卡片 | `--el-bg-color`（透 settings glass） | `--el-text-color-primary` | `--el-border-color-lighter` |
| 矩阵行 segmented | `--el-fill-color-light` / 选中 `--gold-tint-strong` | `--el-text-color-regular` | `--el-border-color` |
| deny 行强调 | transparent | `--cinnabar-400` | `color-mix(in srgb, var(--cinnabar-400) 35%, transparent)` |
| 审计表 | 透 glass | `--el-text-color-regular`（时间/数值 mono） | `--el-border-color-lighter` |
| 空态/错误 | transparent | `--el-text-color-secondary` | — |

- **状态 × 主题**：五态（加载 el-skeleton / 空 EmptyState / 错误 el-alert 留痕 / 无权限=401 由全局 token 层拦截不进面板 / 正常）两主题同构，无差异格。
- **来源教义**：DESIGN.md §新建页面 1-7 + AiProvidersPanel 既有对齐教义；deny 朱砂 = 品牌语汇「破坏性用朱砂」。

### 交互验收标准（并入 Acceptance Criteria，逐条可测）

- 每交互点至少一条 AC 断言「触发后出现什么」，锚真实 API 层返回（vitest stub 网络层 + 走查真实后端）。
- 空态分级文案可测；禁用态可见原因；破坏性二次确认走 `ElMessageBox.confirm`。

### 走查符合性（DoD 绑定）

- 按 `ui-ux-design` 走查 UI 符合性全项留痕；长文本压力（60+ 字符工具名/失败原因）不破版；对比度改动面亮/暗实测进 05。

## Acceptance Criteria

- AC1: 设置页导航出现「权限策略」分类，点击后 pageHead 显示「Settings · 权限策略」且面板渲染策略列表（锚 `GET /api/permissions/profiles` 返回）。
- AC2: `?cat=permission-policy` 直达该分类；非法值回落通用设置且无报错。
- AC3: 零配置（profiles 空数组）时面板渲染「内置默认」只读说明卡（文案含「沉默放行 + 高风险工具转审批」）+「新建策略」引导；UI 不合成策略行（锚空数组返回）。
- AC4: 规则 effect 三态（Allow/Ask/Deny 字面量）切换仅本地生效并亮脏标记；点「保存策略」PUT 后重查读回与所切一致（锚 PUT 200 返回 dto + 重 GET）。
- AC5: 切 Deny 的规则行呈现朱砂视觉；保存前汇总提示含「将拒绝执行」文案；取消保存不落盘。
- AC6: 规则行内白名单三组增删去重生效：重复条目被拒并提示；保存后读回条目集合一致（锚 PUT+GET 往返）。
- AC7: 全局默认（agentId 空）已存在时新建全局草稿 → 提交后错误条逐字展示 409 `{"error":"AgentId「X」已存在策略配置"}` 原文，输入保留。
- AC8: 审计视图首切拉取第一页（裸封套 items/total/page/pageSize）；toolName/kind 过滤组合重查；空结果显示「当前过滤条件下没有记录」；翻页传 page/pageSize（1..200 钳位）。
- AC9: profiles 接口 503 → 面板错误态「权限策略服务未就绪，请升级宿主」+ 重试可恢复；保存失败输入不丢。
- AC10: `pnpm run check`/`test`/`design:check` 三门禁绿；新文件零裸色值（棘轮不涨）。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| ~~U1 DTO 字段名/端点路径~~ | — | **已解决**：verify-dev 契约冻结（见 Input 节），21/21 后端单测绿 |
| ~~U2 决策枚举字面量~~ | — | **已解决（走查对表实锤两段）**：UI 恒用 `"Allow"/"Ask"/"Deny"` 字面量；HTTP 线上 = 数值枚举（Allow=0/Ask=1/Deny=2，宿主 MVC 无全局 JsonStringEnumConverter），API 层集中双向映射，单测锚定 |
| ~~U3 保存并发控制~~ | — | **已解决**：无版本号并发控制；AgentId 唯一性由 409 兜底；UI 逐字透出后端 error |
| ~~U4 审计分页契约~~ | — | **已解决**：裸封套 `{items,total,page,pageSize}`，pageSize 1..200，非法 400 |
| U5 features.ts 深链 `?cat=` 是否需要 | features.ts 条目形态 | 默认做 `?cat=` 深链（SettingsView 写面内），闸门1 可砍 |
| ~~U6 PUT 新建语义细节~~ | — | **已解决（走查对表实锤）**：控制器路由为 `PUT profiles/{id:long}`（id 必填），id=0 = 新建；API 层已按此实现并过测试 |
