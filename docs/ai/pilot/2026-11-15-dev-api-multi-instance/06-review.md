# Review

## 审查八问

1. **Intent**：根 `pnpm dev` 已改为启动宿主后端和前端；运行时两端均 HTTP 200。
2. **Spec**：最终要求是复用 `dev-stack.ps1`，保留 `dev:web`；不保留 API-only 默认入口。
3. **Scope**：未改宿主业务逻辑、数据库、整栈生命周期或 UI；删除了本任务早期新增且已不需要的 `dev-api.ps1`。
4. **Files**：入口、G2 守卫、启动说明、PC-007 case 与 pilot 证据均属于本任务范围。
5. **Tests**：测试项目构建通过；定向测试在 testhost 启动阶段因 `Win32Exception (5)` 中止，断言未执行。
6. **Runtime**：根 `pnpm dev` 启动后端 7301 与前端 7399；健康端点和首页均返回 HTTP 200。
7. **Architecture**：复用已有整栈启动器，没有引入第二个启动管理器或改变固定状态文件的单实例语义。
8. **Evidence**：运行行为已验证；G2 自动化测试与全量后端测试未通过执行门槛，不能据此签署完整验收。

## Requirement Check

PARTIAL：根命令接线与双端运行验收有证据；G2 用例执行和中档全量后端测试受 testhost 环境阻塞。

## Scope Check

PASS

## Test Check

BLOCKED：`dotnet test` 在 testhost `OpenProcess` 权限异常处中止，不是断言红灯，也不能记作通过。

## Architecture Check

PASS

## Risk

自动化守卫和全量测试未验证；`dev-stack.ps1` 具有固定单实例状态生命周期，重复启动可能停止已记录实例。

## Findings

### Critical

无。

### Major

- testhost 启动阶段因 `Win32Exception (5)` 失败；需要在权限正常的环境重跑 targeted 与 full backend tests。

### Minor


## Final Decision

BLOCKED

运行行为已实测，但自动化门禁未通过执行；等待测试环境恢复及用户闸门2验收。当前不提交、不发布。