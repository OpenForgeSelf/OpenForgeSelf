# Review

> 阶段：Stage 8（Reviewer 视角回裁 Intent → Spec → Plan → Task → Code → Test → Evidence 全链）。
> 结论只裁事实。CHANGES_REQUIRED 时回指倒退到哪个阶段。
> 独立评审：CodeReview 子代理（current_session 全量 diff）+ 本会话自审，双重过。

## 八问（逐条回答）

1. **实现是否满足 Intent？** 是。「所有 MCP 执行默认超时改可配置、默认 5 分钟」→ ExecutionTimeoutMs 默认 300000ms，页面/config.json/env 三处可配，贯穿三传输全部外部 MCP 请求。
2. **实现是否满足 Spec？** 是。AC-1~AC-6 逐条有证据（05-evidence）：默认值/往返/校验 400/回滚/Lazy 生效/子超时常量不变（PC-010 守卫复跑绿）；并顺带修复 AC-5 单位 bug（FromSeconds→FromMilliseconds）。
3. **是否超出 Scope？** 未超。改动文件集 = 03-plan A~E 清单 + 测试/e2e/mock fixture；mock 加 `--with-slow` 开关属测试资产配套，默认关闭不改既有行为。
4. **是否修改了不应修改的文件？** 否。Controller 未动（复用既有 ArgumentException→400 通道）；宿主内核未动。
5. **测试是否覆盖 Acceptance Criteria？** 是。单测 169/169（新增 13 组 + 3 用例）、e2e 双档零容忍（5s 掐断带实际秒数 / 60s 重连放行 / 越界 400 / UI 回显换算）。
6. **是否存在隐性回归风险？** 低。传输构造新增可选参（默认值兜底）、McpClientManager 构造 config 可选参——18 处既有调用点零改动；PC-010 既有守卫全量复跑绿。
7. **是否与架构一致？** 是。同一 config 实例贯穿（Runtime 写、CreateTransport 读）与 Stateless v2.4.0 同款模板；毫秒单一存储单位、秒仅 UI 层，符合不变工程规则。
8. **Evidence 是否足以证明任务完成？** 是（Verified 级：命令输出 + e2e 证据日志 + 截图读图 + dev-stack 走查两轮实测）。

## Requirement Check

PASS

## Scope Check

PASS

## Test Check

PASS

## Architecture Check

PASS

## Risk

L1（插件内配置面扩展，无内核/数据契约变更；回滚 = 还原 14 文件回 2.5.1）

## Findings

### Critical

无。

### Major

无。

### Minor（CodeReview 提出 2 条，均已当场修复并复验）

1. **InputNumber 清空 → NaN 误提交**：save 原逻辑 `timeoutInputSec*1000 !== timeoutMsNow` 在清空时得 NaN 恒真。修复 = `Number.isFinite` 守卫 + 脏比较基准 `timeoutLoadedSec`。dev-stack 复验：清空后保存 → 后端 400「执行超时须在 1 秒~3600 秒之间」拒绝并回滚，配置零污染（仍 600000）。
2. **非整千毫秒值被静默四舍五入回写**：反算比较（`sec*1000 !== msNow`）会把 env/config 落的非整千值（如 123456ms）在改其他字段时顺带规范化回写。修复 = 与加载基准值 `timeoutLoadedSec` 做脏比较，未编辑即不提交。

### 走查期发现并已修（本批引入，非评审遗漏）

- 窄视口下新行 hint 溢出被 `overflow:hidden` 裁切 39px、输入框被挤至 84px（长英文 token 撑出 min-content）。修复 = 文案精简 + `.form-hint{min-width:0;overflow-wrap:anywhere}`；e2e UI 腿截图读图确认完整可见。

## Final Decision

**APPROVED**（门禁①②③ + 评审 Minor 闭环均有证据；④发布按硬闸门待用户授权，⑤运行实例复验在用户启用新版本后执行）。
