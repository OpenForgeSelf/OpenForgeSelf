# 00 Repository Understanding — NETSDK1151 修复

## 仓库与位置
- 项目根：仓库根目录（.NET 10 Web 宿主 + MAF 插件 + 前端 Vite）。
- 发布链：`scripts/release/release-local.ps1` 编排 build-frontend → publish-host → publish-bootstrapper → publish-updateagent → package-release。
- 涉事项目：`ForgeSelf.UpdateAgent`（原生自更新代理，AssemblyName=update-agent，FDD 单文件设计）、`ForgeSelf.Api`（宿主，FDD 单文件 publish）。
- SDK：本机 `11.0.100`（项目 TFM net10.0）。

## 相关既有约定
- `ForgeSelf.UpdateAgent.csproj` 注释声明"FDD 单文件、零 NuGet 依赖、BCL only；publish-updateagent.ps1 断言大小防误打自包含"——设计意图是 FDD。
- `ForgeSelf.Api.csproj:107` 以 `ProjectReference ... ReferenceOutputAssembly="false"` 引用 UpdateAgent，仅为建立构建顺序（2026-11-21 输入15）。
- `publish-host.ps1:64` 用 `-r win-x64 --self-contained false -p:PublishSingleFile=true`。
- 问题类闭环规范：`.agents/skills/problem-class-closure/SKILL.md`；台账 `docs/11-troubleshooting/`。
- 上一任务 PC-047（emptyOutDir）+ 并行会话 PC-048（pnpm 非 TTY）已在同一发布链归档，本任务是该链的第三个独立根因。

## 关键事实（调查所得）
- `dotnet build` 同参数成功、`dotnet publish` 失败 → 差异在 publish-only 推断逻辑。
- SDK 11 `RuntimeIdentifierInference.targets:67-78`：PublishSingleFile=true 且 SelfContained 空 → PublishSelfContained=true；85-89 行 publish 时回写 SelfContained=true。
- `ValidateExecutableReferences`（Microsoft.NET.Sdk.targets:1340）读引用项目回传的 SelfContained；项目引用协商忽略全局 SelfContained=false。
- 原 csproj 的 SelfContained 带 Condition 且写在 RuntimeIdentifier 赋值前 → 求值时 RID 空 → 条件 false → 属性未设置。
