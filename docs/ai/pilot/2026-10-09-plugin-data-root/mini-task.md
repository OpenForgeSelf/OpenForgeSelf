# mini-task：插件 Data 落点缺陷修复（PluginDbs 登记 + 旧库迁移）

> 目录：`docs/ai/pilot/2026-10-09-plugin-data-root/`（轻量任务，≤3 文件；Intent/Spec/Plan/Task 合并本文件，Evidence/Review 单独产出）
> 闸门1 裁剪记录：用户输入「修复」（2026-10-09，承接输入8 问答实证）+ 此前多次明确「需要我拍板的就按你推荐的来」「能操作的话自己操作，见面只报结果」——视作明文授权免闸门1 逐项审批，按推荐方案直接执行；裁剪记录于此。

## Intent（为什么 / 做什么 / 到什么程度）

- **Problem**：AgentHub/ImGateway 的 XCode 实体库连接名（`AgentHub`/`ImGateway`）未登记进 `XCodeConfig.PluginDbs`，宿主 `AddXCode` 不为其注册连接串 → `DAL.Create` 走 XCode 默认派生 `{程序基目录}/Data/{连接名}.db`。发布态程序基目录 = `versions/<ver>/`，宿主升级换版本目录后旧库不再被读、新目录首次启动新建空库 ⇒ 登记数据「全丢」（2026-10-08 输入8 实证：2.3.2=512KB/2.3.3=385KB 有数据，2.3.4 起每版 57KB 空库；2.3.7 外部 agent 下拉空）。
- **Why**：用户以为插件数据在「数据目录下的插件目录」不会变（对 todo 等已登记插件成立），AgentHub 是例外——体验与数据安全缺陷，且已导致实际数据丢失（用户需重新登记 opencode）。
- **Expected Outcome**：① AgentHub/ImGateway 库落 `{数据根}/plugins/{id}/`（跨版本持久）；② 2.3.3 旧库（含登记数据）迁移到用户根，升级 2.3.8 后 AgentHub agents 恢复；③ 反向守卫测试防再犯（任何带 XCode 实体库的插件连接名必须登记）。
- **Constraints**：禁止停/启/杀运行宿主（升级走页面自点）；不删任何既有文件（迁移目标旧文件备份）；不改 ImGateway 业务逻辑；不扩大范围到其他插件。
- **Success Criteria**：① `PluginDbs` 含 `AgentHub→agent-hub`、`ImGateway→im-gateway`；② 后端 build 0 错 + 新反向守卫测试绿（`dotnet test --filter XCodeConfigTests` 全绿）；③ 迁移后 `~/.forgeself/plugins/agent-hub/AgentHub.db` 与源库 SHA256 一致；④ 运行实例升级 2.3.8 后 `GET /api/agent-hub/agents` 返回恢复的登记数据（AgentDefinition=1）。

## Spec（规格）

- **Functional Requirements**：
  1. `XCodeConfig.PluginDbs` 增加 `["AgentHub"]="agent-hub"`、`["ImGateway"]="im-gateway"`（key=连接名 PascalCase，value=插件 Id kebab，与 plugin.json Id 一致：agent-hub / im-gateway 已核实）。
  2. 新增反向守卫单测：扫描仓库 `Plugins/**/*.cs` 中全部 `ConnName = "X"`（BindTable 特性），每个连接名必须 ∈ `XCodeConfig.DbFiles.Keys`。
  3. 数据迁移（操作步骤）：备份用户根目标旧库（不删）→ 拷贝 2.3.3 源库 → SHA256 校验。
- **Input**：无（配置登记 + 文件拷贝）。
- **Output**：登记后的 DbFiles（AgentHub/ImGateway 库路径 = `Plugins/agent-hub/AgentHub.db` / `Plugins/im-gateway/ImGateway.db` 相对数据根）。
- **Business Rules**：库文件名 = 连接名 + `.db`（铁律）；连接名登记后宿主 `AddXCode` 派生到数据根（XCodeConfig.cs:60-61 已有机制，仅需补字典行）。
- **Boundary**：ImGateway 旧库均为空（12288B 无实质数据），只登记不迁移；McpCenter/Sems/Home 等无 XCode 实体库，不涉及；已登记插件不动。
- **Error Handling**：迁移拷贝失败 → 停止并保留目标旧文件；升级后 agents 仍空 → 检查宿主日志 `[AgentHub] 数据库初始化完成` 落点 + `AgentDefinition` 计数。
- **Acceptance Criteria**（可测，逐条）：
  - AC1：`XCodeConfig.PluginDbs` 含 AgentHub/ImGateway 两行（代码断言 + 单测）。
  - AC2：`dotnet test --filter "FullyQualifiedName~XCodeConfigTests"` 全绿（含新增反向守卫）。
  - AC3：迁移后目标库 SHA256 == 源库 SHA256，源库未动，目标旧文件已备份存在。
  - AC4：运行实例升级 2.3.8 后 `GET /api/agent-hub/agents` 非空（AgentDefinition=1，opencode 恢复）。

## Plan（具体文件）

| 文件 | 动作 |
|---|---|
| `ForgeSelf.Api/Data/XCodeConfig.cs:24-41` | `PluginDbs` 字典追加 `["AgentHub"]="agent-hub"`、`["ImGateway"]="im-gateway"`（注释注明 2026-10-09 修复） |
| `ForgeSelf.Api.Tests/XCodeConfigTests.cs` | 新增 `[Fact] 有XCode实体库的插件连接名_必须在PluginDbs登记()`：扫描 `PluginsRootOfRepository()` 下 `*.cs`（排除 `_`/`obj`/`bin`）正则 `ConnName\s*=\s*"..."` → Distinct → 断言 ⊆ `DbFiles.Keys`；需 `using System.Text.RegularExpressions;` |
| 数据迁移（操作，非代码） | 备份 `~/.forgeself/plugins/agent-hub/AgentHub.db` → `AgentHub.db.bak-20261009`；`Copy-Item versions/2.3.3.2610081746/Data/AgentHub.db` → 用户根目标；`Get-FileHash` 比对 |

**Test Plan**：后端 `dotnet build`（ForgeSelf.Api + Tests）→ `dotnet test --filter "FullyQualifiedName~XCodeConfigTests"` → 触发中档：碰宿主源码 XCodeConfig.cs → 后端全量 `dotnet test`（基线对表）。
**Verification（Other）**：打包 `release-local.ps1 -Version 2.3.8 -Sign -UpdateDir updates`（判据 ALL DONE + 签名/校验日志正文）→ 设置页自点升级 → exe FileVersion 2.3.8.x → `GET /api/agent-hub/agents` 非空 + `GET /api/plugin/detail/agent-hub` 200 + 截图存档 `screenshots/live-51888/agent-hub-2.3.8-恢复.png`。

## Task（工作单元）

- **Task ID**：2026-10-09-plugin-data-root
- **Objective**：AgentHub/ImGateway 插件库落用户数据根 + 2.3.3 旧库迁移恢复 + 反向守卫。
- **Scope（Allowed）**：改 `XCodeConfig.cs` PluginDbs、`XCodeConfigTests.cs` 加 1 用例；拷贝/备份用户根与版本目录 db 文件。
- **Scope（Forbidden）**：改 ImGateway/AgentHub 业务代码；改已登记插件；删任何既有文件；停/启/杀宿主；git 提交（未授权）。
- **Acceptance**：AC1~AC4 checkbox 全部勾选。
- **Expected Files**：`ForgeSelf.Api/Data/XCodeConfig.cs`、`ForgeSelf.Api.Tests/XCodeConfigTests.cs`；`~/.forgeself/plugins/agent-hub/AgentHub.db`（+`.bak-20261009`）。
- **Verification Commands**：见 Plan Test Plan。
