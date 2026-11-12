# 03 Plan

1. 在 `AIProviderRegistry` 增加 `ResolveUpstreamModel`，逻辑等于原 Chat 私有方法（含别名反查）。
2. 四个控制器删除私有解析，改为调用该方法；在出站前把 `request.Model` 换成上游 id（Agent 测试接口本来就把返回值交给 SDK）。
3. `ChatTurn` 两个头字段长度改为 -1。
4. 测试：别名替换、四文件源码扫描、超长头插入。先跑超长头确认红灯，再改字段。
