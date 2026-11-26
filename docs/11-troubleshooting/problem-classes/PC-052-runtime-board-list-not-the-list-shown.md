---
id: PC-052
title: 运行实况或来源页读到的列表，不是接口注释和页面所声称的那一份
tags: [runtime-board, api-envelope, deep-link, sems]
invariant: 运行实况每一路，以及它点进去的详情，必须消费该来源接口实际返回、并且下一步查询仍会返回的那一份。注释写了含上次结果但查询丢掉它、详情把信封当成记录、深链读的不是当前导出的页面，都算违反。
status: guarded
first_seen: 2026-11-26
recurrences: []
cases: [2026-11-26-runtime-board-list-not-shown]
allowlist: []
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Web/src/services/__tests__/apiEnvelope.test.ts"}
  - {id: S2, kind: test, path: "ForgeSelf.Web/src/services/__tests__/runtimeBoard.test.ts"}
  - {id: S3, kind: test, path: "ForgeSelf.Api.Tests/Plugins/Sems/RunnerServiceTests.cs"}
guards:
  - {level: G1, path: "ForgeSelf.Web/src/services/__tests__/apiEnvelope.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/src/services/__tests__/runtimeBoard.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Api.Tests/Plugins/Sems/RunnerServiceTests.cs", status: active}
lens:
  - 列表注释若写「含上次结果」，当前查询方法是否真的返回那份结果？
  - 详情解析是否先解开 `{ data }`，再把数字状态译成页面用的词？
  - 深链改的是路由实际导出的页面，还是已经不再挂载的旧页面？
  - 已经写下结果的卡片是否还被算进页头数字？
---

# PC-052 运行实况读到的列表不是页面所声称的那一份

## 1 不变量与诱因

运行实况把多路列表拼成一页，来源页再按查询参数打开详情。人会按注释和按钮文案相信「扫描结果还在」「详情就是这条执行」「这个地址就是当前页面」。实现若只在另一次响应里给过数据、把信封当成记录、或改了一份不再导出的页面，界面就会空着或数字不对，而接口看起来是成功的。

## 2 特征签名与扫描方法

签名是三份回归测试的路径仍然存在。它们分别锁住信封解开、写下结果后不再计入阻塞数、扫描结果留在当前列表。盲区：测试不代替浏览器截图。没有截图的状态仍以 102 的 Unknown 为准。

## 3 Canonical fix

列表查询返回与注释一致的那一份，并丢掉已经退出的扫描进程。详情先解开非空的 `data`，再映射状态。深链改路由当前导出的页面。页头数字排除已经写下结果的卡片。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
| --- | --- | --- | --- |
| G1 | `apiEnvelope.test.ts` | 前端单测 | 不覆盖每个来源的字段 |
| G1 | `runtimeBoard.test.ts` | 前端单测 | 不覆盖像素 |
| G1 | `RunnerServiceTests.cs` | 后端过滤测试 | 依赖本机 `/proc` 时才会真的启动外部进程 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
| --- | --- | --- | --- |
| 2026-11-26-runtime-board-list-not-shown | 2026-11-26 | 重新扫描的进程在下一次列表消失，脚本和工作流详情是空的，智能体深链打开的是新会话 | 否，首次归档 |
