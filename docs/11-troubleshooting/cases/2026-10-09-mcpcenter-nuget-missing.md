---
id: 2026-10-09-mcpcenter-nuget-missing
title: McpCenter 升级包缺第三方 DLL，宿主启动即崩
pc: PC-020
severity: P1
evidence: Verified
---
# Case：2026-10-09 McpCenter 缺第三方 DLL

> 规则见 [PC-020](../problem-classes/PC-020-plugin-dependency-unresolved.md)。当时的验证记录在 `docs/ai/pilot/2026-10-09-plugin-data-root/05-evidence.md` V9–V11。

## 1 现象

2.3.8 包里的 McpCenter 只有入口 DLL、deps.json、plugin.json 和 web，没有 `ModelContextProtocol*.dll`。宿主启动即崩：`Could not load 'ModelContextProtocol.Core'`。

## 2 复现红灯

当时的红是发布包缺文件，不是本测试。2026-11-02 用去掉 `CopyLocalLockFileAssemblies` 的 BrowserUse csproj 复现了同一守卫的红灯，日志 `.temp/tmp/plugin-load-guard-red.log`：失败 1，点名 `Plugins/BrowserUse/BrowserUse.csproj` 独有包 `Microsoft.Playwright`。

## 3 根因（5 Whys + 第 6 问）

1. 宿主为什么崩？→ 插件 ALC 按入口 DLL 旁的 deps.json 要 ModelContextProtocol，文件不在。
2. 为什么文件不在包里？→ `StageAllPlugins` 只拷插件 bin。
3. 为什么 bin 里没有？→ 类库默认 `CopyLocalLockFileAssemblies=false`。
4. 为什么没人在编译期发现？→ 插件单独 `dotnet build` 可以成功，不检查输出目录有没有依赖 DLL。
5. 为什么修完还会再犯？→ 修复只改了 McpCenter.csproj，并在 TODO 写了「以后做守卫」。
- **第 6 问：** 待办不是测试。下一个带新 NuGet 的插件不会红。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| Plugins/McpCenter/McpCenter.csproj | 是 | 2026-10-09 已加 CopyLocalLockFileAssemblies |
| Plugins/BrowserUse/BrowserUse.csproj | 是 | 2026-11-02 再犯，见同日 case |
| Plugins/Sems/Sems.csproj（System.Management） | 是 | 已有该属性，守卫扫过为合规 |
| Plugins/AIAgent Microsoft.Agents.AI | 否 | 宿主已引用 Microsoft.Agents.AI.OpenAI，默认 ALC 能解析 |
| Plugins/SystemMonitor PerformanceCounter | 否 | 宿主 csproj 已引用同一包 |

## 5 修复与被否方案

McpCenter.csproj 打开复制。被否：把第三方 DLL 拷进宿主根目录碰运气。插件 ALC 先看自己的 deps.json。

## 6 重构提案与结论

R0。复制开关加枚举测试就挡住这一类，不必改 StageAllPlugins 的排除规则。

## 7 守卫落地

2026-11-02 补上 G2。本 case 归档时守卫已是绿的。
