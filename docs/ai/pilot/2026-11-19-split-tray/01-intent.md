# 01 Intent

## Problem

Linux 上能交叉编译宿主，但不能运行。原因是目标框架 `net10.0-windows` 和 `UseWindowsForms` 把 API 绑在 `Microsoft.WindowsDesktop.App` 上，这个框架没有 Linux 运行时。

## Why

云代理和其它 Linux 环境需要把 API 当 Web 宿主跑起来。托盘只在 Windows 桌面有意义，不该继续挡着 API。

## Expected Outcome

`ForgeSelf.Api` 变为 `net10.0` 的 Web API。Windows 托盘变成同版本目录里的 `ForgeSelf.Tray.exe`。交互模式下托盘「退出」停掉 API。服务模式下只关托盘。

## Constraints

不改版本号。不把 WinForms 用条件编译留在 API 里。托盘不引用 API 工程。Windows 上的菜单点击留到 Windows 环境验收。

## Success Criteria

Linux 上 `dotnet build ForgeSelf.Api` 不传 `EnableWindowsTargeting` 即为 0 error。`dotnet run -- --console` 能对监听端口返回 HTTP。管道单测能在 Linux 上把 `shutdown` 双向送到对端。
