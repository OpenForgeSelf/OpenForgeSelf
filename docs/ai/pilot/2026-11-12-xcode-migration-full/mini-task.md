# mini-task：宿主入口强制 XCode 反向工程 Migration=Full

> 任务ID：`2026-11-12-xcode-migration-full`｜级别：**轻量**（单文件宿主入口变更）｜日期：2026-11-12
> **闸门1 依据**：用户直接指令（2026-11-12 输入3 原话：「你在宿主入口，设置XCode的配置文件，把反向工程改成Full，避免下次出现类似情况」）。按 2026-11-07 输入8 拍板口径「用户直接指令=闸门1 依据」，视为规格确认通过，记录于此，不再单独等待批准。
> 任务性质判定：**非缺陷类**（配置策略强化/预防性变更，非缺陷修复——不修代码 bug，改宿主启动策略默认值）； adjacent PC 见「适用问题类」。

## 1. Intent（意图）

- **Problem**：运行实例（:51888）2026-11-12 17:20:42 报「SQLite需要重建表，因无法执行：Alter Table ChatTurn Alter Column RequestHeaders/ResponseHeaders text NULL COLLATE NOCASE」。根因链：SQLite 不支持 `ALTER TABLE ... ALTER COLUMN`，实体结构变更必须走 XCode「备份-重建-回填」；而宿主当时 `XCodeSetting.Migration=On`（仅新建表），重建不会执行 → 结构漂移永久悬置。
- **Why**：仅靠数据根 `XCode.config` 文件改 Full（本日输入2 已做）可被删档/换机/重置回退；用户指令要求在**宿主入口代码**强制设定，保证任何部署形态下次启动都归位 Full。
- **Expected Outcome**：宿主真实进程每次启动，在 ConfigUnifier 重定向之后、任何 DAL 访问之前，将 `XCodeSetting.Current.Migration` 强制为 `Full` 并持久化到 `{数据根}/config/XCode.config`。
- **Constraints**：不启停宿主；不碰测试夹具 Migration=On 约定（PC-014）；不改 NewLife 包引用；不新增依赖。
- **Success Criteria**：`dotnet build` 过；后端全量 `dotnet test` 相对基线无新增红；启动日志可见强制设定；`{数据根}/config/XCode.config` 中 `<Migration>Full</Migration>`。

## 2. Spec（规格）

- **Functional Requirements**：
  - FR1：宿主入口（`ForgeSelf.Api/Program.cs` 顶层语句，`ConfigUnifier.UnifyAllConfigFiles(...)` 调用之后）设定 `XCodeSetting.Current.Migration = XCode.DataAccessLayer.Migration.Full`。
  - FR2：设定后 `XCodeSetting.Current.Save()` 持久化；Save 失败仅告警降级（B9-5 哲学），不阻断启动（内存值已生效）。
  - FR3：生效顺序约束：必须晚于 FileName 重定向（否则 `Current` 读旧路径/建错 watcher）、早于 `AddXCode`（AppBuilder 内访问 XCodeSetting）与首次 DAL 访问。
- **Input/Output**：无入参；副作用 = XCodeSetting 内存值 + `{数据根}/config/XCode.config` 第 24 行 `Full` + 启动日志一行 Info。
- **Business Rules**：`Migration` 档位语义（NewLife.XCode 12.0.2026.701，XCode.xml 文档实证）：`Off 关闭；ReadOnly 只读不执行；On 打开，仅新建；Full 完全，修改删除`；枚举类型 `XCode.DataAccessLayer.Migration`；SQLite 重建表入口 `SQLiteMetaData.CheckTable(…, Migration, XCodeSetting)`。
- **Boundary Conditions**：
  - testhost 早退分支（Program.cs:13-21）不经过本代码 → WebApplicationFactory/测试夹具不受影响，PC-014「Migration 保持 On」约定原样。
  - RunTrayMode 由同一顶层语句流覆盖（顶层先于 `--tray` 分支执行）。
  - 重复设定幂等（主入口 + AppBuilder 各自路径重复触发无害）。
- **Error Handling**：Save 抛异常 → `XTrace.Log.Warn` 降级，宿主继续启动（与 ConfigUnifier B9-5 同风格）。
- **Compatibility**：`{数据根}/config/XCode.config` 既有其他键不动；文件不存在时由框架按默认值创建后再被本设定覆盖为 Full。
- **Non-functional**：启动期一次性 O(1) 设定 + 一次文件写，无可测性能影响。
- **Acceptance Criteria**：
  - AC1：`dotnet build` 0 error。
  - AC2：后端全量 `dotnet test` 无基线外新增红。
  - AC3：代码位于 Program.cs 顶层、ConfigUnifier 调用之后（顺序可审）。
- **Failure Modes**：
  - 「启动变慢/失败」→ Save 包降级 try/catch，最坏只丢文件持久化，内存值仍 Full（FR2）。
  - 「测试行为漂移」→ 本代码不在 testhost 路径（边界条件第 1 条），全量测试 AC2 复核。
  - 「Full 档误删表」→ Full 只在实体与库结构不一致时触发重建（XCode 语义），删除动作限于反向工程对齐；风险已在汇报向用户显式提示（输入2）。
- **适用问题类**：查 `docs/11-troubleshooting/problem-classes/README.md`——无同根因 PC 命中（本次为配置策略强化，非缺陷复发）。**相邻 lens：PC-014**（test-ctor-rebind-connstr）明确「测试夹具 Migration 保持 On」与「换串 DAL.Reset 后新实例 Migration 回默认」——本变更刻意避开测试路径，不改夹具；全量测试对表。

### 2.1 无界面交互（2026-10-30 规则显式声明）

本任务**无用户界面**：变更点为宿主入口启动策略（XCode Migration 档位），用户不通过任何页面交互感知。操作与观测路径：① 启动日志一行 Info（强制设定结果）；② `{数据根}/config/XCode.config` 第 24 行 `<Migration>Full</Migration>`（可人工查看）；③ 异常时启动不被阻断（Save 降级仅告警）。无空态/加载/报错 UI 形态需验收。

## 3. Plan（计划）

1. `ForgeSelf.Api/Program.cs`：顶部加 `using XCode;`；`ConfigUnifier.UnifyAllConfigFiles(...)`（约 :40-41）之后插入强制设定块（设定 + 降级 Save + Info 日志 + 注释）。
2. `dotnet build` 验证编译（API 名称由编译器裁决）。
3. 后端全量 `dotnet test`（§5.6 中档，碰宿主源码必跑）对基线。
4. 产出 05-evidence.md / 06-review.md / 07-final-report.md。

## 4. Task（任务边界与验证）

- **Allowed**：`ForgeSelf.Api/Program.cs`（唯一业务文件）；`docs/ai/pilot/2026-11-12-xcode-migration-full/*` 工件；日记/TODO。
- **Forbidden**：ConfigUnifier.cs、AppBuilder.cs、测试夹具、数据根配置文件（输入2 已改，勿重复动）、宿主进程启停。
- **验证命令**：`cd ForgeSelf.Api && dotnet build`；`cd ForgeSelf.Api.Tests && dotnet test`（全量，中档）。

## Evidence / Review

完成后另产出 `05-evidence.md`、`06-review.md`（轻量任务不豁免），最终 `07-final-report.md`。
