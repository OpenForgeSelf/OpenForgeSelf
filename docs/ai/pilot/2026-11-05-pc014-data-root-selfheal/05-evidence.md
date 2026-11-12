# 05 Evidence（task-12）

| 验收项 | 结果 | 来源 |
|---|---|---|
| 全量#1（修①后） | 3711 例 28 红/16m06s；风暴族全清零；5 红=AIProviderRegistry（未修②） | .temp/tmp/tb-full-1.log，Verified |
| 全量#2（修①+②） | 26 红/15m52s；no-such-table 5→0；时长 vs 基线 13m58s = +14.4%（<15% 预算） | .temp/tmp/tb-full-2.log，Verified |
| 风暴族两轮 0 红 | ✅ Todo/ImTaskDispatch/ImGateway/DbConfigStore/Memory | 两轮日志分族统计，Verified |
| AIProviderRegistry | #1 5 红 → #2 0 红（锚定生效） | 两轮 diff，Verified |
| 隔离健全 | 三受害族合跑 64/64 绿 5s；AIProviderRegistry 13/13 绿 | tb-filter-sanity/tb-aiprov-after.log，Verified |
| 残余尾巴 | ForgeSetting×1 + PluginFrontendFileMiddleware×2：#1 绿#2 红、隔离 260ms 绿=类序型，非 DAL 源；已记 TODO L9，lead 裁定归 db-dev task-17 | tb-isolate3.log，Verified |
