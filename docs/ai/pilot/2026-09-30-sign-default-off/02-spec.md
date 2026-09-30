# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-051（2026-09-30-sign-default-off）

## Functional Requirements

1. `.github/workflows/release.yml` Build 步骤：`run:` 去掉 `-Sign`（`./scripts/release/release-local.ps1 -Version "${env:GITHUB_REF_NAME}"`）；行注释同步更新为「输入2：CI 默认不签名；需要签名时在此行追加 -Sign（sign-publish.ps1，自签证书自动生成/复用 + DigiCert 时间戳；商业证书场景在 CI secrets 传 -PfxPath/-PfxPassword，脚本可插拔）」。
2. `docs/04-standards/packaging-upgrade-backup.md` §1.1「Authenticode 签名」行：由「**发布必带 -Sign（输入42 起铁律）**」改为「**默认不签名（输入2 起；输入42 曾立「必带」已废止）**：`release-local.ps1 -Sign` 显式指定才签（CI 默认不传 -Sign）……」。
3. `docs/04-standards/packaging-upgrade-backup.md` §5 变更记录：新增 2026-09-30 输入2 行；并在 2026-09-29 输入42 行标注「后经输入2 2026-09-30 改默认关闭」。
4. `AGENTS.md` §2.3 发布规范句：由「**发布必带 `-Sign` Authenticode 签名**（…CI 已接线）」改为「**发布默认不签名，需要时传 `-Sign` Authenticode 签名**（…**CI 默认不签**，签名策略真源见 `docs/04-standards/packaging-upgrade-backup.md` §1.1）」。
5. `.agents/skills/plugin-publish-verify/SKILL.md` 第 40 行参数说明：由「**`-Sign` 为发布必带（输入42 铁律）**」改为「**`-Sign` 可选（输入2 起默认不签，需要签名时才传；输入42 曾立「必带」已废止）**……**CI 流水线默认不传 `-Sign`**（release.yml），签名策略真源 = `docs/04-standards/packaging-upgrade-backup.md` §1.1」。

## Input

无运行态输入；纯脚本/CI/文档策略变更。不新增参数、不改变已有参数语义（`-Sign` switch 缺省仍为 false）。

## Output

- 修改后 4 个文件（release.yml、packaging-upgrade-backup.md、AGENTS.md、plugin-publish-verify/SKILL.md）
- 本地验证产物：`artifacts/release/OpenForgeSelf-0.0.0-local-win-x64.zip`（不带 `-Sign` 打包）
- git：1 个 commit（只含上述 4 文件 + pilot 目录）→ push → 重打 tag

## Business Rules

- 签名 = 显式请求行为：`-Sign` 传了才签，不传不签、不自签、不生成证书
- CI 默认不签（发布主路径），本地手动发布需要签名时显式传 `-Sign`
- 签名能力（sign-publish.ps1 / 自签 / 商业证书 -PfxPath / 指纹复用）完整保留

## Boundary Conditions

- 不改 `sign-publish.ps1` 实现
- 不改 `release-local.ps1` / `package-release.ps1` / `build.ps1` 实现（已核验 `$Sign` switch 缺省 false、`if ($Sign)` 门、legacy 路径均为「默认不签」）
- 不改 `docs/09-operations/code-signing.md`（build.ps1 路径本就默认不签）
- 不提交并行会话 PILOT-050 的任何改动（AppBuilder.cs / StartupPortResolver / e2e 系列 / playwright/vite 配置 / agent-workflow.md 等 ~29 文件）

## Error Handling

- 若本地打包失败：读日志定位（TEMP 需重定向 `.forgeself\test-tmp` 规避系统 Temp 拦截）
- 若 pre-commit hook 拦截：补 PILOT 工件 00-07 八件（含关键节）后重试
- 若旧 tag 在远端存在：`git tag -f` + force push 移动（用户已授权「打 tag」）
- 若旧 CI run 仍在跑：先 cancel 再推新 tag，避免双跑

## Compatibility

- 签名能力仍可用（传 `-Sign` 即恢复，行为与输入42 一致）
- 不破坏自动更新链路（zip 结构、SHA256SUMS、RELEASE-NOTES 不变）
- 对既有本地手动签名路径（`release-local.ps1 -Sign -UpdateDir`）零影响

## Non-functional Requirements

- 文档一致性：真源 §1.1 为签名策略唯一真源，AGENTS.md / 技能只引用不重复展开
- 可验证性：成功判据全部可命令输出判定（见 Acceptance Criteria）

## Acceptance Criteria

- [ ] release.yml 无 `-Sign`（git diff 核验）
- [ ] 真源 §1.1 签名行 + §5 变更记录已更新（含输入42 标注废止）
- [ ] AGENTS.md §2.3 已更新（默认不签 + 真源引用）
- [ ] plugin-publish-verify SKILL.md 已更新（-Sign 可选 + CI 不传）
- [ ] 本地 `release-local.ps1 -Version 0.0.0-local -SkipFrontend`（不带 -Sign）：exit=0、无 Authenticode 签名步骤、出 zip
- [ ] git commit 通过 pre-commit hook（PILOT 工件链 PASS）
- [ ] push github main 成功
- [ ] 重打 tag `v2.2.2026.0930` 推送成功（旧 run 已 cancel）
- [ ] CI 重跑走完构建→打包→Release（不再卡在签名）

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| 重打已推送 tag 的外部影响 | 移动已发布 tag 可能影响他人引用 | 用户输入2 明确「打 tag 再试试」，视为已授权；汇报中说明 |
| CI 重跑结果 | 无法本地完全复现 runner 环境 | 打 tag 后 `gh run list` / `gh release view` 实证，未产出前不宣称完成 |
