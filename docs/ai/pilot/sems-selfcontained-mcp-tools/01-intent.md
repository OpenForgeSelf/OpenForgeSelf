# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-sems-selfcontained-mcp-tools ｜ 日期：2026-09-28

## Problem

sems（软件工程管理系统插件，id `sems`，v1.0.3）当前是一个**只读展示端**，不具备完成自身业务闭环的能力：

1. **项目只能由别的插件产生。** 全仓唯一的登记触发点是 AIAgent 的「选工作目录」（`Plugins/AIAgent/Controllers/ProjectController.cs:39`）。sems 自己没有任何登记入口，首页文案直接把用户支去「AI Agent」页（`Plugins/Sems/web/src/SemsView.vue:9,35`）。AIAgent 不在场时，sems 是一个永远空列表的面板。
2. **项目登记了就不能去掉。** 接缝 `IProjectRegistry` 有 `Update`（只改档案字段）和 `DeleteCommand`，**没有删除项目的方法**（`ForgeSelf.Abstractions/IProjectRegistry.cs:12-37`）。项目档案只能增不能减。
3. **手工改动会被机器覆盖。** `HostProjectRegistry.Register` 命中同 Root 时用目录名覆写 `Name`（`ForgeSelf.Api/Services/HostProjectRegistry.cs:68`），用户在 sems 面板里改的项目名会在下一次目录登记时被冲掉。
4. **登记来源不可区分。** 契约字段 `ProjectInfo.Source` 注释预留了 `manual`（`IProjectRegistry.cs:60-61`），但实现把 `Source` 硬编码为 `"ai-agent"`（`HostProjectRegistry.cs:81`），插件内部自发登记这件事在数据上无处表达。
5. **操作逻辑没有内部落点。** `IProjectService` 只有 `GetProjects()/Count`，所有写操作散在控制器里直连宿主接缝（`ProjectCommandsController.cs:59,73,86`）；`RunnerController` 又与 `RunsController`+`ProjectCommandsController` 端点语义完全重复（已知问题 #1）。同一个能力存在两套真相。
6. **对 AI/外部系统完全不可用。** `SemsPlugin.ToolExtensions` 是空列表（`Plugins/Sems/SemsPlugin.cs:12`）——sems 的全部能力（列项目、管命令、启停进程）无法经 MCP 中心被任何外部调用方使用。
7. **测试面薄。** 控制器与插件装配零测试；插件层 e2e 只有远程加载冒烟（`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`，无任何业务断言）。

## Why

- 用户直接指令（最高优先级来源）要求 sems「插件内应该要能自洽」并「通过 mcp 中心向外提供工具」。
- 项目工作区是**宿主级核心流程**，架构裁决 `docs/01-architecture/host-capability-seams.md` §4.1 在选定「宿主 seed」方案时，写明的优点就是「其他来源（**手动添加**、第三方插件登记）有地方挂」「AIAgent/sems/未来任何插件都是平等的消费方/**登记方**」。**当前实现只落地了 AIAgent 这一个触发源，方案 B 被授权的另一半（sems 自己做登记方）从未实现**——本任务是把已拍板的架构补齐，不是新立架构。
- 工具暴露链路的其他环节（宿主 ToolRegistry、McpCenter 的 `universal_tool`/`list_tools`）均已实现并有测试覆盖；sems 是**唯一未接入的界面型插件**。缺口在 sems 侧，补上即可端到端可用。
- 只有当 sems 的能力收敛到**一处服务实现**，HTTP 端点、自带界面、MCP 工具三个消费面才不会各写一套逻辑（避免第二份真相）。

## Expected Outcome

1. **sems 面板内可完成项目全生命周期**：不依赖 AIAgent，也能「添加项目（含目录浏览/手工输入路径）→ 编辑档案 → 维护运行命令 → 启动/停止/检测进程 → 移除项目」。移除有二次确认，且明确只删档案不动磁盘。
2. **手工登记的来源可辨识**：sems 内添加的项目 `Source="manual"`，AIAgent 目录登记仍为 `"ai-agent"`，两者共存、互不覆盖对方已改的档案字段。
3. **一处实现，多面消费**：项目/命令/运行的全部操作收敛到 sems 插件服务层；HTTP 控制器与 MCP 工具都只做参数装配与结果序列化，不再各自直连接缝。
4. **13 个 `sems_*` 工具经 MCP 中心对外可用**：外部调用方连上网关后，用 `universal_tool{tool:"list_tools"}` 可枚举到全部 sems 工具（含参数 schema），用 `universal_tool{tool:"sems_list_projects"}` 等可真实调用并拿到与面板一致的数据。McpCenter 与宿主零功能改动。
5. **交互约定补齐**：根视图版本徽标、空态分级、破坏性操作 `ElMessageBox` 二次确认（确认编排可单测）。
6. **测试成为完成判据**：契约扩展、sems 服务层、控制器（含鉴权特性）、工具（含 schema 合法性与全局命名唯一）各有后端测试；插件层 e2e 覆盖「添加→落盘→启动→停止→移除（取消+确认两条路）」与「MCP 侧 list_tools 含 sems + 真调 sems 工具」；纯编排函数有 vitest 单测。
7. 按 AGENTS.md §0 出口门禁走完**维护闭环四步**（门禁 / 插件层 e2e / 发布 / 浏览器走查），产出 Evidence + Review 工件与最终汇报。

## Constraints

对照规范 §1 硬性约束逐条自检：

| # | 约束 | 本任务如何满足 |
| --- | --- | --- |
| 1 | 不修改生产环境 | 只改仓库源码；e2e 走隔离实例；**不碰用户运行中的宿主（`:51888` / `D:\src\tools\ForgeSelf`）** |
| 2 | 不修改数据库结构 | **不加列、不建表、不打迁移**。删除项目走行级 DELETE；`Project`/`RunCommand` 表结构原样不动 |
| 3 | 不改鉴权/权限/安全核心 | 沿用既有 `ApiKeyPolicy`，新端点同样挂；不新增策略。MCP 网关 token 机制不动 |
| 4 | 不新增大规模依赖 | 零新 NuGet/npm 依赖（vitest 复用宿主 `ForgeSelf.Web` 已有版本或插件既有 dev 依赖，若需引入即升级审批） |
| 5 | 不无关重构 | 范围内重构仅限 sems 内部（服务层收口、删冗余 `RunnerController`）；宿主只改 `IProjectRegistry` + `HostProjectRegistry` 两点 |
| 6 | 不改无关文件 | 禁止改 AIAgent、McpCenter、宿主前端；`docs/02-features/028-project-workspace.md` 属本插件文档，须同步 |
| 7 | 不为展示能力扩范围 | 工具数量与 HTTP 端点一一对应（13 个），不额外发明「AI 专属」能力 |
| 8 | 必须能实际验证 | 见 03-plan Verification（build/test/e2e 全部真实执行） |
| 9 | 结论基于真实仓库 | 00 号工件每条带 file:line；构建基线已亲跑 |
| 10 | 失败不伪造 | Evidence 只记实际跑过的命令与输出，Verified/Inferred/Unknown 分级 |

其他硬约束：
- **契约变更不得破坏现有调用方**：`IProjectRegistry` 采用**新增重载**而非改签名，AIAgent（唯一 `Register` 生产调用方）零改动。
- **架构一致性**：sems **不得**改为项目数据的 owner（自建库/自建接缝实现），数据仍归宿主 seed（`host-capability-seams.md` §4.1 方案 B）。
- **数据安全铁律**（技能铁律 10）：任何测试与代码不得删除数据库文件/数据目录。
- **发布与进程铁律**（技能铁律 16 / AGENTS.md §0）：禁止 agent 停/启/杀用户宿主；发布走打 tag 或本地更新源。
- **不做事件驱动**：运行列表仍为请求时拉取（`host-capability-seams.md` §5「不做事件优先架构」）。

## Success Criteria

以下每一条都可被实际命令/断言判定，不依赖主观评价：

1. `curl POST /api/projects {root}` 返回 `success:true` 且新记录 `Source=="manual"`；同 Root 重复调用不新增记录；AIAgent `POST /api/project/directory` 行为与状态不变（回归：`HostProjectRegistryTests` 全绿）。
2. 先在 sems 面板改项目名 → 再触发一次同 Root 登记 → 项目名**保持用户所改值**（有后端测试断言）。
3. `DELETE /api/projects/{id}` 删除成功后：该项目与其全部命令行从库中消失；项目有存活 Launched 会话时返回拒绝且**不删任何数据**；磁盘目录与文件不受影响（有测试断言目录仍存在）。
4. sems 自带界面在**不安装/不启动 AIAgent 目录选择动作**的前提下，可从空态完成「添加项目 → 卡片出现 → 刷新页面仍在 → 添加命令 → 启动 → 停止 → 移除项目（取消路径零请求 / 确认路径删除成功）」，全过程 e2e 断言通过并留截图。
5. 经 MCP 网关（默认 `:18889`，Bearer token）：`tools/list` 返回 `universal_tool`；`universal_tool{tool:"list_tools", parameters:{keyword:"sems"}}` 返回全部 13 个 sems 工具名；`universal_tool{tool:"sems_list_projects"}` 返回的总项目数与面板一致。以上为 e2e 真实断言（零 mock）。
6. 全部 sems 工具在宿主 `IToolRegistry` 注册成功（无重名静默丢失）：后端测试对 `GetAllTools()` 断言 13 个名字齐备；且每个工具 `ParametersJsonSchema` 可被 `JsonDocument.Parse` 且 `required` 字段被 `ValidateParameters` 真实拒绝缺参调用。
7. 门禁全绿：`dotnet build` 0 错误；`dotnet test`（含新增测试）0 失败；`Plugins/Sems/web` 构建产出 `dist/index.js`+`style.css`；宿主前端 `pnpm run check`+`pnpm run test` 通过。
8. 插件层 e2e 与菜单/路由对账 spec 实跑通过（本任务不改 route，须证明未回归）。
9. 维护闭环四步证据齐备（`05-evidence.md`）：门禁 / 插件层 e2e / 发布产物可下载且校验一致 / 浏览器走查截图读图并清掉测试数据。
10. `docs/02-features/028-project-workspace.md` 与新契约/新端点/新工具一致；`plugin.json` Version 升至 `1.1.0`；`06-review.md` Final Decision = **APPROVED**。
