---
task_id: 2026-11-06-db-admin-oracle-provider
feature: DbAdmin provider 目录新增 Oracle（EZConnect 连接串/测连/表单/契约样例/文档，插件版本 0.2.1）
risk: { level: L2, rationale: 新增 provider 分支与前端表单；XCode 驱动连接期加载已由 ORA-12541 实测证实；真实 Oracle 实例链路未联调（本机无服务端，UI 标「未实测」） }
expected_files: [Plugins/DbAdmin/Services/DriverCatalog.cs, Plugins/DbAdmin/Services/DbConnectionService.cs, Plugins/DbAdmin/web/src/*, ForgeSelf.Web/src/components/DbAdmin.*, Plugins/DbAdmin/plugin.json, Plugins/DbAdmin/docs/*, docs/02-features/044-db-admin.md]
rollback: git revert 本提交；实体/迁移零改动，无数据回滚面
---
# Task — db-admin 增加 Oracle 类型

> 阶段：Stage 4｜Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06

## Task ID

`2026-11-06-db-admin-oracle-provider`

## Objective

DbAdmin 插件 provider 目录增加 Oracle（表单+连接串建连、测连、Schema、查询），前后端契约样例/测试/文档同步，版本 0.2.1，门禁全绿后打本地 zip。

## Scope

### Allowed

- `Plugins/DbAdmin/Services/DriverCatalog.cs`、`Services/DbConnectionService.cs`（仅 provider 目录 + BuildConnStr oracle 分支）
- `Plugins/DbAdmin/web/src/{types.ts,connForm.ts,contract.test.ts,connForm.test.ts,__fixtures__/backend-samples.json}`
- `ForgeSelf.Web/src/components/{DbAdmin.story.ts,DbAdmin.ct.spec.ts}`
- `Plugins/DbAdmin/plugin.json`（版本+描述）、`Plugins/DbAdmin/docs/*`、`docs/02-features/044-db-admin.md`
- `docs/ai/pilot/2026-11-06-db-admin-oracle-provider/**`、`ForgeSelf.Web/screenshots/dev/db-admin/**`、日记/TODO

### Forbidden

- `DbAdmin.csproj`（零依赖变更）；`SqlGuard/SqlRedactor/审批/审计/控制器`（引擎无关，不动）
- 任何宿主 `ForgeSelf.Api/**` 业务文件；:51888 用户实例（停/启/杀/写配置均禁止）
- 打包 Oracle 驱动 DLL 进插件；真实 Oracle 实例连接验收
- git commit/push/tag（闸门3 未授权；本批发布=本地 zip，用户已指令）

## Acceptance Criteria

- [ ] AC1 `/providers` 5 项含 oracle（1521/false/预检关闭下 driverAvailable=true）——后端单测
- [ ] AC2 oracle 表单 Create → BuildConnStr EZConnect 形态 → Resolve 注册 ConnName——后端单测
- [ ] AC3 oracle 测连失败路径：ok=false、sql_error、message 非空、无密码明文——后端单测
- [ ] AC4 前端 contract/connForm 断言含 oracle（端口 1521、label、buildConnStr 形态）——插件单测
- [ ] AC5 插件前端 test+build 绿；CT S9 option=5 断言绿
- [ ] AC6 dev-stack 走查截图（下拉含 Oracle / 未实测标签 / 测连失败脱敏原文）
- [ ] AC7 文档同步 + `verify-pilot-artifacts.ps1` PASS
- [ ] AC8 `release-local.ps1 -Sign` 产物生成，包内 db-admin=0.2.1

## Expected Files

见 03-plan §Implementation Steps S1-S4 清单（后端 2、插件前端 5、宿主 CT 2、清单+文档 9、工件 8）。

## Verification Commands

见 03-plan §Verification（①-⑥ 六步；判定看日志正文 PASS/计数）。

## Gate 记录

- 闸门1：用户指令端到端授权（03-plan §闸门1 记录）。
- 闸门2：05-evidence + 06-review 齐后交用户验收（本批发布动作已由用户指令预先授权）。
- 闸门3：git 提交未授权，本批不 commit。
