# 01 · Intent

## Problem

智能终端文档写了 URL 深链和 `st_*`，页面和宿主都没接上。堡垒机或浏览器无法唤起正在运行的铸己匣并新开一格标签。助手工具一调用就因依赖注入失败。

## Why

用户要 AI 助手能驱动智能终端，并要文档里承诺过的「按类型开标签 / 协议唤起 / 多实例把参数交给已运行实例」。

## Expected Outcome

同一套不含密码的 query，从页面、`forgeself://terminal?...` 或 `--open-terminal` 打开一格 local/ssh 标签。已有实例收到后开标签，新进程退出。`st_create_session` 能执行，并让已打开的页面附着该会话。

## Constraints

不存密码。不做 RDP/SFTP/分屏。`wsl-ssh` 与 `custom:` 只提示、不开成本机壳。不重启用户的 `:51888`。不把全部宿主工具塞进助手。

## Success Criteria

见 02 的 AC1–AC5。闸门1 依据：2026-11-12 输入5 原文要求完成开发与测试，并授权按稳定简单的方案实现。
