# mini-task — F1（P1）用户连接跨宿主重启丢失：诊断与修复方案

> 任务：`2026-11-05-db-admin-f1-connloss`｜状态：**诊断完成，待闸门1 批准**（lead 指令：先只诊断+方案，批准后动手）。

## Intent（为什么/做什么/到什么程度）

qa 在 51888 抓到 F1（P1）：用户连接跨宿主升级重启丢失（03:17:45 保存 POST 200 → 03:30 升级重启后 GET 只剩 sys:DbAdmin）。目标：定位根因 → 修复落 0.1.1 → e2e 覆盖「保存→（模拟重启）→恢复」链 → 走 T-G''/T-H'' 第三轮发行。成功判据：新包在模拟「升级换版本目录」重启后用户连接仍在（e2e 断言 + 51888 实测）。

## Spec（根因链 — 全部实锤，证据编号引用见附录）

1. **运行实例宿主（2.4.22.2611050327 包）构建自 ef89d71**（T-F 合并 dd00969 发生在 03:40 之后；T-G 打包 03:27 时 main=ef89d71）——该时点 `XCodeConfig.PluginDbs` **没有 `["DbAdmin"]="db-admin"` 登记**（登记行在我 13 提交内，随 dd00969 才进 main）。
2. 宿主 AddXCode（Build 前）因此**不登记 DbAdmin 连接串**。
3. `DbAdminPlugin.cs:22` 在 **Apply（builder.Build 阶段）** 调 `DbAdminTables.EnsureCreated()` → `EntityFactory.InitConnection("DbAdmin")` → XCode 找不到已登记连接串 → **自动兜底**：`自动为[DbAdmin]设置SQLite连接字符串：Data Source=Data\DbAdmin.db`（宿主日志 03:30:32.895 铁证）——**相对 CWD=版本目录**。
4. 建表+数据全落 `versions\<ver>\Data\DbAdmin.db`（版本层，违反 packaging-upgrade-backup §1.7 用户数据必须外置红线）：
   - v0300\Data\DbAdmin.db：53KB，`DbConnection` 表 **1 行 = walkthrough-fixture**（qa 03:17:45 保存，mtime 03:17:44 秒级吻合）——**数据还在，可抢救**
   - v0327\Data\DbAdmin.db：53KB，建表 0 行（新宿主在新版本目录重建）
   - `~/.forgeself/plugins/db-admin/DbAdmin.db`：**4096B 零表空壳**（宿主按登记路径建了目录+文件，但 XCode 连接已绑定版本层）
5. **同型对照**：AgentHub/其余 21 插件库全在 `~/.forgeself/plugins/<id>/` ✓——差异 = AgentHub **插件侧零 EnsureCreated 调用**（等宿主统一初始化，登记先行）；DbAdmin 在 Apply 抢跑。
6. qa 第二次保存（03:33:28）的行在 v0327 主库 0 行 + wal 0B——**去向存疑**（可能 POST 失败/表重建清空），需 qa POST 响应佐证；不阻塞主因定案。

**dd00969 合并后**：登记（AddXCode，Build 前）先于插件 Apply（Build 中）→ 时序自愈，库应落数据根。但「插件 Apply 阶段建表」仍是反模式（AgentHubTables.cs:14 注释明言插件跑在宿主建表之前），且旧包兼容性依赖合并时序——**按 AgentHub 先例修掉**。

## Plan（改动）

1. `Plugins/DbAdmin/DbAdminPlugin.cs:22` → **删除 Apply 里的 `DbAdminTables.EnsureCreated()` 调用**（对齐 AgentHub 先例：插件侧不建表，宿主 InitializeXCodeDatabase 按 DbFiles 全量覆盖，登记先行）。保留 EnsureCreated 方法本体（测试夹具仍用）。
2. `Plugins/DbAdmin/plugin.json` + `Plugins/DbAdmin/DbAdminPlugin.cs` 版本串 → **0.1.1**。
3. e2e 冒烟补「保存连接 → 重启宿主（模拟升级，新版本目录 CWD）→ GET 恢复」链（`ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts`）。
4. 数据抢救（修复验证通过后）：v0300 库的 walkthrough-fixture 行迁回 `~/.forgeself/plugins/db-admin/DbAdmin.db`（DataLocationService 正确数据根）。
5. 工件：本 mini-task + 05-evidence（修后回填）+ 06-review。

## Task（验证 + 边界）

- 快档：`dotnet build` + DbAdmin* 过滤集 + 插件 web build/vitest + db-admin e2e（含新重启链用例）。
- 中档理由：**不碰宿主源码**（改动全在 Plugins/DbAdmin + e2e），但会跑全量 dotnet test 兜底（qa-ci 已有档期）。
- Forbidden：不碰宿主 XCodeConfig/AppBuilder（登记已在我已合并提交内）；不碰运行实例文件（除经批准的数据抢救回写）；禁 push。
- problem-class-closure：同类扫查 = 其余插件是否有「插件 Apply 阶段 EnsureCreated」同款反模式（grep 全 Plugins/）——初步：AgentHub/FileTools 等先例均无（表定义本体不算）；扫查结果入 05。

## 六段方案（待拍板）

1. **目标**：DbAdmin 用户连接跨宿主升级重启不丢失；判据 = e2e「保存→模拟升级重启→恢复」绿 + 51888 第三轮实测连接仍在。
2. **改动**：① `DbAdminPlugin.cs:22` 删 Apply 阶段 EnsureCreated（对齐 AgentHub 先例）② 版本 0.1.1 ③ e2e 补重启链用例 ④ 数据抢救迁回 walkthrough-fixture 行 ⑤ mini-task+05/06 工件。
3. **不改**：宿主 XCodeConfig/AppBuilder（登记已随 dd00969 在 main）；DataLocationService；其它插件。
4. **验证**：快档（build+DbAdmin 过滤集+插件 web+e2e 含重启链）+ qa-ci 中档全量兜底；判据看日志正文。
5. **代价与风险**：~40-60 分钟（含 e2e 新用例 6 轮内）；风险 = Apply 删建表后若有插件内 Build 阶段实体访问会提前炸（已查：DbAdminPlugin.Apply 仅注册服务，无实体访问）→ 回滚点 = 单提交 revert。
6. **待你拍板**：① 修复分支：**推荐 feat/db-admin 之上续提交**（已合并，修复提交随 T-G'' 再 no-ff）——或另开 feat/db-admin-0.1.1？② 今晚 0.1.1 若 04:30 前出齐就按第三轮发行走（你已定）；数据抢救放在修复验证通过后、第三轮发行前——是否同意？

## 附录：诊断证据（.temp/f1-diag/ 与日志，全部只读取证）

| 证据 | 内容 | 结论 |
|------|------|------|
| 宿主日志 03:30:32.895 | `自动为[DbAdmin]设置SQLite连接字符串：Data Source=Data\DbAdmin.db;Migration=On` | XCode 自动兜底（登记缺失）实锤 |
| 宿主日志 03:30:32.947 | `DataSource=D:\...\versions\2.4.22.2611050327\Data\DbAdmin.db` | 实际落点=版本层 |
| v0300 库验尸 | DbConnection 表 1 行 walkthrough-fixture（qa 03:17:45 保存） | 数据在旧版本目录，可抢救 |
| v0327 库验尸 | 建表 0 行（新宿主重建） | GET「只剩 sys:DbAdmin」成因 |
| ~/.forgeselves 侧验尸 | DbAdmin.db 4096B 零表空壳 + wal 0B | 登记路径文件被创建但建表走兜底路径 |
| 22 插件库对照 | 其余全部在 ~/.forgeself/plugins/<id>/ | 仅 DbAdmin 异常，同型对照成立 |
| AgentHub 对照 | 插件侧零 EnsureCreated 调用；AgentHubTables.cs:14 注释阐明时序 | 先例姿势 = 插件不抢跑 |
| 0327 包内容 | appsettings.Development.json/playwright.ps1 为 T-G staging 特征；包构建自 ef89d71 | 登记（dd00969）未入包 |