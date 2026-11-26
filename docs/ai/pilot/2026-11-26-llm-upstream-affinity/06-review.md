# 06 Review

## 九问（摘要）
1. 范围：仅代理出站粘滞，未改 OAuth/Responses 线。
2. 判定先验：host/名/模型，无响应探测。
3. 扩展：新厂商实现 `ILlmUpstreamAffinity` 注册即可。
4. 安全：ForbiddenHeaders 挡住 Authorization。
5. 测试：策略单测 + 管道集成，原管道回归同跑。

## Final Decision
APPROVED
