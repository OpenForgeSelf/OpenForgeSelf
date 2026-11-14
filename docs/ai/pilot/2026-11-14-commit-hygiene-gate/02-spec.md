# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：2026-11-14-commit-hygiene-gate

## Functional Requirements

- **FR1 L1 内置规则**（正则实现，规则 ID 为对外契约；模式在文档中一律以文字描述，不贴正则原文）：
  - `hygiene.abs-fileurl`：file 协议 URL 携带盘符（`file:///` 后紧跟「单字母 + 冒号 + 斜杠」）。
  - `hygiene.abs-drive`：盘符路径（单字母 + 冒号 + 正/反斜杠；前置字符排除字母数字与 URL 方案字符，避免误伤 `https://` 与时间写法）。
  - `hygiene.abs-posix`：POSIX 用户/系统目录绝对路径（斜杠 + {Users, home, root, mnt, media, srv} 之一 + 斜杠；前置字符排除字母数字与斜杠，避免误伤 URL 域名后路径）。
  - `hygiene.abs-unc`：UNC 路径（双反斜杠 + 主机名 + 反斜杠）。
- **FR2 L2 拒绝列表**：默认路径 `.forgeself/commit-hygiene.denylist.txt`（该目录整体不入库）。格式：一行一条；`#` 开头注释；空行忽略；`re:` 前缀 = 正则（大小写不敏感），否则为大小写不敏感子串。命中规则 ID = `hygiene.denylist`。文件缺失 → L2 跳过并在报告尾注明，不算错误。
- **FR3 运行模式**：
  - `-Staged`（无模式参数时默认）：解析 `git diff --cached -U0` 的**新增行**（含新起行号），只拦新增。
  - `-All`：`git ls-files` 全量已跟踪文本文件逐行扫描；跳过二进制（扩展名黑名单 + NUL 字节嗅探）、锁文件（各类 package lock）、超过 2MB 的文件。
  - `-Files <path[]>`：显式文件扫描（自测与点查）。
  - `-DiffFile <path>`：解析统一 diff 文本（与 -Staged 共用解析器；测试用，避免测试触碰 git index）。
  - `-Json`：输出 JSON 数组；`-DenylistPath`：覆盖默认拒绝列表路径。
- **FR4 退出码**：0 = 干净；1 = 存在违规；2 = 用法/基础设施错误（git 不可用、参数冲突等）。
- **FR5 pre-commit 接线**：在现有三道门之后新增第 4 道「提交内容卫生」——存在暂存内容即运行 `-Staged`；非零 → `FAIL=1` 聚合拒绝；日志重定向到文件再回显（sh 内禁 `*>` glob 形态）。后接 `install-git-hooks.ps1` 重装生效。
- **FR6 守卫测试**：`ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs`——子进程起 pwsh 跑守卫（输出重定向到 .temp 日志文件再断言），覆盖：四条 L1 规则阳性各 ≥1、阴性集（外部 URL、无盘符裸目录名、普通中文/代码）0 命中、-DiffFile 行号正确、拒绝列表命中。
- **FR7 登记与清理**：
  - b-engineering-daily.md 新增「提交内容卫生门禁」节（规则表用文字描述 + 守卫用法 + 拒绝列表格式 + 棘轮与清理策略）。
  - AGENTS.md：§2.3 加一条速记；§2.4 表加技能行；§5.6 快档加守卫入口；就地清理 :33（盘符路径去前缀）、:326（对话方称呼改通用词）、:333（下载目录路径去机器前缀）。
  - DESIGN.md：就地清理 :4 与 :98 的机器前缀路径（保留项目目录名）。
  - `.agents/skills/commit-hygiene-audit/SKILL.md` 新建并登记。
  - not-taken-decisions.md 追加 060（三不做：CI 集成、docs 存量批量清洗、人名机器学习检测 + 无关项目信息机械检测）。

## Input

- `-Staged`：git 暂存区 unified diff（`git diff --cached -U0 --no-color --no-ext-diff`，`core.quotepath=false`）。
- `-All`：`git ls-files -z` 输出 + 文件内容。
- `-Files`/`-DiffFile`：显式路径。

## Output

- 文本报告（逐条）：`[hygiene] FAIL <rule-id> <file>:<line>: <摘录（截断 120 字符）>`；结尾汇总行 `check-commit-hygiene: PASS|FAIL (扫描 N 文件 / M 行，命中 K)`。
- `-Json`：`[{rule, file, line, excerpt}]` + 汇总对象。
- 报告仅供控制台/日志消费；**任何入库文档引用命中时只写规则 ID + file:line + 计数，禁止粘贴摘录原文**。

## Business Rules

- **只拦新增，不追存量**（棘轮）：-Staged 只看新增行；-All 是本地盘点口径，供清理批跟踪，不作提交阻断。
- 通用角色词（如「任务协调人」「用户」「开发者」）不违规；指向具体个人/成员的称呼（含群内昵称、机器账号片段）违规——具体词表在本地拒绝列表维护（默认种子：群内昵称与机器账号片段）。
- 外部项目作为调研对象/公共依赖可提及；其业务细节（内部接口、私有路径、项目内约定）不入仓——机械层不可判定，靠 review 把关（not-taken 060 记录）。
- 裸目录名（无盘符/机器前缀）不违规（例：「下载目录下的 design_handoff_… 目录」形态合法）。

## Boundary Conditions

- 二进制/锁文件/超 2MB 文件跳过；diff 解析跳过 `/dev/null` 头、二进制 diff、`\ No newline` 标记行。
- `file:///盘符` 形态由规则①先命中，通用盘符规则的 lookbehind 防止同串双报。
- 同一行命中多条规则时逐条报告（fileurl 与 drive 互斥除外）。

## Error Handling

- git 调用失败 / 参数互斥（如 -All 与 -Files 同给）→ exit 2 + 原因行。
- 拒绝列表缺文件 → L2 跳过 + 报告尾注明（不升 exit 2）。
- hook 环境无 pwsh → 沿用现有 hook 回退/跳过行为（本批不修已登记的 5.1 回退偏差）。

## Compatibility

- 依赖仅 git + pwsh 7（与现有门禁一致）；Windows 主环境，Linux 不保证（与现状一致，如实标注）。
- 不修改任何被检文件；只读扫描。

## Non-functional Requirements

- -Staged 秒级（单次 git diff + 正则）；-All 全仓（约千余文本文件）应在 1-2 分钟内。
- 输出编码 UTF-8（脚本内显式设置控制台编码）。

## Interaction Design（交互设计 · 必写节 · 2026-10-08 用户立）

**无界面交互**：本批为 CLI 守卫 + git hook 自动触发，无任何用户可见 UI。操作入口与反馈如下：

| 交互点 | 触发 | 结果 | 错误提示 |
| --- | --- | --- | --- |
| 提交自动审计 | `git commit`（hook 已安装） | 第 4 道门运行 -Staged；干净 → 继续后续门；命中 → 逐条打印 `[hygiene] FAIL 规则ID 文件:行` + 汇总，提交被拒 | `[pre-commit] 提交内容卫生 FAIL：…提交已拒绝` |
| 手工暂存区检查 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged` | 同上文本报告；exit 0/1 | git 失败 → exit 2 + 原因 |
| 全仓盘点 | `… -All` | 全量命中清单（本地清理批用） | 同上 |
| 指定点查 | `… -Files <路径>` | 该文件命中清单 | 文件不存在 → exit 2 |

入参示例见上表；无五态/主题/空态诉求（非 UI 交付）。设计门禁三步中第 1 步（design:check）仍随本批验证跑（文档批不触前端源码）。

## Acceptance Criteria

- AC1：-Files 阳性集——运行期构造的四类样本（file URL 盘符 / 盘符路径 / POSIX 用户目录 / UNC）各命中对应规则 ID，exit 1。
- AC2：-Files 阴性集——外部 https URL、无机器前缀裸目录名、普通中文与代码行 0 命中，exit 0。
- AC3：-DiffFile 解析统一 diff：命中行号 = diff 新侧行号（±0）。
- AC4：拒绝列表（re: 与普通子串各一）命中 `hygiene.denylist`；缺失文件时 L2 跳过且有注明。
- AC5：pre-commit 含第 4 道卫生门，FAIL 聚合拒绝提交（文本断言 + hook 重装后真实提交演练）。
- AC6：`dotnet test --filter CommitHygieneGuard` 全绿；全量中档仅基线红。
- AC7：-All 盘点中，本批新增/修改文件零违规（存量文件命中不新增）。
- AC8：登记五处齐备（Part B 新节 / AGENTS §2.3+§2.4+§5.6 / 技能 / not-taken 060 / AGENTS+DESIGN 清理行）。
- AC9：本批全部入库文件自身零违规（守卫 -Files 对本批文件自扫）。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| 「通用角色词 vs 人名」的边界（如群内职务型称呼是否算角色名） | 拒绝列表词表宽严 | 保守假设：昵称与机器账号一律拦；通用职务词放行；词表本地可调，最终答复列为待拍板项 |
| 「与本项目无关的其他项目信息」无机械信号 | FR 无法全自动 | 搁置：not-taken 060 记录，靠 review；拒绝列表可加已知项目名 |
| POSIX 目录白名单是否需扩（opt/var/etc/tmp 等） | 误报率 | 保守假设：首版用 6 目录；扩充走拒绝列表 re: 条目，不改正则 |
