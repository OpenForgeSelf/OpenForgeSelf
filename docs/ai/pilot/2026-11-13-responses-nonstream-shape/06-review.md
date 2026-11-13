# Review

1. 这条是不是上次流式 usage 没改到的路径？是。非流式走 `ToResponsesResponse`。
2. 现场回包是否缺 AI SDK 6 必填的 id 和 annotations？是。ChatTurn 42349。
3. 参考实现有没有 message.id？有。annotations 没有，这是客户端 schema 多要的。
4. 工具调用有没有一起补 id？有。
5. 流式完成事件要不要再改？不要。那边已经有 id 和 annotations。

Final Decision: APPROVED
