# mini-task：发布签名改为默认关闭、参数指定（输入2 · 2026-09-30）

> 级别：轻量（≤3 文件核心改动 + 2 处文档同步）。Intent/Spec/Plan/Task 合并本文件；Evidence/Review 单独产出。

## Intent（为什么 / 做什么 / 到什么程度）
- **Problem**：CI 流水线（`.github/workflows/release.yml:43`）强制传 `-Sign`，触发 `sign-publish.ps1` 自签证书自动生成/信任步骤，实测卡在「[2/5] 已生成自签代码签名证书」（run 36664225915 两次 20min+ 无进展），Release 无法产出。
- **Why**：签名只应在明确需要时进行（如对外商业交付）；日常/CI 构建不应强制自签——自签证书生成在 runner 上不可靠且拖慢发布链路。
- **Expected Outcome**：发布脚本与 CI **默认不签名**；仅在显式传 `-Sign`（或 workflow 手动加参）时执行 Authenticode 签名。CI 重跑能走完构建→打包→Release 全链路。
- **Constraints**：不删除签名能力（`sign-publish.ps1` 保留、`-Sign` 参数保留、商业证书 `-PfxPath` 可插拔保留）；只改默认行为与文档表述；不改发布/打包逻辑其余部分。
- **Success Criteria**：① `release-local.ps1` 不带 `-Sign` 运行不出现签名步骤、不生成证书、正常出 zip；② release.yml 无 `-Sign`；③ 文档（真源/AGENTS/技能）表述全部改为「默认不签、参数指定才签」。

## Spec（规格）
- **Functional Requirements**：
  1. `release-local.ps1` 的 `-Sign` switch 保持缺省 false（不签）——现状已满足，不改代码，仅核验。
  2. `package-release.ps1` 的 `if ($Sign)` 门保持不变——现状已满足，不改代码。
  3. `.github/workflows/release.yml` 发布步骤去掉 `-Sign`，注释同步更新（输入42 → 输入2 策略变更）。
  4. `docs/04-standards/packaging-upgrade-backup.md` §1.1 签名行：由「发布必带 -Sign（输入42 起铁律）」改为「默认不签，-Sign 参数指定才签；CI 默认不签」；§5 变更记录新增一行。
  5. `AGENTS.md` §2.3 发布规范句：去掉「发布必带 -Sign」措辞，改为默认不签、参数指定。
  6. `.agents/skills/plugin-publish-verify/SKILL.md`：一键跑示例与参数说明中「-Sign 为发布必带（输入42 铁律）」改为「默认不签，需要时传 -Sign」。
- **Input / Output / Boundary**：无运行态输入；纯脚本/CI/文档策略变更。不改 `sign-publish.ps1` 本身（签名能力保留）。
- **Error Handling / Compatibility**：签名能力仍可用（传 `-Sign` 即恢复）；CI 不再触发自签；不影响本地 `release-local.ps1 -Sign` 手动签名路径。
- **Acceptance Criteria**：见 Success Criteria。

## Plan（具体到文件）
1. `.github/workflows/release.yml:43` → `run: ./scripts/release/release-local.ps1 -Version "${env:GITHUB_REF_NAME}"`（去 `-Sign`）；行 41-42 注释改为「输入2：CI 默认不签名；需要签名时在此加 -Sign（sign-publish.ps1，自签/商业证书可插拔）」。
2. `docs/04-standards/packaging-upgrade-backup.md:34` → 签名行改为「**默认不签名（输入2 起）**：`release-local.ps1 -Sign` 显式指定才签……CI 默认不签」。
3. `docs/04-standards/packaging-upgrade-backup.md` §5 变更记录 → 新增 2026-09-30 行（输入2：签名默认关闭）。
4. `AGENTS.md:95` → 发布规范句改为「发布默认不签名，需要时传 `-Sign`（`scripts/sign-publish.ps1`，自签/商业证书可插拔；CI 默认不签）」。
5. `.agents/skills/plugin-publish-verify/SKILL.md:40` → 参数说明改为「`-Sign` 可选（默认不签，需要时传）」。
6. 不碰：`release-local.ps1`、`package-release.ps1`、`sign-publish.ps1`、`build.ps1`、docs/09-operations/code-signing.md（build.ps1 路径本就默认不签）。

## Task（工作单元）
- **Objective**：让发布/CI 默认不签名，签名改为参数指定。
- **Scope Allowed**：上述 5 个文件的编辑；本地快速核验 `release-local.ps1` 不带 `-Sign` 不触发签名；提交 + 推送 + 打 tag v2.2.2026.0930（删旧重打）。
- **Scope Forbidden**：改签名脚本实现；改打包/发布其他逻辑；动 040 业务代码；未经用户授权执行 git 之外的外部操作。
- **Acceptance Criteria（checkbox）**：
  - [ ] release.yml 无 `-Sign`
  - [ ] 真源 §1.1 签名行 + §5 变更记录已更新
  - [ ] AGENTS.md §2.3 已更新
  - [ ] plugin-publish-verify SKILL.md 已更新
  - [ ] 本地 `release-local.ps1 -Version 0.0.0-local -SkipFrontend` 不出现签名步骤、正常出 zip
  - [ ] git 提交（PILOT 工件链 PASS）→ push → tag v2.2.2026.0930 重打推送
  - [ ] CI 重跑走完（Release 产出）
- **Verification Commands**：
  - 本地：`pwsh scripts/release/release-local.ps1 -Version 0.0.0-local -SkipFrontend`（观察无「Authenticode signing」步骤；输出 zip）
  - git：`git add -p`（只含 5 文件）→ commit（pre-commit hook 验 PILOT 工件）→ `git push github main` → `git tag -f v2.2.2026.0930` + `git push github v2.2.2026.0930 --force`
  - CI：`gh run list` / `gh release view v2.2.2026.0930`
