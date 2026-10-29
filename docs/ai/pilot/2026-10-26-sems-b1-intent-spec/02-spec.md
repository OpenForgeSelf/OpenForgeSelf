# Spec

> 阶段：Stage 2｜规则：内容与实际项目一致；不发明不存在的接口、类、模块；不确定点记为 Unknown，不自行假定。
> Task ID：2026-10-26-sems-b1-intent-spec
> 依据：Plugins/Sems/docs/requirements.md 4.5 B1。FR/AC 沿用该文档的编号与措辞，本文不另立新条目。

## Functional Requirements

- B1-FR1 应支持以结构化形式记录需求、验收标准、约束与任务拆解。
- B1-FR2 应支持规格与代码变更双向关联：由规格查到关联变更，由变更查到所依据的规格。
- B1-FR3 验收标准应可转化为测试或评测用例，并能标记每条标准是否已有对应用例。
- B1-FR4 规格应版本化，修改留痕。

## Input

- 规格的字段集合（标题、验收标准条目、约束、任务拆解的具体字段）：Unknown。
- 「代码变更」的标识形式（提交号、PR 编号还是其他）：Unknown，取决于 U7。
- 「测试或评测用例」的标识形式（文件路径加用例名，还是其他）：Unknown。

## Output

- 规格列表、规格详情（含版本历史）、规格到变更与变更到规格的查询结果、验收标准的用例关联状态。
- 对外呈现形式（HTTP 端点、sems_* 工具、页面）：Unknown，见 Interaction Design 与 Unknown 表。

## Business Rules

- 规格修改不覆盖旧版本；每次修改产生新版本。
- 变更关联记录生成时的规格版本，不随规格后续修改而漂移（B1-AC3）。
- 与项目档案的归属关系（规格是否隶属某个 IProjectRegistry 项目）：Unknown。
- 与 B7 溯源的边界：B1 依赖 B7（requirements.md 4.5）。B1 首批是否包含「变更来源」的采集，Unknown。

## Boundary Conditions

- 规格无验收标准、验收标准条数为 0 时的显示：Unknown，须在 Plan 前定。
- 同一变更关联多个规格、同一规格关联多个变更：须支持（双向多对多），上限 Unknown。
- 规格被引用时能否删除：Unknown，建议沿用 ProjectService.RemoveProject 对存活会话的拒绝思路，仅为类比，不是已定规则。

## Error Handling

- 沿用 Sems 现有封套：HTTP 层用 SemsResult 与 SemsOpStatus（Ok/Invalid/NotFound/Conflict/Unavailable）映射 200/400/404/409/503；工具层用 {success,data|error}。依据：Services/ProjectService.cs、ToolExtensions.cs。
- 存储不可用时返回 Unavailable（503）还是空结果：Unknown，取决于存储方式。

## Compatibility

- 既有 13 个 sems_* 工具、既有 HTTP 端点（ProjectsController、ProjectCommandsController、RunsController）的行为与返回结构不变。
- 插件仍为 net10.0，只引用 ForgeSelf.Core 与 ForgeSelf.Abstractions。
- 版本号随发布按版本规则递增；是否升 minor：Unknown。

## Non-functional Requirements

- 规格与版本数据重启后不得丢失，这一点与现有内存会话（重启清空，NFR-3）不同。是否需要此持久性取决于存储方式，Unknown。
- 保留期限、数据量上限：Unknown，汇入 requirements.md U5 与 U9。
- 若新增库：必须登记 PluginDbs、测试库隔离、禁止清库自动化（技能 plugin-development）。

## Interaction Design

> 缺本节则闸门 1 不通过。是否有用户可见界面是本任务的拍板项。

- 当前结论：是否在 SemsView 中增加规格页签或页面，Unknown。
- 若增加界面：须用 .agents/skills/ui-ux-design 补写「点什么出现什么」交互表、空态、禁用原因、二次确认、轮询无闪动、窄屏与长文本，并把交互验收并入 AC；还要过 pnpm run design:check。
- 若首批不含界面：本节写「无界面交互」，规格经 sems_* 工具与 HTTP 端点操作。工具入参描述须含示例（技能要求）。
- 以上两种形态在闸门 1 二选一，选定后回填本节。

## Acceptance Criteria

- AC1（= B1-AC1）给定一条规格，能列出全部关联变更；给定一个变更，能显示所依据的规格及版本。
- AC2（= B1-AC2）给定含 N 条验收标准的规格，能显示每条标准是否已关联用例。
- AC3（= B1-AC3）修改规格后旧版本仍可查看，已有关联变更仍指向其生成时的规格版本。
- AC4 既有 13 个 sems_* 工具与既有端点行为不变，相关既有测试保持通过。
- AC5 新增写操作端点类级带 [Authorize("ApiKeyPolicy")]，匿名调用返回未授权。
- AC6 构建通过，新增测试通过，判据看日志正文。

## Unknown

| 编号 | 问题 | 影响 | 默认推荐 |
| --- | --- | --- | --- |
| K1 | 存储方式：落文件 / 插件自有实体库 / 只读索引 | 决定是否改数据库结构、改动规模、需否登记 PluginDbs | 闸门 1 拍板；若无倾向，默认「落文件」，不触发库结构变更，但换机器与打包落位需另定 |
| K2 | U7 现有工具链（代码托管、CI/CD、Coding Agent） | 决定「变更」的标识与关联的采集方式（B1-FR2） | 未答前 B1 首批只做规格自身的记录与版本化，变更关联先记为手工登记的标识 |
| K3 | 是否含界面（Interaction Design） | 决定是否走 ui-ux-design 与 design:check | 默认首批不含界面，仅工具与端点 |
| K4 | B1 优先级（U3） | 排期 | 建议 P1 |
| K5 | 规格归属：是否隶属 IProjectRegistry 的项目 | 数据模型 | 默认隶属项目，但宿主接缝未读，Unknown |
| K6 | 首批是否包含 B7 变更来源采集 | 范围边界 | 默认不含，B7 另立任务 |
| K7 | Sems 现有测试与 e2e 文件 | Test Plan | 实现前 list 核对 |
| K8 | Controllers/ 与 web/ 正文 | 兼容性核对 | 实现前补读 |
