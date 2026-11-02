---
id: PC-020
title: 插件依赖在宿主加载期解析不到（NuGet 未进 bin，或控制器构造注入宿主契约）
tags: [plugin-load, nuget, controller-di]
invariant: 插件独有的第三方包必须复制到插件输出目录；插件控制器只能构造注入插件自有服务和 IContext，宿主契约在请求内 ctx.Get
status: guarded
first_seen: 2026-10-09
recurrences: [2026-10-09-mcpcenter-nuget-missing, 2026-11-02-browseruse-and-approval-di]
cases: [2026-10-09-mcpcenter-nuget-missing, 2026-11-02-browseruse-and-approval-di]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs, status: active}
lens:
  - 这个插件的 PackageReference 里有没有宿主 csproj 没有的包？有的话 csproj 是否写了 CopyLocalLockFileAssemblies=true？
  - 插件控制器构造函数参数里有没有 HostProvidedServiceContracts 里的类型（IApprovalService、ITurnTelemetryQuery 等）？
  - 修法是不是「构造只收 IContext，用时 Get」，而不是把宿主契约注册进插件子容器？
---
# PC-020 插件依赖在宿主加载期解析不到

> 事件经过见 cases。本条与 PC-005 相邻：PC-005 管构造期提前解析或解析到错误容器；本条管「程序集文件不在插件目录」和「控制器构造参数在插件子容器里根本没有」。

## 1 不变量与诱因

类库默认不复制 PackageReference。宿主 `StageAllPlugins` 只拷插件 bin，并排除顶层 ForgeSelf.* / NewLife.*。插件独有 DLL 不在 bin，映射控制器时 `FileNotFoundException`，开发栈后端未就绪或发布后宿主起不来。

插件控制器由插件子容器激活，子容器只有插件自己注册的服务和 `IContext`。构造函数写宿主契约，激活抛 `Unable to resolve service for type ...`，HTTP 500。

诱因：类库的默认行为和普通 ASP.NET 控制器构造注入，两处都和插件加载模型相反。2026-10-09 只留了待办，没有会失败的测试，2026-11-02 原样再犯。

## 2 特征签名与扫描方法

G2 测试读全部插件 csproj：包名不在宿主包集合、也不是 NewLife.* / ForgeSelf.* 前缀时，必须有 `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`。宿主包的更具体名称（如 `Microsoft.Agents.AI.OpenAI` 覆盖 `Microsoft.Agents.AI`）算宿主已提供。

同一测试读 `PluginManager.HostProvidedServiceContracts`，扫 `Plugins/**/Controllers/*.cs` 的构造参数。`IServiceProvider` 除外：容器自己会提供它，注入它不是这条 500（它拿到的是插件容器，归 PC-005）。

盲区：deps.json 里的传递依赖没有逐个核对文件是否在发布目录；同目录回退能掩盖「忘了复制、但碰巧 bin 里还有旧 DLL」。发布 zip 内容仍要解包看。

## 3 Canonical fix

独有包：该插件 csproj 加 `CopyLocalLockFileAssemblies=true`，编宿主后看 `Plugins/<X>/` 里有那份 DLL。参考 `Plugins/McpCenter/McpCenter.csproj`、`Plugins/BrowserUse/BrowserUse.csproj`。

控制器：构造只收 `IContext`，用时 `_ctx.Get<T>()`，不把实例存成字段。参考 `Plugins/AIAgent/Controllers/ApprovalsController.cs`。

被否：把宿主契约再注册进每个插件子容器。那是第二条解析通道，和「子容器不含宿主契约」相反。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/Architecture/PluginLoadDependencyGuardTests.cs` | 每次 `dotnet test` 扫到该类 | 不核对发布 zip 里的传递 DLL 文件 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-10-09-mcpcenter-nuget-missing | 2026-10-09 | 升级后宿主起不来，缺 ModelContextProtocol | 否（当时尚无本 PC） |
| 2026-11-02-browseruse-and-approval-di | 2026-11-02 | 浏览器插件缺 Playwright，后端未就绪；审批与成本控制器构造注入宿主契约 500 | 是：10-09 只留了待办，测试没拦住 |
