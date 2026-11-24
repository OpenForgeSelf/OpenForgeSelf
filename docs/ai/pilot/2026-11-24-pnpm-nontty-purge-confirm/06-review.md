# 06 Review — 发布链 pnpm install 非 TTY 清 modules 确认中断（输入34/35）

## 九问复核

| # | 问题 | 结论 |
|---|------|------|
| 1 | 实现是否满足 Intent？ | PASS——非 TTY install 不再按 TTY 确认，单插件真跑 exit=0 |
| 2 | 是否符合 Spec？ | PASS——参数只关 purge 确认，`--frozen-lockfile` 保留 |
| 3 | 是否超出 Scope？ | PASS——只改 build-frontend.ps1 一处代码 + G2 守卫 + 归档文档 |
| 4 | 是否改了不该改的文件？ | PASS——未动 release-local.ps1 / CI workflow / 全局 npmrc / lockfile |
| 5 | 测试是否覆盖 AC？ | PASS——G2 断言参数存在；单插件构建实测覆盖行为 |
| 6 | 明显回归风险？ | 低——CI 原本 `CI=true` 自动确认，参数只是显式化同一语义；有 TTY 人工场景也等价按 Y |
| 7 | 架构不一致？ | 无——沿用现有 Invoke-PnpmBuild 封装 |
| 8 | Evidence 足以证明完成？ | PASS（带限制：完整 -Sign 链未跑，已如实记入 05 Known Limitations） |
| 9 | 同类扫查与守卫落地？ | PASS——同根因命中仅本处已修；PC-048 + case + 三处索引 + G2/G3 守卫齐备 |

## Requirement / Scope / Test / Architecture Check

- Requirement: PASS
- Scope: PASS
- Test: PASS
- Architecture: PASS

## Risk

L0（低）——单点参数 + 守卫，不触碰签名逻辑、宿主进程、配置或数据。

## Findings

- Critical: 0
- Major: 0
- Minor: ① 最小 fixture 未构造出可重复 purge 红灯（pnpm 10.33.1 判定更严），已按熔断停止并以用户现场红灯补位；② 完整 `-Sign` 链未跑（沙箱配额 + 外部死锁进程，非本批引入）。两条均已在 case/05 落档，非悬空。

## 问题类归档

- 新 PC-048（guarded）+ case `2026-11-24-release-local-pnpm-nontty-purge-confirm` + 三处索引（problem-classes/cases/index README）已更新。
- 同类扫查 4 类位置：同根因 1 处（已修），异根因 3 类（CI workflow / publish-plugin-full / 文档技能示例，均不修并记入 case §4 / PC §2 盲区）。

## 重构提案

R0 不重构。理由：单点参数修复 + G2 守卫足够，无跨模块契约变更；`release-local.ps1` 主流程与 `Invoke-PnpmBuild` 封装保持不变。

## 悬空教训清单

空。所有发现均已落成 PC-048 / TODO（safe-delete 配额发布链）/ 05 Known Limitations。

## Final Decision

APPROVED（闸门2 待用户验收；未提交，闸门3 未启动）。
