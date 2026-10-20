---
id: 2026-10-18-host-request-204-empty-body
title: 宿主 UI 删除 AI 提供方「看起来没反应」——request() 对 204 空体无条件 res.json()
pc: PC-009
severity: P1
evidence: Verified
---
# Case：2026-10-18 宿主 request.ts 204 空响应体解析崩溃被 catch 吞掉

## 1 现象

e2e T020（创建 → 删除 provider → 卡片消失）在**干净单实例环境**（无并行宿主、端口矩阵空闲）确定性红：浏览器发出 `DELETE /api/ai-providers/{id}` 返回 **204**（后端确已删除），但页面卡片 5s/14 次轮询均仍在、也无「已删除」提示。用户视角 = 点删除后什么都没发生（刷新页面后条目才消失）。

前两轮排查曾把它误判为「并行实例竞用基线红」（expect.poll 直连确认后端无该项、浏览器却有 → 推断两个后端实例）；本批在端口全空的干净窗口复跑仍红，才逼出真因。

## 2 复现红灯

- `ForgeSelf.Web/e2e/ai-providers.spec.ts` T020（锚点 + 创建 → 删除 → 卡片消失）：修复前确定性红（204 断言过、`toBeHidden` 失败）。
- 修复后同用例绿（tmp-e2e-t020-fixed.log，1 passed 58.9s）；全套 11/11 绿（tmp-e2e-ai-providers-s9-allgreen.log）。

## 3 根因（5 Whys）

`ForgeSelf.Web/src/services/request.ts` 成功路径**无条件 `return res.json()`**；204 No Content 空 body → `SyntaxError: Unexpected end of JSON input` → `aiProvidersApi.remove()` reject → `AiProvidersPanel.deleteProvider()` 的空 `catch { /* 用户取消 */ }` 把请求异常**误当用户取消吞掉** → `loadProviders()` 不执行、成功提示不发。

- Why1 卡片不消失？删除后列表未刷新。Why2 未刷新？remove() 抛异常走了 catch。Why3 抛异常？res.json() 解析空 body。Why4 空 body？后端 Delete 返回 204 NoContent。Why5 为什么宿主会犯？插件侧 AIAgent `web/src/http.ts:265-267` 早已踩过并修复（注释原话点名「204 直接 return undefined 避免 Unexpected end of JSON input」），但该规律**只落在那个文件里，没有上浮为类**——宿主 services 层与其余插件各写各的封装，宿主漏网。
- **第 6 问**：为什么之前没暴露？此前 T020 跑在共享数据目录恰有其他 provider 的实例上，删除的不是「唯一默认」不触发 400，且曾经某轮列表接口返回过带 body 的形态/断言时序掩盖了缺口；e2e 间歇红时又被「环境竞用」假设吸收了（见 §5 逃逸分析）。

## 4 同类扫查命中表（2026-10-18，正则 `status === 204|status: 204|NoContent` 全域封装层）

| 位置 | 有 204 守卫？ | 处置 |
|------|---------------|------|
| ForgeSelf.Web/src/services/request.ts（宿主，本 case） | ❌（唯一漏网） | 已修：`if (res.status === 204) return undefined as T`（L55）+ 守卫单测 `__tests__/request.test.ts` 3/3 |
| Plugins/AIAgent/web/src/http.ts:267 | ✅（历史已修，即本类首例） | 无需改 |
| Plugins/McpCenter/web/src/http.ts:44 | ✅ | 无需改 |
| Plugins/Home/web/src/http.ts:266 | ✅ | 无需改 |
| Plugins/TodoTracker/web/src/http.ts:108 | ✅（return {}） | 无需改 |
| Plugins/DesignSystem/web/src/http.ts:112 | ✅ | 无需改 |
| Plugins/AgentHub/web/src/http.ts:61 | ✅ | 无需改 |
| Plugins/ImGateway/web/src/http.ts:36 | ✅ | 无需改 |
| Plugins/FileTools/web/src/services/fileToolsFoldersApi.ts:35 | ✅ | 无需改 |
| ForgeSelf.Web/src/services/authFetch.ts | 不适用（返回原生 Response，调用方自解析） | 无需改 |

后端侧 204 端点全量（`return NoContent\(\)|StatusCode\(204\)`）：宿主仅 AIProviderController.Delete 1 处；插件 6 处（TodoTracker×2、TodoDispatch×1、CostScope×2、AIAgent×1）——CostScope 无 web 前端（MCP 工具插件），其余插件前端均已守卫。

## 5 逃逸/误判分析

- 本 case **不是守卫后逃逸**（首见建类）；但含一次**诊断误判**：间歇红被归因「并行实例竞用」而未在第一次干净环境复跑逼认真因——「竞用」假设可解释 poll 与卡片分打两处的表象，但它**可被廉价证伪**（端口矩阵全空窗口复跑，成本 1 分钟）。教训入 PC-009 lens。
- 另一子型：`deleteProvider` 空 catch 把「ElMessageBox 取消」与「请求异常」混为一谈（不同根因，范围控制进 TODO，见 TODO.md「catch 语义拆分」）。

## 6 教训与上浮

- 规律：任何 fetch 封装的成功路径**禁止无条件 `res.json()`**；204/空体必须先返回 undefined（参考实现 `ForgeSelf.Web/src/services/request.ts:52-56`）。
- 已上浮：PC-009（本类）+ G1 vitest 守卫 + G3 e2e T020 实例守卫；类级 G2（全域封装枚举）planned 见 PC-009 guards。
- 「插件侧已修的坑未上浮为类 ⇒ 宿主同坑复发」再次成立（同 PC-005 §3 第 6 问模式）：插件↔宿主同型基建代码的修复必须做双向扫查。
