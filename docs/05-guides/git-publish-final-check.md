---
title: git 发布前最终检查（Push 前清单 + 内容审计）
status: 生效
updated: 2026-09-21
适用范围: OpenForgeSelf 仓库（本地 master，历史已两轮清理）
配套工具: scripts/check-git-content.ps1
---

# git 发布前最终检查清单（Push / 开源 / 分发前必读）

> 目标：回答两个问题——**还有没有"不希望提交"的文件会被提交？还有没有敏感内容已经提交在历史里？**
> 配套脚本 `scripts/check-git-content.ps1` 自动完成大部分扫描，本清单给出人工复核与处置步骤。

---

## 一、30 秒快速开始

```powershell
cd D:\src\my-proj\OpenForgeSelf\OpenForgeSelf

# 1. 工作区/暂存层（快）：会不会把不希望的文件提交上去
.\scripts\check-git-content.ps1

# 2. 历史层（慢，首次/发布前必跑）：历史里是否还有敏感内容
.\scripts\check-git-content.ps1 -CheckHistory

# 3. 落盘报告（可留档、可 diff）
.\scripts\check-git-content.ps1 -CheckHistory -ReportFile "$env:TEMP\git-audit-$(Get-Date -Format yyyyMMdd).txt"
```

脚本退出码：`0` = 未发现；`1` = 发现需处理项；`2` = 执行错误。
命中分级：`PATH`（黑名单路径）→ 建议 gitignore 或移除；`HIGH`（随机密钥格式）→ 基本判定泄漏，必须处理；`LOW`（key=value 形态）→ 可能误报，人工复核。

> ⚠️ **已知误报（正常现象，勿慌）**：项目测试夹具使用假密钥（`sk-00000000000000000000000000000001`、`sk-hashchange1234567890`、`AesSecretEncryptionOutputTests.cs` 的 `cs-sk-<REDACTED>` 占位符）。这些是**无害测试数据**，会以 HIGH 报出属预期行为——逐条确认形如"全 0 / hashchange / <REDACTED>"即可放行。

---

## 二、检查清单（按层，全部过才可 Push）

### A. 工作区 / 暂存层 —— "会被提交的"

- [ ] **跑脚本工作区模式**：`.\scripts\check-git-content.ps1`，确认 0 项 PATH/HIGH 命中
- [ ] **未跟踪文件逐一过目**：`git status --porcelain` 中 `??` 项，确认都是要提交的新文件（或已 gitignore）
- [ ] **确认 .gitignore 覆盖**：对"不想入库"的目录，`git check-ignore -v <path>` 应返回规则（如 `.temp/`、`publish/`、`*.db`、`.forgeself/`、`specs/`）
- [ ] **暂存区检查**：`git diff --cached --stat` 确认本次提交范围正确；提交时**精确 add 目标文件**，禁止 `git add -A`（并行 Agent 工作区共存时尤为重要）

### B. 历史层 —— "已经提交的"

- [ ] **跑脚本历史模式**：`.\scripts\check-git-content.ps1 -CheckHistory`，确认 HIGH 命中**全部可解释**（测试夹具 / 已脱敏占位符）
- [ ] **黑名单路径**：脚本 `[C1]` 输出"当前仍跟踪 0"即为历史干净；若 >0，用 `git ls-files | findstr <路径>` 定位并移除
- [ ] **密钥逐条人工复核**：对每条 HIGH，看提交号 + 文件 + 行内容，确认不是真实密钥（真实密钥特征：随机字符串、非全 0、非测试占位）
- [ ] **bundle / 备份文件不进分发**：`.temp\history-rewrite-backup\*.bundle` 含**重写前完整历史（含旧密钥）**，只能留在本地 `.temp/`（已 gitignore），**绝不随仓库分发、不上传到任何网盘/远端**

### C. 远端 / 推送层

- [ ] **确认 remote 意图**：`git remote -v` —— 当前仓库无 remote。首次 add remote 前先完成 A/B 两层检查
- [ ] **force-push 警告**：历史重写后推送必须 `--force-with-lease`（不是 `--force`），且确认远端没有他人依赖旧历史
- [ ] **推送后复检**：在远端仓库（GitHub/Gitee 页面）再跑一遍脚本（clone 到临时目录后执行），确认远端实际内容无泄漏——**远端泄漏无法通过本地删除补救**（需联系平台 + 吊销密钥）

### D. 仓库配置层

- [ ] **`.git/config` 无敏感项**：检查 `http.extraheader`、`credential.helper` 不落明文 token（Windows 凭据管理器可保留）
- [ ] **提交者信息合理**：`git config user.name / user.email` —— 公开仓库会暴露邮箱，确认用预期身份
- [ ] **无残留 ref**：`git for-each-ref` 应只有 `refs/heads/master`（如有 `refs/stash`、`refs/restore/*` 等临时 ref 已清理）
- [ ] **不可达对象已清**：`git fsck --unreachable` 无输出（历史重写后 `git reflog expire --expire=now --all` + `git gc --prune=now`）

### E. 防再犯（一次性做完，长期受益）

- [ ] **.gitignore 纪律**：新增"不该入库"的文件/目录时先加 .gitignore 再决定是否提交；`*.db`、`*.log`、`.env*`、密钥类文件永远不入库
- [ ] **提交前自检习惯**：每次提交前跑工作区模式脚本（10 秒），养成肌肉记忆
- [ ] **建议加 pre-commit 钩子**（可选）：把脚本工作区模式接入 `.git/hooks/pre-commit`，命中即拒绝提交：
  ```powershell
  # .git/hooks/pre-commit（PowerShell 版）
  $r = & "D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\scripts\check-git-content.ps1"
  exit $LASTEXITCODE
  ```
  （git for windows 默认钩子 shell 是 sh，PowerShell 钩子需配置；或改用 gitleaks/trufflehog 等专业工具）

---

## 三、发现泄漏后怎么处理（简要流程）

1. **工作区/暂存层命中**：加 .gitignore 或移除文件 → 重新提交 → 复跑脚本确认 0 命中
2. **历史层命中（真实密钥）**：
   - 先备份：`git bundle create .temp\pre-fix.bundle --all` + `git diff > .temp\pre-fix-worktree.patch`
   - **绝不用 stash 做备份**（filter-repo 会重写 refs/stash 并删其中匹配路径，上次事故即因此丢失未提交文件）
   - 写替换规则（**注意文件无 BOM**，PS5.1 的 `Set-Content -Encoding UTF8` 带 BOM 会污染首行模式）：
     ```
     原密钥串==>sk-LEAK_FIXED_BY_AUDIT_YYYYMMDD
     ```
   - `python .temp\history-rewrite-backup\git-filter-repo --force --replace-text <规则文件>`
   - 恢复工作区：`git apply <patch>`（二进制文件无法用 patch 恢复，须从发布副本/其他备份复制）
   - 复跑脚本历史模式确认清除
   - **若密钥曾有效，立即吊销/轮换**（泄漏即视为泄露，替换占位符只是止血）
3. **无法自动恢复的**：如实记录，升级给人

---

## 四、本项目基线（2026-09-21 审计后）

| 项 | 状态 |
|----|------|
| 分支 | 仅 `master`（131 提交），无 remote |
| 历史黑名单路径 | 0（两轮 filter-repo 已剔：密钥/凭据/`.db`/构建产物 + 6 个 dot 目录 + `openwiki` + `specs`） |
| 已知真实密钥 | 0（`sk-638fbe…` e2e 真实 API key 已于 2026-09-21 替换为 `sk-LEAK_FIXED_BY_AUDIT_20260921`，历史 3 处） |
| 历史 HIGH 命中 | 102 条，全部为测试夹具假密钥（`sk-0000…01`、`sk-hashchange…`），无真实密钥 |
| 备份 | `.temp\history-rewrite-backup\`（pre-rewrite / pre-step2 / pre-leakfix bundle + 规则 + 脚本）——**仅限本地，禁止分发** |
| 已知未提交文件丢失 | `specs/033-home/` 立项文档（随 2026-09-20 stash 事故，待页面 Agent 重建） |
