# AI-Native Pilot Result（最终汇报）

> 任务：宿主入口强制 XCode 反向工程 Migration=Full｜ID：`2026-11-12-xcode-migration-full`｜2026-11-12
> 状态：闸门2 待用户验收（**未提交 git**，验收通过前不提交）

## 1. Repository Understanding

我确认了：宿主 ForgeSelf.Api（.NET 10 + NewLife.XCode 12.0.2026.701）入口 `Program.cs` 顶层语句流含 testhost 早退分支（:13-21）与真实宿主路径（数据根解析 → XTrace.LogPath → `ConfigUnifier.UnifyAllConfigFiles` :41-42 → 单实例 Mutex → AppBuilder）；反向工程字段为 `XCodeSetting.Migration`，枚举类型 `XCode.DataAccessLayer.Migration`（Off/ReadOnly/On/Full），SQLite 重建表入口 `SQLiteMetaData.CheckTable(…, Migration, XCodeSetting)`；测试夹具 Migration=On 为 PC-014 故意约定。

## 2. Selected Task

2026-11-12 输入3：运行实例 17:20 报「SQLite 需要重建表，因无法执行 Alter Table ChatTurn …」——Migration=On（仅新建）使结构漂移永久悬置；用户指令在宿主入口强制反向工程 Full，避免复发。

## 3. Changed Files

- `ForgeSelf.Api/Program.cs`：+`using XCode;`；:44-62 新增强制块（Info 日志 + `XCodeSetting.Current.Migration = XCode.DataAccessLayer.Migration.Full` + 降级 Save + 注释）——唯一业务文件。
- 工件：`docs/ai/pilot/2026-11-12-xcode-migration-full/`（mini-task / 05 / 06 / 07）；日记、TODO 同步。

## 4. Validation

Build:

- ✅ `dotnet build` 0 错误（1446 条存量警告）14.45s — Verified

Unit Test:

- ✅ 后端全量 `dotnet test`（中档门禁，-m:1）：总 3738 / 通过 3712 / 失败 26 / 17m10s；对 2026-11-07 基线（3737/29 红）名单差集实证：**本批新增红 = 0**（23 红存量 ⊆ 基线；3 红归属今晨未提交 LLM 代理批，已记 TODO 转交；6 基线红消失为轮次时序波动）— Verified

E2E:

- 未跑（无 UI/插件/契约改动，不满足深档触发条件）— 明示本段未验证
- 附加冒烟：`dev-stack.ps1 -SkipBuild` 起源码宿主，XTrace 日志命中「XCode 反向工程强制为 Full（Migration=Full，新建+修改+删除）」且 `{数据根}/config/XCode.config` 落盘 `<Migration>Full</Migration>`，完成后已 `-Stop` 清理 — Verified

## 5. Evidence

关键结论见 `docs/ai/pilot/2026-11-12-xcode-migration-full/05-evidence.md`（E1 构建 / E2 时序回读 / E3 API 语义三源实证 / E4 全量对表 / E5 冒烟双证据）。日志：`.temp/logs/xcode-migration-build.log`、`.temp/logs/xcode-migration-fulltest4.log`、`.temp/logs/xcode-migration-smoke.log`。

## 6. Review

`06-review.md` Final Decision = **APPROVED**（八问全过；Risk L1；Minor 2 条：LLM 批 3 红转交、本机验证环境不稳定记录）。

## 7. Risk

L1。Full 档语义 = 漂移时执行「备份-重建-回填」（含删除对齐），是用户明确要求的预期行为；已向用户披露其对库结构变更的自动落库语义。

## 8. Problems Found

1. LLM 代理批遗留 3 红（PC-027 违例 AnthropicProtocolBridge.cs:151 / LlmProxyHttp.cs:101；MCP Windows npx shim 启动；OpenAI 出站 delta 未声明 item_id）→ 已记 `TODO.md`（P2），本批不修。
2. 本机全量验证环境不稳定：MSB4166 worker 集群崩溃 + 23 个空闲 MSBuild 节点堆积 → 已清理；`-m:1` 单节点稳定复现。
3. 本环境 `job_output(wait:true)` 不真正阻塞 → 墙钟误判致误杀两轮正常测试 → 已记日记教训（判挂死只信文件时间戳差值与进程 CPU 增量）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取（保留打法）**：
  1. **API 禁脑补三源实证法**：枚举/属性真名用「宿主自写配置注释 + nupkg 内官方 XML 文档 + 编译裁决」三源交叉，比反射更快更稳（反射因依赖加载失败不可行）。
  2. **基线对表用名单差集**：全量红的判责不比总数，提取失败名单做集合差（本次红 − 基线红 / 基线红 − 本次红），配合守卫报错自证文本定归属。
  3. **挂死判定用硬指标**：文件时间戳差值 + 进程 CPU 增量，不信等待体感（本次墙钟误判教训的机制修正）。
- **待改进**：
  1. 首轮全量未先清 MSBuild 空闲节点即开跑，遇 MSB4166 后又在诊断路径上卡死浪费时间 → 下次全量前例行清理节点 + 固定 `-m:1`（引用 §5.0 环境前置精神）。
  2. `job_output(wait)` 不阻塞导致两轮误杀 → 已固化「结束回合等完成通知」打法（日记 T3）。

## 10. 问题类归档

无新增 PC + 理由：本任务为配置策略强化（预防性，非缺陷修复），未修复任何缺陷类问题；16:20 报的现象由输入2 的配置文件修复（Full 已落用户目录 XCode.config）+ 本批入口强制共同闭环，无新的可复现缺陷模式产生；相邻 lens PC-014（测试夹具 Migration=On 约定）已核对不受影响。查 `problem-classes/README.md` 无同根因既有 PC。

## 11. 重构提案

R0 + 理由：单点 19 行启动策略代码，职责清晰（策略归入口、基建归 ConfigUnifier）、无重复模式、无跨模块扩散；不构成重构对象。

## 12. 悬空教训清单（必须为空）

（空——LLM 批 3 红已落 TODO.md 逐字可检索条目；验证环境问题已记日记与本报告 §8；无未落成项。）
