---
task_id: 2026-11-07-db-multi-database
feature: DbAdmin 连接级多库（Database 可空矩阵 + 库枚举/切换 + db_list_databases 工具 + 审批重放透传 database）+ connectionId 数字/字符串双收（输入4/输入5 合并实施）
risk: { level: L2, rationale: HTTP 与 MCP 工具契约双面扩展（可选参数向后兼容）；审批重放丢库为必修缺陷；SQLite 端到端机制级实测，server 型 provider 仅代码路径与 SQL 断言（真机未联调） }
expected_files: [Plugins/DbAdmin/Services/DbConnectionService.cs, Plugins/DbAdmin/Services/DbQueryService.cs, Plugins/DbAdmin/Services/DbAdminToolFunctions.cs, Plugins/DbAdmin/Services/DbAdminAgentService.cs, Plugins/DbAdmin/Services/ResultLimiter.cs, Plugins/DbAdmin/Controllers/DbAdminController.cs, Plugins/DbAdmin/Models/DbAdminModels.cs, Plugins/DbAdmin/web/src/*, ForgeSelf.Api.Tests/Plugins/DbAdminTests/*, ForgeSelf.Api.Tests/Unit/ToolRegistryTests.cs, ForgeSelf.Web/e2e/plugins/db-admin/*, ForgeSelf.Web/src/components/DbAdmin.*, docs/02-features/044-db-admin.md, Plugins/DbAdmin/docs/*]
rollback: git revert 本提交；实体/Model.xml 零改动，无迁移回滚面
---
# Task — Agent 工作单元

> Task ID：PILOT-db-multi-database ｜ 级别：全量 ｜ 实现：本会话（lead）

## Allowed

- S1-S6 列出的文件；新增文件仅限：`Models` 内两 record（并入 DbAdminModels.cs）、`web/src/databaseSelector.ts` + 其测试、`docs/ai/pilot/2026-11-07-db-multi-database/*`、日记/TODO。
- 发布动作（S6 后按需）：`release-local.ps1 -Sign -UpdateDir`，**须用户同意后执行**。

## Forbidden

- 改 `Data/Model.xml` / Entities 生成件（本批实体零改动）。
- 改其他插件、宿主非测试源码（ToolRegistryTests 除外）、`ForgeSelf.Api/**` 主工程。
- 停/启/杀任何运行实例（:51888 用户实例只读复验除外，发布并经用户更新后）。
- 扩大 provider 支持面、做 Oracle schema 会话切换（ALTER SESSION）——明确不做。
- 跳过闸门2/3 提交 git。

## 验证命令（快档 + 插件六步）

1. `cd ForgeSelf.Api && dotnet build`
2. `dotnet test --filter "FullyQualifiedName~DbAdmin"`（+ `~ToolRegistry` 过滤集）
3. `cd Plugins/DbAdmin/web && pnpm run check && pnpm run test`（沙箱内出树构建兜底按 plugin-development §3.2/速查）
4. 插件 CT（若加 story）：`pnpm run test:ct` 过滤 DbAdmin
5. 冒烟 e2e：`bash node_modules/.bin/playwright test --config=playwright.config.ts e2e/plugins/db-admin`（工作目录 ForgeSelf.Web；§5.0 环境前置）
6. dev-stack 起源码走查（AC-I1…I5 + 截图读图 + 清测试数据）
7. 中档触发判定：本批不改 `ForgeSelf.Api/**` 主工程与仓库脚本 → 不强制全量 dotnet test；但契约测试夹具若有共享改动则升中档（实施时按实判并记录）

## 步骤 → 检查映射

- S1 → 命令 1+2 红→绿；S2 → 命令 2；S3 → 命令 2 + 网关过滤集；S4 → 命令 3/4；S5 → 全部；S6 → 走查 + 文档对表。
