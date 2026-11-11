# Final Report — db-admin 增加 Oracle 类型 + 本地 zip

> Task ID：`2026-11-06-db-admin-oracle-provider`｜日期：2026-11-06｜状态：**COMPLETED_WITH_RISK**（风险=真实 Oracle 实例未联调，标注「未实测」；闸门3 git 提交未授权故未 commit）

## 1. Repository Understanding

见 00。要点：provider 唯一真源 `DriverCatalog.cs`（原 4 项）；XCode 12.0.2026.701 内置 `XCode.DataAccessLayer.Oracle`（ODP.NET Managed Driver）；驱动预检已关闭（2026-11-06 决策3）。

## 2. Selected Task

用户指令：「数据库管理插件，增加oracle类型，改完打本地zip」→ DbAdmin 第 5 个 provider（Oracle）+ 发布链本地包。

## 3. Changed Files（15 源文件 + 工件）

- 后端：`Plugins/DbAdmin/Services/DriverCatalog.cs`（+oracle 项）、`Services/DbConnectionService.cs`（BuildConnStr oracle 分支，EZConnect）
- 插件前端：`web/src/types.ts`（ProviderId+oracle）、`connForm.ts`（1521/label/EZConnect 预填）、`connForm.test.ts`、`contract.test.ts`、`__fixtures__/backend-samples.json`
- 宿主 CT：`ForgeSelf.Web/src/components/DbAdmin.story.ts`（fixture+oracle）、`DbAdmin.ct.spec.ts`（option 4→5）
- 清单/文档：`plugin.json`（0.2.1+描述）、插件 docs 8 处、`docs/02-features/044-db-admin.md`
- 同类修复（存量缺陷）：`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` :179/:199 选择器收紧

## 4. Validation（全部 Verified，详见 05）

插件 test 104/104 + build ✅｜宿主 build 0 错 + DbAdmin 168/168 ✅｜宿主 check 0 错 + test 1203/1203 + design:check ✅｜CT 66 过（2 既有红）✅｜e2e 冒烟 1 passed（48s）✅｜dev-stack 走查 + 截图读图 ✅｜release-local -Sign ALL DONE ✅

## 5. Evidence（关键实测）

- **走查 ORA-12541**：测连回显「No listener at 127.0.0.1:1521」⇒ XCode 连接期驱动加载+TCP 真实发生，链路可用性实证到协议层；错误脱敏无密码。
- **包内证据**：`OpenForgeSelf-2.4.22.2611062341-win-x64.zip`（180.4MB，updates/ 目录）内 db-admin plugin.json=0.2.1、DbAdmin.dll 含 oracle 字面量、web bundle 含 oracle/1521；SHA256 与 SUMS MATCH（cfc34690…）；签名 新增2/跳过3/失败0 + RFC3161；versions 嵌套 0。

## 6. Review

APPROVED（06）。闸门2 交用户验收；闸门3（git 提交）未授权未执行。

## 7. Risk

- 真实 Oracle 实例链路（建表/查询）未验证——UI/文档已按「未实测」口径标注；用户提供实例后可直接复验。
- `DriverCatalog.ProbeEnabled` 若未来改回 true，oracle 依赖用户放置 `Oracle.ManagedDataAccess.dll`（提示文案已含包名）。
- CT 全量在本机有 Chromium GPU 崩溃形态（r1 13 败），降 worker 可复现绿；记 TODO 观察项。

## 8. Problems Found

1. 【存量缺陷·已修】db-admin e2e `table-` 前缀选择器与恒渲染 `table-filter` 碰撞（:199 恒红、:179 空过）；6116843 提交时未跑 e2e 潜伏。
2. 【既有】CT 全量并行下浏览器崩溃形态（本批 r1）。
3. 【既有】backend-samples.json providers 夹具 mysql/pg/sqlserver 仍 `driverAvailable:false`（预检关闭前旧样本）——contract.test 对 mysql 的对应断言同值自洽未红，记 TODO 待同步。

## 9. Process Evaluation

- 顺畅点：00-04 工件先行使实现一次性落位；XCode DLL 字符串探测提前证实 Oracle provider 存在，避免立项争论。
- 卡点：CT r1 浏览器崩溃与 e2e 存量断言红消耗一轮定位——两者均为环境/存量问题，非流程缺口；「提交前必跑一次 e2e」已被门禁覆盖（本次是提交后首次执行该 spec 修订版）。
- 信息丢失：无。

## 10. 问题类归档（缺陷类三节之一）

- 新增 case：`docs/11-troubleshooting/` 未新增独立 case 文件（缺陷为 e2e 断言选择器碰撞，属测试代码缺陷，修复+同类扫查+证据已在本目录 05/07 留痕；如需 PC 台账升级由维护会话裁决）。
- 同类扫查结论：全插件 e2e 前缀 testid 选择器仅此两处命中；修复模式=「断言目标节点加类限定」，已写入本目录。

## 11. 重构提案

- 无（本批不做；`backend-samples.json` 夹具同步建议见 Problems Found 3，TODO 承接）。

## 12. 悬空教训清单

- 无（本节必须为空：所有教训已落 PC/TODO/工件）。

## 13. 交付物

- 代码+文档：工作区未提交（闸门3 未授权）
- **发布包：`D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.4.22.2611062341-win-x64.zip`**（用户在设置页填本地目录更新源或直接解包即用）
- 走查截图：`ForgeSelf.Web/screenshots/dev/db-admin/oracle-provider-form.png`
