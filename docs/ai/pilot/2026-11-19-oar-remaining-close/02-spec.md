# Specification

> Task ID：`2026-11-19-oar-remaining-close`

## Functional Requirements

| ID | 要求 |
| --- | --- |
| FR1 | `AgentHubAgentsController` / `AgentHubTasksController` 类级 `[Authorize("ApiKeyPolicy")]` + 程序集级 Auth 守卫测 |
| FR2 | Worker `runtime.install`（有 `rt.install` 才执行）；宿主 `GET …/install-plan` + `POST …/install`（body `confirmed=true` 否则 400）；UI 确认框 |
| FR3 | `http.ts` + Session 页：input / abort / close / records / stream 接线 |
| FR4 | Runtime 行：probe / models 按钮与分列展示 |
| FR5 | 按 Runtime `capabilities` 禁用 Steer（input）与 abort；展示 Reason |
| FR6 | Verify 证据落盘；走查截图；032 §5/§6 + 架构 §8/§9 回写 |

## Business Rules

1. 安装禁止静默：未 `confirmed` 不得调用 Worker install。
2. 对外仍禁止泄漏 OarSessionId。
3. 能力以 Runtime 声明为准，禁止按名称猜 Steer。
4. scripted/bundled：`installPlan` 可为 unsupported；UI 展示原因，不假装成功。

## Boundary / Error

| 边界 | 行为 |
| --- | --- |
| 无 token | agents/tasks/runtimes/sessions → 401 |
| 未知 session | abort/records/input → 404 |
| Worker 不可用 | probe/models/install → 503 可解释 |
| 无 session 能力 | input 禁用 + Reason |
| confirmed=false | install → 400 |

## Interaction Design

| 通道 | 点什么 | 出现什么 |
| --- | --- | --- |
| Runtime 检查 | 点「检查」 | installation / auth 分列 JSON 摘要 |
| Runtime 模型 | 点「模型」 | models 载荷或 not_supported |
| Runtime 安装 | 点「安装」 | 弹出 plan → 确认 → 执行 → toast → 自动再探测 |
| Session 续接 | 输入框+发送 | 调用 input；结果 toast；records 刷新 |
| Session 终止 | Abort / 关闭 | 调用 abort/close；列表刷新 |
| Session records | 选中会话 | 下方 records 列表；可选订阅 stream |
| 能力不足 | 打开无 session 能力的会话 | Steer 禁用；旁注 Reason |
| 五态 | 加载/空/出错/401/正常 | 工具栏 loading；空态文案；setMsg err；401 经 http 抛错 |

## Failure Modes

无已知 PC 复发；鉴权漏挂用 Auth 守卫防回潮。

## Acceptance Criteria

- [x] AC1：Auth 守卫测绿；无 token agents/tasks 401
- [x] AC2：install-plan + install(confirmed) API 可达；未确认 400
- [x] AC3：Session UI 操作可脚本/走查；http 客户端齐
- [x] AC4：Runtime probe/models UI 有截图
- [x] AC5：能力禁用 Steer 有 UI 证据或可解释
- [x] AC6：V1–V8/V11–V12 证据文件存在（可行项）
- [x] AC7：W1–W8 尽可能截图；W9–W10 ⏸
- [x] AC8：032/架构/gaps/TODO 一致；00–07 齐
