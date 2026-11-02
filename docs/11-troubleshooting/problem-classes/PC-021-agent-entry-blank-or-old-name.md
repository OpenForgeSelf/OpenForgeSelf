---
id: PC-021
title: 智能体入口顶栏仍用旧名，或主区域因兜底路由被判冲突而空白
tags: [ai-agent, router, shell]
invariant: 宿主里智能体的可见名称是「智能体」；未匹配兜底路由不得把插件声明的单段路径判成已被占用。
status: guarded
first_seen: 2026-11-02
recurrences: [2026-11-02-agent-page-blank-old-name]
cases: [2026-11-02-agent-page-blank-old-name]
signature:
  - {id: S1, kind: grep, glob: "ForgeSelf.Web/src/stores/*tabs.ts", regex: "label: 'AI Agent'", expect: no-match}
  - {id: S2, kind: grep, glob: "ForgeSelf.Web/src/views/*SettingsView.vue", regex: "label: 'AI Agent'", expect: no-match}
  - {id: S3, kind: grep, glob: "ForgeSelf.Web/src/views/*ChatView.vue", regex: ">AI Agent<", expect: no-match}
  - {id: S4, kind: test, path: "ForgeSelf.Web/src/stores/tabs.test.ts"}
  - {id: S5, kind: test, path: "ForgeSelf.Web/src/router/__tests__/dynamicPlugins.test.ts"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Web/src/stores/tabs.test.ts", status: active}
  - {level: G1, path: "ForgeSelf.Web/src/router/__tests__/dynamicPlugins.test.ts", status: active}
lens:
  - 新增的参数化兜底路由，会不会让 isPathTaken 把同段数的插件路径判成冲突？
  - 宿主壳上的智能体文案，是不是还写着改名前的 AI Agent？
---

# PC-021 智能体入口空白或仍显示旧名

## 1 不变量与诱因

插件清单和功能表已经叫「智能体」，宿主默认标签、设置分类、旧聊天标题仍写死改名前的英文名。`openTab` 见到已有路径就返回，所以后写的正确名称盖不掉种子标签。

主区域空白是另一层：清单没注册到该路径时，`RouterView` 没有匹配组件，渲染成空注释。补一条 `/:pathMatch(.*)*` 之后，冲突检测把「路径里有冒号且段数相同」当成占用，单段的 `/ai-agent` 被挪到 `/plugin-view/ai-agent`，用户打开的地址又落到兜底页。

## 2 特征签名与扫描方法

签名只扫宿主三处写死旧名的文件，加上两个测试文件必须存在。插件包里的旧文案不在这三条正则里。盲区：动态拼接出来的标题、以及 `Plugins/**` 里的可见字符串。

## 3 Canonical fix

默认标签和设置/聊天标题改成「智能体」。兜底路由保留，但 `isPathTaken` 跳过 `route-missing` 和路径里带 `(.*)` 的记录，让插件声明路径按原样注册。插件界面产物 `web/dist/index.js` 必须先构建，远程入口才加载得到。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `tabs.test.ts` | 前端单测 | 不扫插件包内文案 |
| G1 | `dynamicPlugins.test.ts` 的兜底冲突用例 | 前端单测 | 不打开浏览器 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-02-agent-page-blank-old-name | 2026-11-02 | 顶栏「AI Agent」，主内容空注释；兜底路由一度把 /ai-agent 判成冲突 | 否（首次建档） |
