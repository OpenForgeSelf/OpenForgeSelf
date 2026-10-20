---
id: PC-006
title: PowerShell 5.1 编码/BOM/码页陷阱（范例 PC：机器守卫替代自觉的样板）
tags: [powershell, encoding, scripts, release]
invariant: 仓内 *.ps1 含非 ASCII 必须 UTF-8 with BOM；捕获外部命令（git）输出的脚本必须先切 [Console]::OutputEncoding=UTF8；一切脚本用 pwsh 跑（禁 powershell 5.1），专门复现 5.1 行为除外
status: guarded
first_seen: 2026-10-04
recurrences: []
cases: []
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/RepositoryScriptTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/RepositoryScriptTests.cs, status: active}
lens:
  - 新脚本含中文吗？文件是否 UTF-8 BOM？
  - 脚本会捕获 git/外部命令输出吗？第一次捕获前切 UTF-8 码页了吗？
  - 交付链路（release-local/make-release-notes 类）产物会不会把乱码烤进发布物？
---
# PC-006 PowerShell 5.1 编码/BOM/码页（范例归档）

> **本条是「做对的范例」**：问题最终不是靠提醒自己注意，而是靠 `RepositoryScriptTests` 仓库级守卫封堵的。规则本体真源 = `AGENTS.md` §2.3 + `docs/04-standards/agent-workflow/README.md` §B6；此处只登记类、守卫与 lens。

## 1 不变量与诱因

5.1 三实证代价（AGENTS §2.3）：① 按码页（GBK/936）写/解码 → 中文乱码进交付物；② Node spawn 无 `Cert:` 提供程序 → Authenticode 必失败；③ 靠 BOM 判定编码，UTF-8 无 BOM 被当 GBK 解析 → 解析期崩溃（update-agent 曾据此把 51888 实例弄下线）。

## 2 特征签名与扫描方法

`RepositoryScriptTests`：NonAsciiScripts 必带 BOM（Theory 逐文件）+ git log 捕获前必切码页（含「扫描集合非空」阳性对照）。既有 G3 语义 = 守卫在 dotnet test/CI 全量触发。

## 3 Canonical fix

写文件时存 UTF-8 BOM；脚本首段 `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`（在第一次捕获之前）；调用统一 `pwsh -NoProfile -ExecutionPolicy Bypass -File`。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `RepositoryScriptTests.cs`（BOM 逐文件枚举 + 码页判据） | dotnet test / CI | 非 scripts/ 树的 .ps1；5.1 专属复现例外靠人审 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| （历史散在 AGENTS §2.3/日记，守卫落地后未再复发，不再逐条补档） | 2026-10-04 | RELEASE-NOTES 整片乱码 | — |
