---
id: PC-035
title: 会话起始目录占位符（~）未解析，报错拼接解析后路径泄露宿主进程目录
tags: [smart-terminal, start-dir, path-resolution, info-leak]
invariant: 会话起始目录入参必须解析为已存在目录的绝对路径后才可用于建会话；`~`/`~/x` 展开 UserProfile、环境变量展开、不存在时报错信息只回显用户输入原文（根路径与 ~ 形态展示解析结果，相对路径不得拼接宿主进程 cwd）。解析单点 = Plugins/SmartTerminal/Services/StStartDir.cs（V1 创建与 V2 ResolveLocalCwd 共用，禁止旁路 Path.GetFullPath 直接消费用户输入）
status: guarded
first_seen: 2026-11-13
recurrences: [2026-11-15-st-ux-defects]
cases: [2026-11-15-st-ux-defects]
signature:
  - {id: S1, kind: grep, glob: "Plugins/SmartTerminal/Services/TerminalSessionService*.cs", regex: "GetFullPath\\((request\\.Cwd|cwd)\\)", expect: no-match}
  - {id: S2, kind: test, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs}
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs, status: active}
lens:
  - 新增/改动的会话创建入口（V1/V2/未来 agent 入口）起始目录是否经 StStartDir.Resolve？直接 Path.GetFullPath(cwd) = 旁路。
  - 报错信息回显的是用户输入原文还是解析后的完整路径？相对路径拼接宿主 cwd = 泄露面。
  - 前端默认值/占位符是否与后端解析语义一致（`~` 是合法输入，不是字面目录名）？
---
# PC-035 会话起始目录占位符未解析 + 报错泄露宿主路径

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。

## 1 不变量与诱因

终端/会话类功能的起始目录输入面天然接受 `~`（前端占位符即默认 `~`）。`Path.GetFullPath("~")` 在 Windows 上把 `~` 当相对路径，相对**宿主进程 cwd**（versions\<ver>\）解析 → 不存在 → 400 报错串拼接该完整路径：功能上挡死新会话创建，安全上泄露宿主部署路径。诱因：把「用户输入」当「文件系统路径」直接消费，没有占位符解析层；错误路径回显解析结果而非输入原文。本类 2026-11-13 pilot（gate-violations-fix BUG#1）分诊为延期 TODO P1，2026-11-15 用户实测命中 = 延期债到期（同类第二次出现，建类收口）。

## 2 特征签名与扫描方法

- 代码：`Path.GetFullPath(` 直接作用于**用户可输入**的 cwd/startDir 字段；报错串 `工作目录不存在` 的拼接源。
- 扫描：grep `ResolveLocalCwd|startDir|工作目录不存在` 于插件 Services；每处消费者核对是否经 `StStartDir.Resolve`。
- 对照测试（红灯锚）：`Resolve_Tilde_ReturnsUserProfile` / `Resolve_NonRootedMissingPath_MessageDoesNotLeakHostProcessCwd`。

## 3 Canonical fix

`StStartDir.Resolve`（单一真源，V1+V2 共用）：空 → UserProfile（无则 CurrentDirectory）；`~`/`~/x`/`~\x` → UserProfile 展开；其余 → 环境变量展开 + GetFullPath；不存在 → SmartTerminalException(400)，报错回显规则：`~` 前缀或根路径展示解析后完整路径（用户可自查），相对路径只回显输入原文（不拼宿主 cwd）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StStartDirAndSessionShellTests.cs`（StStartDirTests 5 例） | dotnet test | 纯单测；「新入口旁路 StStartDir」靠 lens 人工扫查 + 未来可加 G2 枚举（当前会话创建入口仅两处，枚举收益低） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| [2026-11-15-st-ux-defects](../cases/2026-11-15-st-ux-defects.md) | 2026-11-15 | 新增会话默认 `~` 确定即 400「工作目录不存在: <宿主进程目录>\~」（11-13 分诊延期债到期） | 建类前既有缺陷（分诊时守卫未立项），非守卫逃逸 |
