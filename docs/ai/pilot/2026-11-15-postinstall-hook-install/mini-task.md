# mini-task — postinstall 自动安装 git hooks + 根目录 package.json 透传前端脚本

> 轻量任务：Intent / Spec / Plan / Task 合并为本文件（五要素）；Evidence / Review / Final Report 单独产出 05/06/07。
> 来源：输入4/输入5（2026-11-15，原文见 `.forgeself/memory/2026-11-15.md`）。用户已授权本任务提交（「改完提交」），不推送。

## 1. Intent（为什么 / 做什么 / 到什么程度）

- 背景：git hooks（`pre-commit`/`commit-msg`）本体不入库（`.git/hooks` 是本地文件），新 clone / 新环境需要人工记得手动跑 `scripts/install-git-hooks.ps1`——用户原话「不要靠自觉安装」，要求还原包时自动触发。
- 目标：
  1. `pnpm install`（还原包）时自动安装 git hooks，覆盖两条主还原路径：**仓库根** 与 **ForgeSelf.Web**（CI `frontend-check.yml` 在 ForgeSelf.Web 内 `pnpm install --frozen-lockfile`，走后者）。
  2. 根目录新增 `package.json`，把 ForgeSelf.Web 的脚本透传（用户在根目录可直接 `pnpm run check` 等）。
- 成功判据（AC）：
  - AC1 根目录存在 `package.json`：`private` + `packageManager` 与 ForgeSelf.Web 同源（pnpm@10.13.1），scripts 全量透传（`pnpm -C ForgeSelf.Web <script>`）。
  - AC2 根目录与 ForgeSelf.Web 均有 `postinstall`，调用 `scripts/postinstall-install-hooks.cjs`（粘合层）。
  - AC3 触发链实测（根目录）：把 `.git/hooks/pre-commit`、`commit-msg` 移走 → 根目录跑 `pnpm install` → 两个 hook 还原且内容与 `scripts/hooks/` 源一致，日志含安装输出。
  - AC4 同一触发链在 ForgeSelf.Web 内复测通过（`pnpm install --frozen-lockfile`，对齐 CI 形态）。
  - AC5 失败模式显式：非 git 检出（无 `.git`）时 postinstall 跳过并打提示，安装不失败；pwsh 不在 PATH 时同样跳过并提示；安装脚本真实失败时透传非零退出码（代码审查 + 触发链日志，证据等级见 05）。
  - AC6 透传实测：仓库根 `pnpm run design:check` 与 `pnpm run check`、`pnpm run test` 跑通，行为与在 ForgeSelf.Web 内等价。
  - AC7 文档回写：AGENTS §0 门禁条（新 clone 自动装 + 手动兜底）、agent-workflow b-engineering-platform §B13 机制行、commit-hygiene-audit 技能（如其提及手动安装为唯一途径）。

## 2. Spec

### Interaction Design（无界面交互）

本任务无 UI；交互面 = 包管理器生命周期与 CLI：

- 在仓库根或 ForgeSelf.Web 执行 `pnpm install` → 还原依赖后自动输出安装日志（「已安装 <hook> -> …」「安装校验通过」）；非 git 检出输出 `[postinstall] skip: not a git checkout…`；pwsh 缺失输出 `[postinstall] skip: pwsh not found on PATH…`；真实失败透传非零码（还原显式失败）。
- 在仓库根执行 `pnpm run <web脚本>` → 等价于在 ForgeSelf.Web 内执行同名脚本；`pnpm run <脚本> -- <args>` 时参数向下游透传。
- 空态/边界：根目录 `package.json` 无依赖（不装任何包）；`postinstall` 幂等（重复 install 重复安装 hooks，结果一致）。

### 边界与约束

- **不改** `scripts/install-git-hooks.ps1`（安装逻辑单一真源不动；守卫做在粘合层）。
- **不引入** `pnpm-workspace.yaml`（workspace 化需迁移 lockfile，风险大、收益小）。
- pnpm 10 下根项目 `postinstall` 预期默认触发（husky 等同类机制依赖此行为）；**以实测为准**（AC3/AC4），若不触发则改用 `prepare` 钩子或 `.npmrc` 开关重测。
- 覆盖缺口（如实声明，不夸大「全自动」）：`Plugins/*/web` 独立还原、`--ignore-scripts` 安装、在其他子目录跑 `pnpm install`，均不触发本机制；文档如实写。

### 决策记录（决策 → 背景 → 理由 → 准则）

- 决策：postinstall 走 node 粘合脚本（`scripts/postinstall-install-hooks.cjs`），不在 package.json 内嵌 pwsh 命令行。
- 背景：JSON 内嵌 pwsh 需多层引号转义，Windows cmd 与 POSIX shell 引号语义不同；CI frontend-check 跑在 ubuntu。
- 理由：粘合层集中处理「是否该装」（`.git` 存在性守卫，兼容目录与 worktree 的 gitdir 文件）与「pwsh 缺失兜底」（ENOENT 跳过），跨平台行为一致；真正安装仍单一真源 `install-git-hooks.ps1`。
- 准则：可回滚（删 3 处接线即回原状）、少耦合、失败模式显式（该跳过的跳过有提示，真失败透传）。

## 3. Plan（改什么文件）

1. 新建 `scripts/postinstall-install-hooks.cjs`：`repoRoot = scripts/` 上级；无 `.git` → 提示后 exit 0；否则 spawn `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1`（cwd=repoRoot，stdio inherit）；ENOENT → 提示跳过；非零退出码透传。
2. 新建根 `package.json`：`private` + `packageManager` + `engines` + `postinstall` + 透传脚本集（对齐 ForgeSelf.Web 现有全部脚本）。
3. 编辑 `ForgeSelf.Web/package.json`：scripts 增 `postinstall: node ../scripts/postinstall-install-hooks.cjs`。
4. 实测触发链（AC3/AC4/AC6）+ 门禁：快档（check / test / design:check / 卫生守卫 `-Staged`）；**中档全量 `dotnet test`**（`scripts/**` 新增文件触发，按 §5.6 规则跑）。
5. 文档回写 + 05/06/07 + TODO 移除 + 提交（`Pilot: 2026-11-15-postinstall-hook-install` trailer，不推送）。

## 4. Task（Allowed / Forbidden + 验证命令）

- Allowed：上述 3 个 manifest/脚本文件；docs 回写（`AGENTS.md` §0、`docs/04-standards/agent-workflow/b-engineering-platform.md` §B13、`.agents/skills/commit-hygiene-audit/SKILL.md` 按需）；本工件目录；`TODO.md`/日记。
- Forbidden：`ForgeSelf.Api/**`、`ForgeSelf.Web/src/**`、`Plugins/**` 源码、既有 `.ps1` 脚本、`not-taken-decisions.md`、历史 pilot 工件、他人日记条目；不推送。
- 验证命令：AC3/AC4 触发链（hook 移走 → install → 还原核对）；AC6 透传；快档 `pnpm run check` + `pnpm run test` + `pnpm run design:check` + `scripts/check-commit-hygiene.ps1 -Staged`；中档 `dotnet test` 全量（后台跑，结果与基线红对表）。
