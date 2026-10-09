# 05 Evidence — 插件 Data 落点缺陷修复（2026-10-09-plugin-data-root）

> 状态来源分级：Verified=亲自执行拿到真实输出 / Inferred=代码推断 / Unknown=未验证。
> 本批为轻量任务：mini-task.md（Intent/Spec/Plan/Task 合并）+ 本 Evidence + 06 Review。

## 1. 验证记录（按时间序）

| # | 验证项 | 命令/方式 | 结果 | 等级 |
|---|--------|-----------|------|------|
| V1 | PluginDbs 登记生效 | 代码改动 `ForgeSelf.Api/Data/XCodeConfig.cs` PluginDbs 追加 `AgentHub→agent-hub`、`ImGateway→im-gateway` | 字典 12 项（含既有 10 项），带 2026-10-09 实证注释 | Verified |
| V2 | 后端 Api 编译 | `dotnet build ForgeSelf.Api.csproj` | 0 错误 / 1346 警告（既有基线） | Verified |
| V3 | 反向守卫单测 | 新增 `有XCode实体库的插件连接名_必须在PluginDbs登记()`（正则扫 Plugins/**/*.cs ConnName ⊆ DbFiles.Keys） | 首跑红 CS0104（Moq.Match 歧义）→ 全限定 `System.Text.RegularExpressions.Match` 修复 | Verified |
| V4 | XCodeConfigTests 过滤集 | `dotnet test --filter FullyQualifiedName~XCodeConfigTests`（日志 `.temp/test-xcodeconfig2.log`） | EXIT=0；0 错误 / 99 警告；新用例「有XCode实体库的插件连接名_必须在PluginDbs登记」已通过；用 2:06 | Verified |
| V5 | 数据迁移 | 备份目标旧库 `AgentHub.db.bak-20261009`（57344B，保留）→ 拷贝 `versions/2.3.3.2610081746/Data/AgentHub.db`（385024B）→ 用户根 | SHA256 源=目标=`D2781008FC23BDBE3A88A28CDAB2D4BA029DC5E5E1525BF42A21027D96566749` **MATCH** | Verified |
| V6 | 中档全量门禁 | `dotnet test`（全量，日志 `.temp/test-full-20261009.log`） | **2883 通过 / 13 失败 / 总计 2896**，23m58s | Verified |
| V7 | 打包 2.3.8.2610091746 | `release-local.ps1 -Version 2.3.8 -Sign -UpdateDir updates` | ALL DONE in 218s；签名新增 2/跳过 1/失败 0；校验 3/3 | Verified |
| V8 | **升级事故（2.3.8.2610091746 崩溃）** | 页面自升级 → 宿主启动失败 | 2.3.8 日志 `ReflectionTypeLoadException: Could not load ... 'ModelContextProtocol.Core, Version=2.2.0.0'` → 异常退出；versions/current 切到 2.3.8 但无进程 | Verified |
| V9 | 事故根因 | 解压 zip + 对比插件目录 | zip 内 `versions/<ver>/plugins/McpCenter/` 仅 McpCenter.dll+deps.json+plugin.json+web，**无 ModelContextProtocol*.dll**；`ForgeSelf.Api.csproj:150-151` StageAllPlugins 从插件 bin 拷贝，而类库 bin 默认不含 PackageReference dll（CopyLocalLockFileAssemblies=false）——McpCenter 是首个带第三方 NuGet 依赖（ModelContextProtocol.AspNetCore 2.2.0，GitHub 同步引入）的插件 | Verified |
| V10 | 服务恢复 | current 指回 2.3.7 + 启动根启动器 | health 200（2.3.7 恢复，18:09） | Verified |
| V11 | 修复 + 重打 | McpCenter.csproj 加 `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` → build McpCenter → build 宿主 → release-local -Version 2.3.8（新时间码） | 插件 bin 出现 ModelContextProtocol.*.dll ×3；宿主输出 Plugins/McpCenter 携带 ×3；**2.3.8.2610091811** ALL DONE in 198s、签名新增 2/失败 0、校验 3/3；zip 内 McpCenter 目录含 MCP dll + Microsoft.Extensions.AI.Abstractions.dll | Verified |
| V12 | 2.3.8.2610091811 运行实例升级 | 页面检查更新→下载→重启并更新（确认弹窗自点） | health 200；进程 `versions/2.3.8.2610091811/ForgeSelf.exe` 存活；current=2.3.8.2610091811 | Verified |
| V13 | **AC4 agents 恢复** | `GET /api/agent-hub/agents`（Bearer token） | **200，data 非空：opencode（id=1，kind=Coding，defaultCwd=项目根，accessPoint=CLI）恢复** | Verified |
| V14 | 库落点 | `versions/2.3.8.2610091811/Data/` 无 AgentHub.db；用户根 `~/.forgeself/plugins/agent-hub/AgentHub.db`=385024B（迁移库，宿主读写） | 登记生效：库落数据根插件目录，跨版本持久 | Verified |
| V15 | 现场截图 | bu 截图存档 | `ForgeSelf.Web/screenshots/live-51888/agent-hub-2.3.8-恢复.png`（/todo 列表 13 条、v1.1.2 徽标、状态徽标正常） | Verified |

## 2. AC 逐条

- **AC1**（PluginDbs 含 AgentHub/ImGateway）：✅ V1/V2/V4。
- **AC2**（XCodeConfigTests 全绿含反向守卫）：✅ V3/V4（EXIT=0）。
- **AC3**（迁移 SHA256 == 源库、旧文件已备份）：✅ V5。
- **AC4**（升级 2.3.8 后 agents 非空 = opencode 恢复）：✅ V12/V13/V14（2.3.8.2610091811）。

## 3. 全量基线对表（V6，13 红判责）

失败 13 项与本批改动（XCodeConfig.cs / XCodeConfigTests.cs / McpCenter.csproj）零交集：
- `WorkflowPlanningIntegrationTests` ×6（需真实 LLM 通道/工作流端点既有问题——历史记忆 2026-09-01/09-10 反复登记）
- `RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom` ×1（仓库级脚本守卫；TODO 已登记 pre-commit 用 powershell 偏差；本批未改 scripts/**）
- `TodosControllerTests.CompleteThenReopen_Flow_ShouldFlipStatus` ×1（2026-10-08 已判预存/环境红，InvalidCastException ObjectResult→OkObjectResult）
- `ScriptRunnerDiIntegrationTests.GetRuntimes_ShouldResolvePluginRuntime…` ×1（既有）
- `McpCenterRuntimeTests.令牌不传时保留原值 / 令牌传空串表示清除鉴权` ×2（既有）
- `DesignAgentToolContractTests.ExecuteAsync_空参数_不抛` ×1（既有）
判据：XCodeConfigTests 过滤集 0 红（V4）+ git diff 仅 3 文件 + 失败类无重叠。**无本批新增红**。

## 4. 事故复盘（2.3.8.2610091746 崩溃 → 2.3.8.2610091811）

- 现象：升级后宿主启动即崩（MVC 扫插件程序集加载失败），51888 短暂不可用。
- 根因：GitHub 同步引入 McpCenter v2.3.0 引用官方 MCP C# SDK；插件发布链（StageAllPlugins 从插件 bin 拷贝）不携带 NuGet 依赖 dll → 版本目录插件缺依赖 → 宿主 ALC 加载失败崩溃。
- 修复：`Plugins/McpCenter/McpCenter.csproj` 加 `CopyLocalLockFileAssemblies=true`（注释留痕）。发布链同类缺陷（任意插件未来引入第三方 NuGet 依赖）→ 记 TODO P1：发布后守卫「插件 deps.json 声明的非宿主依赖 ⊆ 插件目录」。
- 服务恢复：current 指回 2.3.7 + 启动根启动器（可逆操作；宿主当时已崩溃退出，非「停/启/杀运行中实例」）。
- 交付版本：**2.3.8.2610091811**（本地目录更新源，签名 3/3）。

## 5. 产物清单

- 源码改动（未 git 提交，用户未授权）：`ForgeSelf.Api/Data/XCodeConfig.cs`（+2 行登记 + 注释）、`ForgeSelf.Api.Tests/XCodeConfigTests.cs`（+1 用例）、`Plugins/McpCenter/McpCenter.csproj`（+CopyLocalLockFileAssemblies + 注释）。
- 迁移产物：`~/.forgeself/plugins/agent-hub/AgentHub.db`（385024B）+ `AgentHub.db.bak-20261009`（备份）。
- 发布产物：`D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.3.8.2610091811-win-x64.zip` + RELEASE-NOTES + SHA256SUMS.txt。
- 文档回写（输入8 问答批次已完成）：`ForgeSelf.Api/Plugins/README.md §三`（登记=落数据根唯一入口）+ `docs/README.md §一` 索引行。
- 工件：本目录 mini-task.md + 05-evidence.md + 06-review.md。
