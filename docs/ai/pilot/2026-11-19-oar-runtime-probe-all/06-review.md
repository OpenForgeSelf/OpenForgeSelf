# Review — Runtime 库存「一键探测」

## 八问

1. **需求是否满足？** 是：一键对当前 Runtime 列表真实 probe，与「刷新」分离。
2. **实现是否最小？** 是：仅前端循环既有 `probeRuntime`；未加 batch 端点。
3. **交互是否诚实？** 是：失败会计入聚合；not_found 仍算 probe 成功（API 返回真相），卡上保留安装态徽标。
4. **并发是否可控？** 是：固定上限 3。
5. **文档是否同步？** 是：032 §6.1 + ui-design-brief。
6. **测试证据？** 插件 web build Verified；真机走查+截图 Verified；正式 e2e 用例未扩 Unknown。
7. **范围越界？** 无：未改 Controller/Worker/Agent 探测。
8. **提交卫生？** 文档无个人绝对路径入库意图；截图路径仅在 Evidence/artifacts。

## Final Decision

**APPROVED**（实现与证据充分，可提交；合并 main 另受栈 CI/权限约束）。
