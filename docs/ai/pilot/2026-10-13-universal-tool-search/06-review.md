# 06-review：万能工具搜索功能（search_tools）

## 审查八问
1. **实现是否真正满足 Intent？** 满足：search_tools 先检索（名称/描述打分 top-N），unknown 猜错名直接给 top 建议，list_tools 保留为兜底——「搜索不到工具再用列举」的用户语义在三个入口（search 工具、unknown 封套、空 keyword 报错）都成立。
2. **是否符合 Spec？** 符合。打分/排序/limit 截断/错误封套与 mini-task FR-1~4 一致；偏差两处均已记录：① unknown hint 生成失败时降级为纯引导语（FR 未写，实现补了防御）；② search_tools 的 JSON 序列化里 tools 恒在场（0 命中=空数组）而非缺省——语义无差，断言按空数组锚定。
3. **是否超出 Scope？** 无。改动面=2 插件 4 文件 + 2 测试文件；宿主 ForgeSelf.Api/**、web/、其他插件、mcp.* 外部路由未触碰（git diff 对照）。
4. **是否改了不该改的文件？** UniversalToolTests 的 FakeTool 加可选参数属支撑新断言的最小测试基建调整，旧用例零语义变化。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖：排序三态（名称前缀/精确/同分/描述弱命中/0 分排除）、limit 截断与上限夹紧、空 keyword 红线（封套无 tools 字段）、双链路 unknown 建议、registry 未挂/坏 JSON 兜底。缺口：McpCenter 侧建议文案无直测（Inferred，见 05 §3）。
6. **回归风险？** 低——unknown 封套为加字段（既有断言 Contains("unknown tool")/Contains("已注册") 不破坏，实测 42/42 与 108/108）；forwarder 错误串追加仅影响错误文案，MCP isError 语义不变。
7. **架构不一致？** 打分逻辑 AIAgent/McpCenter 两处轻量复刻（无公共工程可放），已按 C3「不引第三套机制」记录触发条件式上移（第三处需求出现再抽）。风险：两处规则漂移——已在两处注释互指缓解。
8. **Evidence 是否足以证明任务完成？** 是（构建双 0 错误 + 42/42 + 108/108 全 Verified）。浏览器链路（真实模型调 search_tools）未验，属 ②-⑥ 闭环范围。

## Findings
- Minor：McpCenter 侧建议文案建议下批补 1-2 例直测（挂在 UniversalToolForwarderTests）。
- Minor：打分是词面匹配，中文同义词（"文件" vs file）不命中——描述含中文关键词时可命中（ListFiles 描述含「读/列」类词），跨语言同义词不在本批范围。

## Final Decision: **APPROVED**（单测级；e2e/发布属后续闭环批次）
