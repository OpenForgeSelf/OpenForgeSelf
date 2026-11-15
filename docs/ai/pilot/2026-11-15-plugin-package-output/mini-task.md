# mini-task — 插件包统一交付目录（输入25，2026-11-15）

## Intent（为什么/做什么/到什么程度）
用户直接指令（=闸门1 依据）：插件打包输出目录各插件/各工作树不一致，统一为**项目根上级的 `OpenForgeSelfPluginUpdates/`**；
本批智能终端包也要输出到该目录。程度：`package-plugin.ps1` 打包成功后自动复制 zip 到统一目录；
**worktree 打包也能落到同一目录**（用户原话「如果是工作树的，增加一条复制操作，复制到工作区上级目录」）——
即 worktree 场景从 `.git` 文件的 `gitdir:` 反解主仓根，投递到**主仓根上级**的 `OpenForgeSelfPluginUpdates/`，
不是 worktree 自身的上级（那会落在 `.agents/worktrees/` 里各撒一份）。

## Spec（九节精简）
- 行为：打包成功 → 本地 `artifacts/plugin-packages/` 保留 + 复制到统一交付目录；复制失败只 Warning 不失败（本地包仍在，回滚点=本地副本）。
- 边界：宿主**更新源**（`release-local.ps1 -UpdateDir` → `../updates/`）语义不变——插件交付目录 ≠ 宿主更新源，两个目录各司其职。
- Interaction Design：无 UI 变更（纯脚本/流程/文档）。

## Plan（具体到真实文件）
1. `scripts/package-plugin.ps1` — 新增 `Get-PluginPackageDeliveryDir`（`.git` 为目录=常规检出，为文件=worktree，反解主仓根）+ 打包收尾投递步骤
2. `.agents/skills/plugin-publish-verify/SKILL.md` — 新节「插件包统一交付目录（2026-11-15 用户立）」
3. `docs/04-standards/packaging-upgrade-backup.md` — 真源 §1.8 侧补一行交付目录裁决（§2.5 读完码回写真源）
4. 重跑 `package-plugin.ps1 -Plugin SmartTerminal` 实测端到端投递

## Task
- Allowed：上列文件 + 日记/TODO/工件
- Forbidden：不改 `release-local.ps1` 与宿主更新源语义；不改其他插件打包脚本（如有再按同类扫查判同根因）
- 验证命令：重跑打包脚本看 `Delivered:` 行 + 统一目录实物清单；`scripts/**` 触发中档全量 `dotnet test`；hygiene -Staged；verify-pilot-artifacts
- 裁定：≤3 文件流程改动，轻量 mini-task（任务协调人依 §11 裁剪规则裁定）

## Evidence / Review
完成与验证后补记于 05-evidence（本目录）与日记；Review 结论在 06-review.md。
