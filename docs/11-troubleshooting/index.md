# 11-troubleshooting — 排障手册

> 状态：部分（已沉淀少量已知问题，2026-08-12）
> 最后更新：2026-08-12

错误现象 → 根因 → 解法，一条一例。当前已从代码/运行事实沉淀：

| 现象 | 根因 | 解法 |
|------|------|------|
| `dotnet build` 复制阶段 CS2012（文件被占用） | 运行实例占用 `ForgeSelf.exe/dll`，无权限停止 | 停止进程或构建到独立输出目录 |
| 前端 `pnpm run build` 约 31 个类型错误 | 预存错误（非本轮回归，T032） | 独立修复，不混入功能 PR |
| 多模态图片块 `InvalidCastException` | `image_url` 内层用了匿名类型 | 改用 `Dictionary<string,object>`（已修） |
| 本地 1234 返回 404（多模态） | `BuildVisionRequest` 转发未剥离 `provider:` 前缀 | 转发前剥离前缀（已修） |
| `TruncatedContent.test.ts` 1 失败 | `navigator.clipboard` 只读（环境缺陷） | 改 `Object.defineProperty` / `vi.stubGlobal`（待修） |
| `ChatRecordRealLLMTests` 偶发红 | 测试间状态干扰 | 排查隔离性（TODO 已知） |

新案例排查后补入本表。
