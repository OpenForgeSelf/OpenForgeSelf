# Intent（PILOT-074 · 2026-10-27 · openai-responses-upstream）

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。

## Problem

出站上游调用仅支持 OpenAI 兼容格式（`chat/completions`，外加 Anthropic messages 特例）。统一网关入站虽已能接收 Responses 风格请求（转成统一内部表示），但落到出站时所有上游都被按 chat/completions 语义调用——仅暴露 `/v1/responses` 的上游（Azure OpenAI `/openai/v1/`、vLLM 等）无法接入；OpenAI 官方以 Responses 为新功能优先落点的演进方向（推理模型官方推荐路径、`text.format` 结构化输出等）也无法吃到。

## Why

供应商生态已双格式化（标准调研证据见 00 工件「调研」节：Azure/vLLM 已支持 Responses，LiteLLM/openai-dotnet 均按「协议可配置/双客户端」处理）。本项目定位是「把任意上游归一给任意客户端」的统一网关，出站协议单一会持续收窄可接入上游集合；且协议面是既有核心场景（`/v1/chat/completions`、`/v1/models` 已对齐 OpenAI 规范，PILOT-068 别名机制依赖该面）。

## Expected Outcome

- 供应商可显式声明「OpenAI Responses」协议类型；声明后该 provider 的全部出站调用（聊天非流式/流式/模型列表/测试连接）走 `/v1/responses`。
- 三种入站风格经统一内部表示路由到 Responses 上游全链路可用：文本回复、流式增量、工具调用（含工具结果回填）、usage 统计。
- 既有 OpenAI/Anthropic/Custom 供应商行为零变化；零数据库迁移；零新依赖。

## Constraints

- 规范 §1 逐条对照：不修改生产环境；**不改数据库结构**（新增枚举值复用实体现有 ProviderType 字符串列，见 00-F7）；不新增大规模依赖（沿用手写 HttpClient + System.Text.Json，与现库一致）；不做无关重构（Custom 类型 URL 推导 throw 的既有缺陷只记录不顺手修）。
- 契约改动落在 `ForgeSelf.Abstractions`（architecture-design 铁律 2）。
- 默认值不变：不配置新类型 = 一切照旧（Compatibility 红线）。

## Success Criteria（可验证判据）

1. xUnit：Responses 请求构造映射断言绿（instructions/input items/扁平 function tools/`arguments` 为 JSON 字符串/`store:false`）。
2. xUnit：Responses 流式事件→统一流分片映射断言绿（output_text.delta、function_call_arguments.delta、response.completed 的 usage、error/incomplete 终止）。
3. xUnit：工厂分派与测试连接 URL 推导断言绿（新类型→新 provider 实例；裸域→补 `/v1/responses`）。
4. vitest：前端类型下拉含新选项、保存 payload 与列表 tag 正确。
5. 集成：FakeHandler mock 上游 `/v1/responses` 回真实形状包（非流式+流式），经 `/v1/chat/completions` 入站端到端拿到文本与 usage。
6. 门禁：后端 build+全量 dotnet test（中档）、前端 check+test、design:check 全绿；既有相关测试零回归。
