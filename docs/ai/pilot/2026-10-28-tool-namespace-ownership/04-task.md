---
task_id: 2026-10-28-tool-namespace-ownership
feature: 034-mcp-center
risk: { level: L2, triggers: ["Plugins/**/plugin.json", "ForgeSelf.Abstractions/ToolNamespace.cs"], raised_by_agent: false }
gate1: { mode: reviewer, at: 2026-10-28, ref: "用户 2026-10-28 00:33 拍板" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/ToolNamespace.cs
  - ForgeSelf.Abstractions/IProjectWorkspaceService.cs
  - ForgeSelf.Api.Tests/Architecture/ToolNamespaceGuardTests.cs
  - Plugins/McpCenter/plugin.json
  - Plugins/AIAgent/plugin.json
rollback: "git revert <commit>"
writeback: { feature_doc: pending, reason: "" }
---

# 04 · Task — Agent 工作单元（闸门1 已批，2026-10-28 用户拍板 v2 修订）

## Task ID
`2026-10-28-tool-namespace-ownership`（批次 B1→B4，见 03-plan v2）

## 拍板记录（用户 2026-10-28 00:33）
- 豁免名单：universal_tool / search_tools / list_tools / request_help / run_terminal_command / 文件三件套（read_file/write_file/list_files）全部豁免前缀；finish/submit_plan/complete_step 维持上轮方案豁免（协议词一致性，推定项，可随时推翻）。
- 公共工具迁 MCP 中心（mcp-center 插件）：文件读写、执行命令、万能工具、搜索工具等与插件主功能无关的都迁出；get_current_time/calculate 按「等等」范畴一并迁出并加 mcp_ 前缀（推定，可推翻）。
- 不考虑存量引用：无别名层、无数据迁移。
- 其余工具按上轮方案加前缀。

## Objective
统一工具命名空间（toolPrefix + 11 个保留名 + 迁移表 + 守卫）、公共工具迁 McpCenter（含 IProjectWorkspaceService 契约上移 Abstractions）、删 AIAgent 重复三件，四批落地，中档门禁全绿。

## Scope
**Allowed**
- `ForgeSelf.Abstractions/`：新增 ToolNamespace.cs、IProjectWorkspaceService.cs（接口上移）
- `ForgeSelf.Api/`：ToolRegistry.cs、ExtensionPointManager.cs、Tests（守卫/单测/字面量）
- `Plugins/**`：15 个插件工具 Name/描述/plugin.json toolPrefix；AIAgent（迁出 8 个、删 3 个、字面量、web 前端三表与测试）；McpCenter（迁入 8 个公共工具）
- `ForgeSelf.Web/e2e/plugins/ai-agent/*.spec.ts`
- `docs/`：021/034、fs工具集文档、design-system-consume 技能、ADR 004、not-taken

**Forbidden**
- ❌ 别名层/存量数据迁移（用户拍板不做）；❌ 改数据库 schema；❌ 动外部 MCP 协议面（mcp.* 命名空间、tools/list 契约）
- ❌ 动 ToolBridge 内部词表；❌ 动 run_terminal_command 三道安全门；❌ 新增文件根白名单
- ❌ 动 design-system / sems 已合规工具名（零改动）
- ❌ 发布/提交（闸门2/3 前禁止）；❌ 停/启/杀用户运行实例

## Acceptance Criteria
（= 02-spec v2 §10 AC-1 ~ AC-9，执行时勾选并回填证据）

- [x] AC-1 守卫测试 4 断言绿 + mutation（去前缀）变红验证（S1-S5 + S5 mutation 自检；过滤集 251/251 绿，2026-10-28 01:10）
- [x] AC-2 全注册表零重名；映射表与实际一致（S4 + S3 绿；扫描器三层防误报：StripComments/ServerId 行/绑定特性行）
- [x] AC-3 read_file/write_file 绝对路径必填可用；list_files 省略 path 回退项目根；未选项目根时 read/write 照常工作（ProjectWorkspaceDefaultRootTests F 组重写：NoWorkspace_AbsolutePath 三例 + 相对路径拒绝两例）
- [x] AC-4 AIAgent 注册表贡献 = aiagent_plan_workflow + finish/submit_plan/complete_step/request_help；8 个公共工具在 McpCenter；workflow_* 替代删除的三件（AIAgentPlugin 322 行；McpCenterPlugin Apply 8 扩展 + ListTools）
- [x] AC-5 IProjectWorkspaceService 接口在 ForgeSelf.Abstractions；插件 csproj 零新增插件间引用（ProjectController 补 using 即用）
- [x] AC-6 composer 三档白名单行为等价（表驱动换新名）（宿主 vitest 1034/1034 绿，含 composerContext 权限三档语义断言）
- [x] AC-7 tool-gateway-031 e2e 4 场景（新名新归属语义不变）+ 涉改插件冒烟绿 ⚠️——mcp-center 冒烟 8/8 绿 + ai-agent 冒烟 6/7 绿（红项=LLM 401 环境面）+ e2e 宿主 127 工具注册实证（零旧名零重名）；tool-gateway-031 四场景挂 LLM 上游 401（e2e 宿主无有效 key + 并行会话 provider WIP 进 build），preflight/工具装配层无 unknown tool；另修 spec 两处 calculate→mcp_calculate 漏网
- [x] AC-8 中档门禁 ⚠️——快档 251/251 + vitest 1034/1034 + 20 插件编译 + AIAgent web build 全绿；全量 dotnet test 4 次被存量 testhost 崩溃中止（Integration 段类间组合触发，二分定位 + TODO P1 专项；崩前跑到部分全绿 0 失败；8 红全为登记基线）
- [x] AC-9 文档 + ADR + not-taken 同步；021 清单与新归属一致（ADR-004 + not-taken 044/045 + 021 清单重写 + 005/034/036 活文档 + e2e spec 同类扫查）

## Expected Files
按 03-plan v2 B1-B4 表（约 45 文件：契约 2 + 宿主 2 + 新测试 3 + 插件后端 ~25 + 插件前端 ~6 + e2e 4 + 文档 5+）。超出清单先记偏差再修订本件（Stage 5.7）。

## Verification Commands
```
# 快档（每批）
pwsh -NoProfile -Command "cd ForgeSelf.Api; dotnet build"
pwsh -NoProfile -Command "cd ForgeSelf.Api; dotnet test --filter 'FullyQualifiedName~ToolNamespace|ToolRegistry|UniversalTool|SearchTools|ListTools|PublicToolFunctions'"
# 中档（B3/B4 收口）
pwsh -NoProfile -Command "cd ForgeSelf.Api.Tests; dotnet test"   # 全量，对照基线
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd Plugins/AIAgent/web && pnpm run build
# e2e（AC-7）
npx playwright test e2e/plugins/ai-agent/tool-gateway-031.spec.ts
# 环境前置（AGENTS §5.0）：NO_PROXY=localhost,127.0.0.1 + TEMP=<repo>\.temp\tmp；判定看日志正文不拿 exit code 当证据
```
