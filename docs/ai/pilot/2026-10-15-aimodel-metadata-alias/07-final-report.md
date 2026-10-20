# AI-Native Pilot 结果汇报

> 全量批次收口汇报（2026-10-20 会话归档补产出 05-07；实现于 2026-10-15~16，闸门1 批准 2026-10-16）。

## 1. Repository Understanding
宿主 AI 模型层（XCode 实体 AIModel + AIProviderRegistry 路由 + UnifiedAI 网关）此前只有 `Alias/Capabilities/MaxContext` 三个可编辑元数据；对外 `/v1/models` 一律暴露 `provider:model` 原始 id，外部客户端无法用简洁稳定的主名访问。

## 2. Selected Task
追加 4 列结构化元数据（MaxOutput/SupportVision/ThinkingLevels/PublicAlias）并落地「别名即对外主名」：`/v1/models` 有别名只出别名、chat/completions 反向解析别名、别名全局唯一（大小写不敏感、禁冒号、直查库校验）。

## 3. Changed Files
见 05-evidence《Changed Files》（后端 8 + 前端 4 + 测试 2 + 文档 1）。

## 4. Validation
- `dotnet build ForgeSelf.slnx -c Release` exit 0（2026-10-20 Verified）
- `dotnet test --filter ~PersistentSessionStore|~AIModelFeature` 38/38（Verified）
- 宿主 vitest 定向 33/33（Verified）；dev-stack 走查（Documented，2026-10-15/16）
- Review 见 06：APPROVED，Risk L2

## 5. Evidence
05-evidence.md（AC-01~21 逐条映射，Verified/Documented 分级）。

## 6. Review
06-review.md；Final Decision = APPROVED。

## 7. Risk
L2——`/v1/models` 输出形态对外可见；发布链 2.4.5→2.5.x 长时间实实例运行未见回归。回滚点=本批 commit 单独 revert（无数据库迁移脚本，4 列为加列、XCode 自动建列，回滚后旧代码不读新列即无害）。

## 8. Problems Found
1. 【本批修复·收口日发现】AC-13 测试用例假阴性（两次 `Find` 取新对象，禁用未落库）——修测试非修产品；06 Minor-1。
2. 【流程】工件链滞后：ModelProxy 编译阻断暂停提交后，本批 05-07 无人回收，遗忘 4 天。
3. 【环境】全量 dotnet test 基线红家族（AIModel 表缺失等）与 10-18 TODO 台账一致，非本批引入。

## 9. Process Evaluation
Repository Understanding→Implement 达标（当时）；Evidence/Review/Persist 滞后——本次补齐；闸门1 范围纪律执行良好（「只追加 4 列」未扩）。

## 10. 问题类归档
- 建议登记（归 PC 体系「测试自身缺陷掩盖行为」小类，若后续再发「断言写后读用新对象」形态则正式建类）：本次为单例，先以 06 Minor-1 + 本条留痕，不建 PC。

## 11. 重构提案
无。`AIModelEditableFields` 重载已收敛旧四参签名，暂无进一步重复模式可抽。

## 12. 最终教训清单
- XCode `Find(...)` 每次返回**新对象**：测试里「改属性→Save→再 Find 断言」必须持有同一实例改值，或 Find 后显式校验已落库值。
- 被外部阻断暂停的批次：阻断解除后的收口清单必须扫描**所有因同一阻断挂起的批次**补工件，而非只恢复手头的批。
