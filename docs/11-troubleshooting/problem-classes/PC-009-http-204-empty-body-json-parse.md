---
id: PC-009
title: HTTP 封装成功路径无条件 res.json()，204/空响应体解析崩溃被上层 catch 吞成「无操作」
tags: [frontend-fetch, http-envelope, empty-body]
invariant: 一切 fetch 封装（宿主 services/request.ts 与插件 web/src/http.ts 族）的成功路径禁止无条件 res.json()；204/空 body 必须显式返回 undefined/{}，且不得被调用方 catch 与「用户取消」混淆
status: guarded-partial
first_seen: 2026-10-18
recurrences: [2026-10-18-host-request-204-empty-body]
cases: [2026-10-18-host-request-204-empty-body]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Web/src/services/__tests__/request.test.ts}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Web/src/services/__tests__/request.test.ts, status: active}
  - {level: G3, path: ForgeSelf.Web/e2e/ai-providers.spec.ts, status: active}
  - {level: G2, path: none, status: planned, todo: PC-009 G2 类级守卫：枚举宿主与插件全部 fetch 封装，成功路径 JSON 解析者必含 204/空体分支, expires: 2026-12-31}
lens:
  - 新增/改动 fetch 封装时，204/205/空 body 分支在哪？成功路径是裸 res.json() 吗？
  - 后端新端点返回 NoContent() 时，其前端调用方（含插件 web）解析层验证过空体吗？
  - catch 是否把「用户取消确认框」与「请求异常」混吞？失败在 UI 上有无提示？
  - e2e 间歇红归因「环境竞用」前，是否在端口矩阵全空的干净窗口复跑证伪过？
---
# PC-009 fetch 封装对 204/空响应体无条件 JSON 解析

> 规则正文在此；事件经过见 `../cases/2026-10-18-host-request-204-empty-body.md`。首例其实是插件侧（AIAgent `web/src/http.ts:265-267`，2026 年已修并写了注释），但当时**未上浮为类**，宿主 `services/request.ts` 同坑漏网——本 PC 把该规律登记为跨宿主/插件的不变量。

## 1 不变量与诱因

后端 204 No Content（DELETE 类端点惯例）响应体为空。`res.json()` 解析空串抛 `SyntaxError: Unexpected end of JSON input`；若上层再惯用「空 catch = 用户取消」语义（ElMessageBox 场景），异常被吞，UI 呈现「点了没反应」——数据实际已变更，仅界面不同步。认知缺口：`fetch` 不像 XCode/ASP.NET 客户端那样帮你区分有无 body；`res.ok` 覆盖 2xx 全部（含 204），开发者容易把「ok」等同于「有 JSON」。

## 2 特征签名与扫描方法

- S1（kind: test）：`ForgeSelf.Web/src/services/__tests__/request.test.ts` 锚定宿主封装三态（204→undefined 且不调 json / 200→解包 / 400→抛错透传 message），修复回退即红。
- 已知盲区：插件侧各 `http.ts` 无统一枚举守卫（G2 planned）；扫描方法见 case §4 命中表正则（`status === 204|status: 204|NoContent` 对封装层、`return NoContent\(\)|StatusCode\(204\)` 对后端端点），新增 PC 时先跑这两条。

## 3 Canonical fix

```ts
if (res.status === 204) return undefined as T;   // 置于 !res.ok 错误分支之后、res.json() 之前
return res.json();
```
参考实现：`ForgeSelf.Web/src/services/request.ts:52-56`；插件侧同型 9 处（case §4 表）。被否方案：给 res.json() 包 catch 返回 undefined——会把真正的 JSON 格式错误（后端 200 但 body 坏）也吞成 undefined，诊断力丢失，拒绝。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Web/src/services/__tests__/request.test.ts` | pnpm run test / CI | 只锚宿主封装 |
| G3 实例 | `ForgeSelf.Web/e2e/ai-providers.spec.ts` T020 | e2e 批次 | 只锚 ai-providers 删除动线 |
| G2 planned | none（枚举宿主+插件全部封装源码扫描，形态参考 ProblemClassEnumerationTests） | 未落地 | 见 TODO.md 条目（expires 2026-12-31） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-18-host-request-204-empty-body | 2026-10-18 | 删除 AI 提供方后端已删 204，UI 卡片不消失无提示 | 否（首见建类；插件侧先例未建 PC 致宿主漏网，属「规律未上浮」型复发土壤） |
