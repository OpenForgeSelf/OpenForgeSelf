# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-051（2026-09-30-sign-default-off）

## Files To Change

- file: `.github/workflows/release.yml`
  reason: CI 发布主路径强制 `-Sign` 是卡死根因（输入42 遗留），改为默认不签
- file: `docs/04-standards/packaging-upgrade-backup.md`
  reason: 打包/升级规则唯一真源，§1.1 签名行 + §5 变更记录需同步（输入42 行标注废止 + 新增 2026-09-30 行）
- file: `AGENTS.md`
  reason: §2.3 发布规范句「发布必带 -Sign（CI 已接线）」与 CI 实际行为矛盾，改为默认不签 + 真源引用
- file: `.agents/skills/plugin-publish-verify/SKILL.md`
  reason: 第 40 行「-Sign 为发布必带（输入42 铁律）」需改为可选/默认不签 + CI 不传
- file: `docs/ai/pilot/2026-09-30-sign-default-off/`（00-07 八件）
  reason: AI-Native 闭环工件链，pre-commit hook 门禁要求

## Implementation Steps

1. 改 `.github/workflows/release.yml` Build 步骤：`run:` 去 `-Sign`，注释改「输入2（2026-09-30）：CI 默认不签名。需要签名时在此行追加 -Sign…」
2. 改 `docs/04-standards/packaging-upgrade-backup.md` §1.1 签名行：默认不签 + `-Sign` 参数指定 + CI 默认不传
3. 改 `docs/04-standards/packaging-upgrade-backup.md` §5：2026-09-29 输入42 行标注「后经输入2 2026-09-30 改默认关闭」；新增 2026-09-30 输入2 变更行
4. 改 `AGENTS.md` §2.3 发布规范句：默认不签名 + 需要时传 `-Sign` + CI 默认不签 + 真源引用
5. 改 `.agents/skills/plugin-publish-verify/SKILL.md` 第 40 行：`-Sign` 可选 + 默认不签 + CI 不传（第 37 行一键跑示例保留 `-Sign -UpdateDir` 不动）
6. 本地验证：`powershell -File scripts/release/release-local.ps1 -Version 0.0.0-local -SkipFrontend`（不带 -Sign，TEMP 重定向 `.forgeself\test-tmp`）→ 确认 exit=0、无 Authenticode 签名步骤、出 zip
7. 补 PILOT 工件 00-07 八件
8. 选择性暂存 4 文件 + pilot 目录 → commit（pre-commit hook 校验）→ push github main → 重打 tag `v2.2.2026.0930` force push → CI 复跑验证

## Test Plan

1. 本地打包实测（不带 `-Sign`）：无签名步骤 + zip 产出（见 Verification / Other Checks）
2. git 链路：`git diff --cached --name-only` 核对暂存清单（只含我的 4 文件 + pilot 目录）
3. pre-commit hook：`scripts/verify-pilot-artifacts.ps1 -TaskId 2026-09-30-sign-default-off` 直接跑 PASS
4. CI：push tag 后 `gh run list` / `gh release view v2.2.2026.0930`

## Verification

### Build

```powershell
# 本任务不改代码，无需 dotnet build；打包脚本链路已由 release-local.ps1 实测覆盖
```

### Unit Test

```powershell
# N/A：纯 CI/文档策略变更，无单元测试面
```

### Integration Test

```powershell
# N/A：无代码变更，无集成测试面
```

### E2E

```powershell
# N/A：无前端/插件行为变更
```

### Other Checks

```powershell
powershell -File scripts/release/release-local.ps1 -Version 0.0.0-local -SkipFrontend
# 判据：exit=0；日志检索 "sign|签名|Authenticode" 无命中（除 csproj 还原/编译行外）；artifacts/release/ 出 zip
powershell -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-09-30-sign-default-off
# 判据：PASS: 全部 PILOT 工件链齐全
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-09-30 | AGENTS.md 提交边界 | 计划直接暂存 AGENTS.md | 工作区混有并行会话 PILOT-050 大批改动 → 备份工作区版 → checkout 恢复 HEAD → 仅应用我的行 → 暂存 → 恢复工作区备份（并行改动保留在工作区不提交） |
