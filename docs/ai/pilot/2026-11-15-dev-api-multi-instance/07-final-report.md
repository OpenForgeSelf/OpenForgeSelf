# AI-Native Pilot Result（最终汇报）

## 状态

PARTIALLY_COMPLETED：根 `pnpm dev` 已切换为启动整栈且双端运行验证通过；自动化测试受 testhost 权限异常阻塞，闸门2未完成。

## 1. Repository Understanding

确认仓库已有 `scripts/dev-stack.ps1` 负责启动 API 与前端，使用固定 `.temp/dev-stack.json` 管理单套实例；重复启动可能停止状态文件记录的实例。因此最终根入口应复用该脚本，而非新增 API-only 生命周期。

## 2. Selected Task

根 `pnpm dev` 启动宿主前后端，`pnpm run dev:web` 保持前端-only；移除早期试验增加的 API-only 启动脚本。

## 3. Changed Files

`package.json`、`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`、`AGENTS.md`、`docs/README.md`、PC-007 case/登记及本 pilot 工件；`scripts/dev-api.ps1` 已删除。未提交或发布。

## 4. Validation

- 整栈运行：✅ Verified，后端 `http://localhost:7301/api/health` HTTP 200，前端 `http://localhost:7399` HTTP 200；状态文件记录两端地址及 PID。
- 测试项目构建：✅ Verified，28 projects、0 errors、458 warnings。
- G2 定向测试：⚠️ Blocked，testhost `OpenProcess` 报 `Win32Exception (5)`，断言未执行。
- 全量后端测试：⚠️ Blocked，尚未获得可用执行环境；不得视为通过。
- PC-007 问题类守卫：✅ Verified，1 个 PC 的 signature/allowlist/guards 全部通过。
- Pilot 工件门禁：✅ Verified，00-07 工件及关键章节齐全。
- 提交卫生：✅ Verified，暂存内容 0 项、0 命中；拒绝列表缺失，L2 按守卫提示跳过。
- `git diff --check`：✅ Verified，无输出。

## 5. Evidence

详细实测记录见 [05-evidence.md](05-evidence.md)；问题类复用记录见 [2026-11-16-dev-api-multi-instance.md](../../../11-troubleshooting/cases/2026-11-16-dev-api-multi-instance.md)。

## 6. Review

见 [06-review.md](06-review.md)，Final Decision = BLOCKED。等待 testhost 环境恢复及用户闸门2验收。

## 7. Risk

G2 与全量后端测试未验证。`dev-stack.ps1` 是固定状态文件管理的单实例整栈入口，重复运行可能先停止状态文件记录的实例。

## 8. Problems Found

早期 API-only 入口与既有整栈启动器职责重复，最终按用户要求移除；本次将根命令接回 `dev-stack.ps1`。testhost 父进程访问拒绝仍未解决，继续由 TODO 跟踪。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Runtime Test | PASS |
| Runtime Test → Evidence | PASS |
| Automated Test → Review | BLOCKED |

### 9.5 本批自复盘

- 根开发入口应优先复用已存在且职责匹配的整栈脚本；API-only 并行能力不应在未确认需求时替换默认开发体验。
- 自动化测试框架启动异常必须和断言结果分开记录；真实运行探活不能替代 G2 与全量测试门禁。

## 10. 问题类归档

PC-007；case：`docs/11-troubleshooting/cases/2026-11-16-dev-api-multi-instance.md`。PC registry、cases registry 与 `index.md` 已登记，机器检查 PASS。

## 11. 重构提案

R0：不增加新的启动管理器或公共接口。根入口复用 `dev-stack.ps1`，保持其既有单实例生命周期。

## 12. 悬空教训清单

- 在 testhost 可监控父进程的环境重跑 G2 定向测试和全量后端测试。
- 闸门2 尚待用户验收；在阻塞解除前不提交、不发布。