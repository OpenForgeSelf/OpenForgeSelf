# Task

## Task ID
AW-SPLIT-20261007

## Objective
把 925 行 / 136KB 的 `docs/04-standards/agent-workflow.md` 按主题簇拆成 `agent-workflow/` 目录下 5 个 ≤~310 行的文件 + 1 个索引，
**§编号与规则文字一字不改**，旧路径保留为薄指路文件，并把 AGENTS.md 等 21 处指路改到新路径。

## Scope

### Allowed
- 新建 `docs/04-standards/agent-workflow/`（README + a-workflow-core + a-workflow-docs-report + b-engineering-daily + b-engineering-platform + c-changelog）
- 覆写 `docs/04-standards/agent-workflow.md`（改为薄指路文件，内容全部迁出）
- 改指路：`AGENTS.md`（13 处 §指向）、`docs/README.md`、`docs/04-standards/packaging-upgrade-backup.md`、`docs/04-standards/ai-native-engineering-workflow.md`
- 本目录 05–07 工件
- 一次性切割/对账脚本放 `.temp/`（不入库，用完即删）

### Forbidden
- **改写任何一条规则的文字内容**（只搬家）；调整 §A*/§B* 编号或小节标题文本；重排小节顺序
- 回改历史 `docs/ai/pilot/**` 工件（含其中的 `agent-workflow.md:551` 式行号引用）
- 碰并行会话在途文件：`Plugins/McpCenter/**`、`docs/02-features/034-mcp-center.md`、`build.ps1`、`scripts/release/*.ps1`、`scripts/publish-plugin-full.ps1`、`.gitignore`、`ForgeSelf.Web/components.d.ts`、`docs/ai/pilot/2026-10-07-tool-bridge-ui-fix/`；`AGENTS.md` 里他人的 §5.0 移位 hunk 不代提交
- 业务代码 / 构建 / 发布链 / 数据库；任何宿主进程启停
- 把切割脚本沉淀进 `scripts/` 或 `temp/` 留存；用一次性脚本的 exit code 当验证结论
- 未获闸门1 批准前动任何 `docs/04-standards/**` 文件；未获授权 commit/push

## Acceptance Criteria
- [ ] AC1 **零内容丢失（逐字节）**：5 个内容文件按原顺序拼接 == 原文件去掉 BOM 与头部 1–13 行后的内容；SHA256 相等
- [ ] AC2 **标题清单闭合**：原文件全部 `^# `/`^## ` 行在新目录中恰好各出现一次（排序后 diff 为空；仅允许每份新增的 H1 行）
- [ ] AC3 单文件体量：最大内容文件 ≤ 320 行（实测 309）；旧 `agent-workflow.md` ≤ 20 行且不含任何规则正文
- [ ] AC4 **指路可达**：全仓 grep 后，仍写 `agent-workflow.md §A*/§B*` 的位置只剩三类允许项（历史 pilot 工件 / 薄指路自身 / README 的历史说明）
- [ ] AC5 相对链接可解析：新目录内跨文件与跨目录链接逐条 `Test-Path` 为真
- [ ] AC6 工件门禁：`pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-agent-workflow-split` 输出 PASS（00–07 八件齐）
- [ ] AC7 范围零越界：`git status` 中本批新增/修改仅限 Allowed 清单；他人文件 diff 未被动过

## Expected Files
- 新增：`docs/04-standards/agent-workflow/README.md`、`a-workflow-core.md`、`a-workflow-docs-report.md`、`b-engineering-daily.md`、`b-engineering-platform.md`、`c-changelog.md`
- 覆写：`docs/04-standards/agent-workflow.md`（薄指路）
- 指针修改：`AGENTS.md`、`docs/README.md`、`docs/04-standards/packaging-upgrade-backup.md`、`docs/04-standards/ai-native-engineering-workflow.md`
- 本目录：00–07

## Verification Commands
```bash
# AC1/AC2（脚本形态，跑完即删）
pwsh -NoProfile -ExecutionPolicy Bypass -File .temp/tmp/split-agent-workflow.ps1 -CheckOnly

# AC3
wc -l docs/04-standards/agent-workflow/*.md docs/04-standards/agent-workflow.md

# AC4
git grep -n "agent-workflow\.md" -- AGENTS.md docs/README.md docs/04-standards | head -30

# AC6
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-07-agent-workflow-split > .temp/tmp/gate-awsplit.log 2>&1; cat .temp/tmp/gate-awsplit.log
```
