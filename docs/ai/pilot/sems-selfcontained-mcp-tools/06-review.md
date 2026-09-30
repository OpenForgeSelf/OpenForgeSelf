# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？**
   满足。三重目标逐一对证：(a) 插件内自洽——sems 界面新增「添加项目」（目录浏览+手工路径，source=manual）、移除、命令 CRUD、启停、版本徽标、空态分级，不再依赖 AIAgent 选目录，旧文案「请前往 AI Agent 页」已移除并有 e2e 反断言；(b) AI-Native 流程——`docs/ai/pilot/sems-selfcontained-mcp-tools/` 全套九阶段工件，闸门1 经用户批准（全量 + 13 工具含启停）；(c) 经 MCP 中心对外——13 个 `sems_*` 工具注册进宿主 ToolRegistry（backend.log 实证），经 mcp-center `list_tools` 枚举 / `universal_tool` 调用，McpCenter 与宿主代码零改动。
2. **实现是否符合 Spec？**
   符合。AC 逐条对证（详见 05-evidence）：服务层收口一处真相（SemsResult 状态语义共用）、Register(source)/Remove 契约扩展且 AIAgent 零改动、运行中守卫 409、目录浏览、13 工具 schema/required/不可恢复警示齐全、前端出树构建裸导入 2 处、测试四层（单测/控制器/工具/ e2e）覆盖。
3. **是否超出了 Scope？**
   否。改动静圈定在 Abstractions 契约、HostProjectRegistry、sems 插件（含 web）、两个 docs 文件、pilot 工件。AIAgent/McpCenter/其他插件/宿主 src 均未动。实施期发现的三处范围外问题（AIAgent browse-directories 404、TodoTracker 同款 CreateScope 隐患、批次E 8 项存量红）全部只记 TODO/not-taken，未顺手修。
4. **是否修改了不应该修改的文件？**
   否。`ForgeSelf.Web/components.d.ts` 曾被出树构建改写（仅行尾），已 `git checkout --` 恢复，保持零无关改动；`.trash/`（RunnerController 移除去向）与 `.temp/`、`publish/` 均在 .gitignore。
5. **测试是否覆盖 Acceptance Criteria？**
   是。85 项 sems 后端过滤全绿（登记来源/同名不覆写/级联删除/服务层 CRUD/503 降级/状态码矩阵/13 工具 schema 与往返/Context 包装 provider 回归）；e2e 4 用例覆盖 UI 全链（零 mock）与 MCP 网关真调（含必填拒绝、已删命令报错、移除后磁盘仍在）与 401 鉴权矩阵。
6. **是否存在明显回归风险？**
   低。契约扩展全向后兼容（2 参 Register 保留委托）；全量 dotnet test 1533/1541，8 项失败逐条归因为批次E 存量（与本次无关，名单与 batch-a 证据一致）；前端宿主 check/vitest 独占跑全绿。
7. **是否存在架构不一致？**
   无新引入。sems 仍是 L1 接缝消费方（数据宿主 owner，方案 B）；工具与 HTTP 端点共用服务层与状态语义；未新增任意脚本执行面（sems_run_command 只接受已登记 commandId）。
   **实施期发现并修复一处真缺陷**：工具基类在宿主 IContext 上 `CreateScope()` 抛「No service for IServiceScopeFactory」——改为经宿主 seed 的根 IServiceProvider 回落（PluginManager.ProvideHostServices 的既定接缝），有 Context 包装回归测试。
8. **Evidence 是否足以证明任务完成？**
   是（以 05-evidence 为准，全部 Verified 级，含 e2e 终态）。唯余「发布」属闸门2 验收后的动作（tag/本地打包需用户拍板），不在本次证据范围。

## Requirement Check

PASS — Intent 三重目标逐条满足；用户「插件内自洽」的可感知差异：不依赖任何其他插件即可完成 项目登记→命令→启动→停止→移除 全生命周期（e2e 生命周期用例即为该 AC 的自动化走查）。

## Scope Check

PASS — 变更文件清单与 Task 工件 Allowed 一致；范围外发现全部记 TODO（AIAgent 404 / TodoTracker CreateScope / 批次E 8 红）。

## Test Check

PASS — 后端 85/85（sems 过滤）+ 全量 1533/1541（8 存量红归因）、宿主前端 check 0 errors + vitest 473 全绿、sems confirmOps vitest 8/8、sems e2e 4/4（见 05-evidence E2E 节）。

## Architecture Check

PASS — L1 接缝语义未破坏；一处实施缺陷（scope 解析）已修并有回归守卫；数据归属、鉴权（类级 ApiKeyPolicy + 401 矩阵）、不可恢复操作警示（4 个危险工具描述含「不可恢复」）均符合 spec。

## Risk

L1 — 见 05-evidence Known Limitations：宿主重启丢运行态（NFR 已接受）、无运行历史持久化（决策 014）、AIAgent 选目录 404 为存量缺陷（决策 013）。

## Findings

### Critical

（无）

### Major

（无）

### Minor

- TodoTracker 工具基类与 sems 旧实现同款 `_services.CreateScope()` 写法，一旦被真调会同款失败——记 TODO P2，建议后续统一收敛（本次不动其他插件）。
- 宿主日志将「工具返回 success=false 信封」记为「执行工具成功」，排障时看 isError 会误判——已在日记沉淀，建议后续宿主侧改进（范围外，未动）。

## Final Decision

APPROVED

<!-- 依据：八问全过、三闸门状态 = 闸门1 已批准、闸门2 证据齐备待用户验收、闸门3（提交/发布）待验收后进行 -->
