# 06 Review：IM 网关会话视图方向/几何修复批（2026-10-31）

> 审查人：lead（本批实现 im-dev / 测试 im-test / 审查 lead 分离）。八问逐条 + Final Decision。

## 八问

1. **需求是否全部满足？** 满足。输入9 指认两缺陷：D1 布局比例（侧栏被长预览撑破挤压主栏）→ `min-width:0` 修复 + 压力实测 300px 恒定；D2 气泡方向（应为回复=左、来讯=右）→ 对调修复 + e2e 语义锁。版本 2.1.0→2.1.1。规范沉淀（输入11「先更新」）另账完成（DESIGN.md/B3/清单/门禁脚本），不在本目录重复。
2. **是否有未实现/简化？** 无简化。同类扫查跨插件 4 组低风险点按范围控制只登记未修（异根因/跨插件面，已入 TODO 候选小批）——符合 mini-task「登记清单回报」的约定。
3. **测试是否真实通过？** Verified：插件 build exit 0、design:check PASS 棘轮零增量、e2e 4/4（含两把新锁）、走查几何数值 6/6 达标、双主题截图读图通过、vue-tsc 0 错、check-problem-classes OK（19 PC）、Registry 5/5。
4. **失败模式覆盖？** 方向不因文本长度漂移（语义锁含 out cx < in cx 几何侧证）；长文本压力（120 字 e2e / 70+ 字走查）；seed 残留双清（e2e 哨兵 + 走查哨兵各实查 0 残留）。
5. **回归风险？** 低：改动面 = 插件 web 两文件 CSS + plugin.json + 文档/台账；零后端源码。既有 e2e 2 例不动语义仍绿。e2e 文件级 serial 化（防 shared-DB 并发互踩）为测试基建改动，已交代且不改变断言。
6. **可观测/可回滚？** 单 commit 可 revert；插件版本回退 = plugin.json + dist 重建；无数据/契约变更。
7. **文档同步？** 02-spec 修订记录（方向基准变更 + D1 登记）、ui-design-brief（§三方向/图示/版本/变更记录）、README:113、DESIGN.md/B3/清单（沉淀批）、PC-019 扩面 + case + 双索引——全链同步完成。
8. **范围控制？** Allowed 面内：Plugins/ImGateway/**（im-dev）、e2e im-gateway/**（im-test）、pilot 目录 + 台账 + 规范文件（lead）。越界 = 0（走查实例复用 e2e publish + 隔离数据根，未触共享状态）。

## 判责记录（过程中的红与修）

- **lead BOM 回归（2 红）**：edit 工具剥掉两份 .ps1 的 UTF-8 BOM，被 RepositoryScriptTests 抓住；字节级恢复 + 复跑 28/28 绿。教训已记日记（改 .ps1 后必须对照 HEAD 核字节，勿拿「看起来对」当判据）。
- **cases/README 索引缺行（1 红）**：PILOT-077 批漏登 aiagent case 行（存量腐烂被 Registry 抓住）+ 本批新 case 同需登记；两行补齐后 5/5 绿。
- e2e run1 假红：中档全量测试 testhost 占 7102（跨会话争抢新形态）——不构成代码判据，已登 TODO 协调机制项。

## Final Decision

**APPROVED**（lead 审查通过，闸门2 提交用户验收；验收通过前不 git 提交）。
