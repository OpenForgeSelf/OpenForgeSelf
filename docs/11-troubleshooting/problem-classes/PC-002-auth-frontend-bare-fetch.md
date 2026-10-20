---
id: PC-002
title: 后端控制器加鉴权后，前端裸 fetch 未同步带 token（401 空态被误判为环境问题）
tags: [auth, frontend-fetch, api-contract]
invariant: 一切指向需鉴权端点（/api/**）的前端请求必须经带 Bearer 的通道（宿主 authFetch.ts/request.ts 或插件 http.ts buildHeaders）；后端给控制器加 [Authorize] 的同批必须扫完全部前端调用方
status: guarded
first_seen: 2026-09-24
recurrences: [2026-09-24-plugin-auth-frontend-bare-fetch]
cases: [2026-09-24-plugin-auth-frontend-bare-fetch]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.Web/src/**/*.{ts,vue}", regex: "fetch\(\s*[\x27\x60\x22]/api/", expect: any-match-with-file-level-token-check}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs}
allowlist:
  - {path: "ForgeSelf.Web/src/views/DevToolsView.vue", reason: "目标端点 /api/devtools 匿名（DevToolsController 无 [Authorize]，鉴权按控制器逐个标注非全局），签名判据仅限需鉴权端点；若该控制器加鉴权须同批改 authFetch", expires: 2026-12-31}
guards:
  - {level: G3, path: ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs, status: active}
  - {level: G3, path: scripts/check-problem-classes.ps1, status: active}
lens:
  - 本次给控制器/端点加 [Authorize] 了吗？同批是否 grep 过前端全部 `fetch(` 调用方（宿主 services + 每个插件 web/src）？
  - 新前端请求走的是 authFetch/request.ts（或插件 http.ts）吗？有没有绕过封装的裸 fetch？
  - 401/403 的空态在页面上是可解释错误还是「看起来像环境坏了」？（e2e 是否断言非空态，参照 plugin-store.spec.ts 鉴权回归守卫）
---
# PC-002 后端加鉴权、前端裸 fetch 未同步

> 规则正文在此；事件经过见 `../cases/2026-09-24-plugin-auth-frontend-bare-fetch.md`。技能侧操作细节（鉴权改造四步清单）归 `e2e-testing/SKILL.md`「鉴权改造后必查前端 fetch 调用方」节——本文件不复述步骤，只载不变量与守卫。

## 1 不变量与诱因

宿主 `GET/PUT/POST /api/**` 走 `ApiKeyPolicy`（Bearer `forge_api_token`）。后端加鉴权是**契约变更**，消费方（前端裸 `fetch`）不同批适配即全站该端点 401。诱因：只验后端 401/200 就收工；vite dev 不报类型、页面把 401 渲染成空态，曾被误判为「浏览器环境问题」。

## 2 特征签名与扫描方法

- G3 枚举测试：`ProblemClassEnumerationTests.FrontendBareFetchToAuthedApi_MustCarryToken`——扫 `ForgeSelf.Web/src` 与 `Plugins/*/web/src`（含 `ForgeSelf.Api/Plugins`）中直接 `fetch(<字面量/API常量>)` 的文件，要求文件级存在 token 供给证据（`authFetch|request.ts 封装|buildHeaders|Authorization`），否则红。
- 盲区：经 axios 式封装但封装本身漏 token——靠 `authFetch.ts`/`http.ts` 自身单测锚定；运行时 401 由 e2e plugin-store 守卫兜底。

## 3 Canonical fix

宿主侧改走 `ForgeSelf.Web/src/services/authFetch.ts`（自动带 Bearer，与 request.ts 同键；跨域/第三方 URL 禁用）；插件侧走插件 `web/src/http.ts` 的 `buildHeaders`。参考实现：坑 8 修复记录（`.agents/skills/plugin-publish-verify/references/troubleshooting.md` §坑 8）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G3 | `ProblemClassEnumerationTests.cs`（源码扫描） | dotnet test 全量 / CI | 第三方 URL、封装内漏注入 |
| G3 | `check-problem-classes.ps1`（签名 S1 + baseline） | pre-commit 触碰台账时 | 同上 |
| 实例 | `e2e/plugins/plugin-store`（鉴权回归守卫） | e2e 批次 | 只覆盖插件管理页 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-09-24-plugin-auth-frontend-bare-fetch | 2026-09-24 | PluginController 鉴权后插件管理页/远程视图空态（裸 fetch 全 401） | 否（首见，当时无守卫；守卫为本批 2026-10-17 补） |
