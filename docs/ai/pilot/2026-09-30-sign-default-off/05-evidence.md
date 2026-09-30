# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

## Task

PILOT-051（2026-09-30-sign-default-off）

## Changed Files

- `.github/workflows/release.yml`：Build 步骤去 `-Sign`，注释改「输入2：CI 默认不签名；需要签名时在此行追加 -Sign」
- `docs/04-standards/packaging-upgrade-backup.md`：§1.1 签名行改默认不签；§5 输入42 行标注废止 + 新增 2026-09-30 行
- `AGENTS.md`：§2.3 发布规范句改「发布默认不签名，需要时传 -Sign；CI 默认不签，真源 §1.1」
- `.agents/skills/plugin-publish-verify/SKILL.md`：第 40 行 `-Sign` 改可选/默认不签/CI 不传
- `docs/ai/pilot/2026-09-30-sign-default-off/`：00-07 八件工件

## Build

Command:

```powershell
powershell -File scripts/release/release-local.ps1 -Version 0.0.0-local -SkipFrontend
```

Result: PASS（来源等级：Verified）

```text
# 关键输出（.forgeself/memory/rel-nosign-0930.log 摘要）
exit code: 0
"ALL DONE"（耗时 431s）
artifacts/release/ 产出：OpenForgeSelf-0.0.0-local-win-x64.zip (102.1 MB) + SHA256SUMS.txt + RELEASE-NOTES
日志检索 "sign|签名|Authenticode"：仅命中 csproj 还原/编译行（依赖名 XCode.SQLite 等），
无 Authenticode 签名步骤、无证书生成步骤 —— 确认默认不签生效
```

## Unit Test

Result: N/A（依据：纯 CI/文档策略变更，无代码改动，无单元测试面）

## Integration Test

Result: N/A（依据：无代码变更，无集成测试面）

## E2E

Result: N/A（依据：无前端/插件行为变更）

## Static Analysis

Result: N/A（依据：未改代码；git diff 核验改动范围见下）

```text
git diff --cached --name-only（提交前核验）：
.agents/skills/plugin-publish-verify/SKILL.md
.github/workflows/release.yml
AGENTS.md
docs/04-standards/packaging-upgrade-backup.md
（+ docs/ai/pilot/2026-09-30-sign-default-off/ 00-07 八件）
—— 仅含本任务文件，无并行会话 PILOT-050 改动（来源等级：Verified）
```

## Screenshots

N/A（无 UI 变更）

## Known Limitations

- CI runner 环境（自签证书生成卡死）无法本地完全复现；本地验证证明「不带 -Sign 不触发签名步骤」，CI 复跑结果待 push 后实证
- 打包产物为 `0.0.0-local` 本地验证版本，非发布版本

## Unresolved Issues

- CI 复跑结果：尚未执行（依赖 git 提交 → push → 重打 tag），完成后以 `gh run list` / `gh release view` 实证
- 旧 run 36664225915：需在重推 tag 前确认已 cancel，避免双跑
