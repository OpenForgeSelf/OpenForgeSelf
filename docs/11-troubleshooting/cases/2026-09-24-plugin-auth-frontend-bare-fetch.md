---
id: 2026-09-24-plugin-auth-frontend-bare-fetch
title: PluginController 鉴权后，前端插件管理页/远程视图裸 fetch 全 401 呈空态
pc: PC-002
severity: P1
evidence: Verified
---
# Case：2026-09-24 鉴权改造漏掉前端裸 fetch

> 规则/修法/扫描方法见 `../problem-classes/PC-002-auth-frontend-bare-fetch.md`，本档只讲这一次发生了什么。

## 1 现象

`PluginController` 接入鉴权（ApiKeyPolicy，Bearer `forge_api_token`）后，插件管理页与插件远程视图渲染成空态；`pluginApi.ts`/`pluginManifestApi.ts` 的裸 `fetch()` 全部 401。空态曾被误判为「浏览器环境问题」。出处：`.agents/skills/plugin-publish-verify/references/troubleshooting.md` §坑 8（2026-09-24 新增，Verified）。

## 2 复现红灯

浏览器打开插件管理页 → Network 面板对 `/api/plugin/**` 请求返回 401（请求头无 Authorization）。当时以人工实测定位（Verified，坑 8 记录）；机器守卫为 2026-10-17 本批补建（G3 枚举测试 + 签名扫描）。

## 3 根因（5 Whys + 第 6 问）

后端加 `[Authorize]`/策略是契约变更，而前端调用方散在多个文件用裸 `fetch`，未被同批排查。
- Why1 401？请求不带 Bearer。Why2 不带？调用点绕过带 token 的封装。Why3 为什么绕过？这些文件写成裸 fetch 在先，鉴权加在后。Why4 为什么加鉴权时没扫到？验收只看后端 401/200，没有「grep 前端全部调用方」的强制步骤。Why5 为什么没有？流程里没有 Stage 问「鉴权变更的消费方都有谁」。
- **第 6 问**：教训只进了坑 8 散文 + e2e-testing 技能文字；无索引（问题类台账当时不存在）、无守卫（不跑 e2e 就没人知道）→ 归因「找不到索引 + 无守卫强制」。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `ForgeSelf.Web/src` pluginApi.ts / pluginManifestApi.ts（本 case） | 是 | 已修：改走 `services/authFetch.ts`（坑 8 修复记录） |
| 2026-10-17 建类时全量预扫：UpdatePanel.vue、DevToolsView.vue、四插件 `web/src/http.ts` | 否（均有文件级 token 供给证据） | G3 测试/扫描判据=文件级证据，存量绿 |

## 5 修复与被否方案

修法 = PC-002 canonical fix（宿主 authFetch / 插件 http.ts buildHeaders）。被否：全局 fetch 拦截器注入 token（掩盖第三方 URL 泄漏 token 风险，authFetch 明确禁跨域）。

## 6 重构提案与结论

R0 不重构——封装入口已存在（authFetch.ts），缺的是「强制走入口」的守卫，已以 G3 补齐。

## 7 守卫落地

G3 `ProblemClassEnumerationTests.FrontendBareFetchToAuthedApi_MustCarryToken`（active，2026-10-17）；G3 `scripts/check-problem-classes.ps1` 签名 S1 + baseline（active，2026-10-17）；实例守卫 e2e `plugin-store` 鉴权回归（既有）。

## 8 逃逸分析

N/A 首次（2026-09-24 首见时问题类体系尚不存在）。

## 9 基建改进

把「后端加鉴权必须扫前端调用方」从坑 8 散文升级为提交期可执行守卫（G3），并入 PC-002 lens 供 Spec Stage 2 Failure Modes 预填。

## 10 复用赋能

任何给 `/api/**` 加 `[Authorize]`/策略的任务，Spec 必答 PC-002 lens；新前端请求代码 Review 对照「有没有绕过 authFetch/http.ts 的裸 fetch」。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 现象与修复 | Verified | troubleshooting.md §坑 8 |
| 存量调用方合规（baseline 绿） | Verified | 2026-10-17 预扫描 + G3 测试首跑 |
