# 02 · Spec

## Functional Requirements

页面、协议和命令行使用同一套 query：`st=1&type=local|ssh&host&port&user&shell&title&step`。口令键丢弃。`type=ssh` 且无密钥时先出密码框。`step` 在会话连上后按权限闸逐条提交。

## Input

- 浏览器：`/smart-terminal?st=1&...`
- 协议：`forgeself://terminal?type=ssh&host=&port=&user=`
- 命令：`ForgeSelf.exe --open-terminal --type ssh --host h --user u`
- Agent：`st_create_session` / `st_run_command` 等既有工具

## Output

一格新标签，或附着 Agent 已创建的 `sessionId`。第二个进程退出码 0，并让正在运行的实例打开标签。

## Business Rules

密码不进 URL、收件箱、审计。写操作停在确认框。助手默认作用域增加 `smart-terminal`，不挂其余插件工具。

## Boundary Conditions

`wsl-ssh` 与 `custom:<id>` 显示错误，不创建会话。`dotnet` 开发宿主、`FORGESELF_NO_TRAY=1`、带实例号的并行实例不写注册表。协议登记指向安装根 `ForgeSelf.exe`（若进程在 `versions/<ver>/` 下）。

## Error Handling

inbound 的 query 仍带口令键则 HTTP 400。转交失败退出码 2。找不到会话时页面提示一次。

## Compatibility

旧的 `profile=` / `step=` 解析保留。`POST /launch` 不删。

## Non-functional Requirements

转交走 `127.0.0.1` 且不走系统代理。收件箱只在进程内存里。

## Acceptance Criteria

- AC1 页面深链打开本机标签并执行 `step`，地址栏去掉启动参数。
- AC2 已有实例在时，新进程把 `forgeself:` URL 转交并退出 0，页面新开标签。
- AC3 inbound 拒绝带密码的 query。
- AC4 `st_*` 工具执行期使用 `CreateHostScope`，并进入助手默认作用域。
- AC5 `wsl-ssh` 不打开本机壳。

## Interaction Design（交互设计）

| 触发 | 结果 |
|------|------|
| 打开带 `type=local` 的地址 | 新标签，连上后跑 `step`；写操作弹出确认 |
| 打开带 `type=ssh` 且无密钥的地址 | 先密码框；确认后才有标签；取消不加标签 |
| 链接里有密码 | 丢弃密码并提示重新输入 |
| 第二个进程带着协议 URL | 不新开一套服务；当前页面多一格标签 |
| Agent 创建会话 | 当前页面附着该会话 |
| 加载 / 空 / 错误 / 401 | 沿用 v2 五态，不因深链失败把整页打成空白 |

## 走查符合性

主路径在 dev-stack `:7399` 走通：页面深链与协议转交各开一格标签，命令有输出。
