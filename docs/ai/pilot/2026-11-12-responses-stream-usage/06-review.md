# Review

1. 需求是否只覆盖用户要的三个字段？是。`reasoning_tokens` 留在 TODO。
2. 出站是否只在流式 Responses→Chat 注入？是。`LlmJson.Bool` 为 true 才写。
3. 同协议和反方向有没有被带上？没有。同协议不进 `ToChatRequest` 的注入；Chat→Responses 仍丢 `stream_options`。
4. 红灯是否证明缺的是请求而不是抄写？是。`stream_options` 为空时测试在读出站处失败。
5. 守卫是否锁住 18/3/21？是。G1 在 `LlmProxyPipelineTests`。
6. 有没有把参考代理的事件词表搬进来？没有。
7. 运行实例是否已含此修复？没有。需用户自己发布更新。
8. 文档是否回写到 004 §2.5 和 PC-031？是。

Final Decision: APPROVED
