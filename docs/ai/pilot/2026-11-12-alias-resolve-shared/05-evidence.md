# 05 Evidence

## 生产日志（Verified）

2026-11-12 13:51 / 14:07，`C:\Users\12504\.forgeself\log\2026_11_12.log`：`模型: default, 提供者: gpustack`，`POST http://gpustack.luxvisions-inno.com/v1/chat/completions` 404。库内 `AIModel` Id=16：`PublicAlias=default`，`UpstreamModelId=qwen3_8_vision`，`Enabled=1`。

## 红灯（Verified）

修复前过滤跑 `SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText`：`Expected turn.Id to be greater than 0L, but found 0L`。日志 `.temp/tmp/alias-header-red.log`。

## 绿灯（Verified）

同一过滤复跑：失败 0，通过 3，总计 3，433 ms。日志 `.temp/tmp/alias-header-green.log`。

- AC1 `ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId` 通过。
- AC2 `GatewayModelEntries_CallSharedResolveUpstreamModel` 通过。
- AC3 `SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText` 通过。

未对正在运行的宿主做重启或真实请求。源码测试通过不等于当前进程已加载新代码。
