# 06-review：通用网关 list_todos IServiceScopeFactory 复现（2026-10-17）

> Reviewer 八问（规范 §Stage 8）。审查对象 = 本批净改动（1 个测试文件 + 工件/TODO/日记）。

## 八问结论
| # | 问题 | Check | 说明 |
| --- | --- | --- | --- |
| 1 | 是否真正满足 Intent？ | **PASS** | Intent=「先复现、只加一条单测、不改业务代码」；产出即一条 `[Fact]`，用户入参/错误串逐字锚定，复现由真跑证明。 |
| 2 | 是否符合 Spec？ | **PASS** | 五步时序复刻与 AppBuilder 实际顺序一致（RegisterAllServices:364 → ProvideHostServices:402 已核对）；真实 Context/ToolRegistry/UniversalToolForwarder，零 mock 摆拍（仅外部 store 走临时目录，同现例姿势）。 |
| 3 | 是否超出 Scope？ | **PASS** | 仅新增测试文件；发现的基线红（盘符大小写 2 条）与 ModelProxy 已修复事实均只登记不顺手修。 |
| 4 | 是否改了不该改的文件？ | **PASS** | 业务代码零改动；TODO.md/日记/pilot 属流程工件，免批动作。 |
| 5 | 测试覆盖 Acceptance Criteria？ | **PASS** | AC 三条（编译过/过滤跑绿/错误逐字一致）全部在 05-evidence 有 Verified 记录。 |
| 6 | 明显回归风险？ | **PASS（低）** | 新测试无静态副作用、不写库；相邻 160 条通过。风险仅在未来修复时——本测试为特征化测试，修复后必翻红提醒改写（文件头注释已写明处置方式）。 |
| 7 | 架构不一致？ | **Minor** | 测试放 `Plugins/TodoTracker/`（缺陷归属侧）而非 `McpCenterTests/`（入口侧）——跨插件链路测试两处皆合理；根因在 TodoTracker eager 解析，归属正确。 |
| 8 | Evidence 足以证明任务完成？ | **PASS** | 命令+输出+计数均实测；「线上实例是否恰因此失败」的边界已显式标注待修复批次复核（未冒充 Verified）。 |

## Findings
- **Minor-1**：`ObservedError` 全文锚定了对 `Microsoft.Extensions.DependencyInjection` 程序集前缀的依赖——.NET 升级若改异常文案，本测试翻红属**预期行为**（文案变了=封套变了，应人工确认而非静默兼容）。不改。
- **Major-0 / Critical-0**：无。

## Risk
**L1（低）**：纯测试新增；无部署/数据/契约影响。回滚点 = 删除新增测试文件与本 pilot 目录（git 未提交，天然可回退）。

## Final Decision
**APPROVED**（待闸门2 用户验收；未获授权前不提交 git）。
