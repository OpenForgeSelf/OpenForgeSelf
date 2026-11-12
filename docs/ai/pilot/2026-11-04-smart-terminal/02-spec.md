# 02 · Spec — 2026-11-04-smart-terminal

> 真源：插件 docs/01-产品需求说明.md、02-交互说明.md、03-URL协议.md、04-权限与审计.md、06-UI交互评审稿.md（分支内，均已定稿）。
> 定位：**可审批的命令会话**（本机/SSH 多标签 + URL 通用启动器），非完整 PTY/Windows Terminal。

## 一、Functional Requirements
| ID | 需求 | 要点 |
|----|------|------|
| FR-1 | 基础多标签 | 新建/切换/重命名/关闭；每标签独立 shell、cwd、输出、历史、审批队列；会话隔离不串台 |
| FR-2 | 三级权限 | Auto / **AskWrite（默认）** / AskHighRisk；Agent 经会话级 `st_set_permission_mode` 调整，不改全局 |
| FR-3 | JSONL 审计 + 脱敏 | 全量审计落 JSONL；UI 审计抽屉（类型过滤/搜索）；键名命中 password/secret/token/key/credential 或值形态像密钥 → 只留键名 + `` |
| FR-4 | 跨平台一等壳 | Linux bash/zsh/sh；Windows cmd/powershell/pwsh（探测 WT profiles）；不可用项仍列出 |
| FR-5 | 通用 URL 启动器 | `profile=local|ssh|wsl-ssh` + `step`/`run` + 可选 env/cwd/title；旧参数 type/cmd 兼容映射；`custom:<id>` 仅协议位不实现 |
| FR-6 | 认证决策树 | 默认交互式密码（无 URL 密码时）；`auth=key`；URL+sshpass 仅内网过渡 + 「我已知晓风险」ack；S5 **先框后标签**；地址栏密码擦除；密码不落任何持久化存储 |
| FR-7 | 刷新恢复（S-R） | 恢复标签列表/连接信息（无密码）/近期输出缓冲（每标签最近 1000 行或约 100KB 先到先截，sessionStorage）；关闭标签即焚毁 |
| FR-8 | 宿主集成 | 控制器类级 `[Authorize("ApiKeyPolicy")]`；MCP 工具 `st_*` 8 件（toolPrefix=st）；版本徽标 v<version>；CT 画廊故事（MultiTabs/UrlLaunch*/PasswordRiskAck/S-R） |

## 二、Input / Output
- 输入：终端命令文本；URL 深链 query（profile/step/run/env/cwd/title/auth/password[内网]）；`st_*` 工具调用（会话 id + 命令/模式）。
- 输出：流式 stdout/stderr/system 行（每标签独立缓冲）；审批弹层；审计 JSONL 行；工具 JSON 应答。
- 端点面：`/api/smart-terminal`（18 端点，会话生命周期/执行/审批/审计/配置探测）。

## 三、Business Rules 与 Boundary Conditions
- 写操作一律 AskWrite 审批闸门（含 URL step 内命令）；高风险再升 AskHighRisk；Auto 仅显式授权会话。
- 浮层互斥优先序：风险 ack → 交互密码 → 审批 → 审计。
- 交互密码：先出密码框、确认后才建标签连接；取消 = 不建连接不建标签；无「记住本会话」；关标签即焚。
- URL 密码：ack 策略 always-ack（默认）/intranet/disabled；确认后仅短暂注入 SSHPASS；地址栏 scrub；审计只记 `auth=url` + 脱敏。
- 标签数量沿用现有多标签上限；缓冲 1000 行/100KB 先到先截。

## 四、Error Handling 与 Compatibility
- 非法/未知 profile 或参数 → S5 400 兜底红条（错误文案读，不建标签。
- shell 探测失败 → 该项置灰仍列出；连接断开 → 输出区展示可重连状态（S-R 恢复后）。
- 参数 type/cmd 向后兼容映射 profile/step；custom: 协议位返回明确「未实现」提示而非静默失败。

## 五、Non-functional Requirements
- 审计与存储零密码明文（DevTools Application 面板不可见）；密码仅内存随标签生命周期。
- 权限：控制器类级 `[Authorize("ApiKeyPolicy")]`；前端经宿主 import map 共享依赖，禁 import 宿主模块。
- 性能：标签切换不重放全量缓冲（仅内存追加）；URL 解析同步 <50ms 量级。

## 六、Interaction Design（交互设计）
「点什么出现什么」（主链路，含五态）：
1. 点击 `+` → 新标签（默认壳）；双击标题 → 重命名；`×` → 关闭该标签（缓冲焚毁）；点击标签头 → 切换（输出不串台）。
2. 命令输入框输入 + 回车 → AskWrite 默认：写命令弹**审批浮层**（展示脱敏命令 + 批准/拒绝）；批准 → 执行并流式输出；拒绝 → 不执行、审计记 rejected。
3. 点击「审计」 → 审计抽屉（类型过滤 + 搜索，仅脱敏字段）；空审计显示空态文案。
4. URL 带 profile 进入 → 蓝条脱敏摘要（profile/步数/host）→ 按 03§6 决策树：无密码 SSH → **S5 密码模态**（显示 user@host:port，不预填，主推密钥文案；取消不建标签）；URL 带密码 → **风险 ack 勾选框**（未勾选主按钮禁用；确认后地址栏密码被擦除）。
5. F5 刷新 → 恢复标签列表/激活标签/连接摘要（无密码）/上限内输出；无持久化密码。
- 加载态：标签建立中骨架/禁用输入；空态：无标签时主区引导文案 + 主推密钥登录；出错态：S5 红条/连接失败条（可重连）；无权限态：401/403 由宿主鉴权兜底；正常态：流式输出 + 徽章。
- 验收标准并入 AC1-AC8（见下节）；走查按 06-UI交互评审稿 S0–S9+S-R 逐态核对。

## 七、Acceptance Criteria
- AC1 多标签新建/切换/重命名/关闭可用，会话隔离不串台（e2 冒烟主链路覆盖）。
- AC2 默认 AskWrite：写命令弹审批，批准执行、拒绝不执行（审计留痕）。
- AC3 审计 JSONL + 抽屉过滤/搜索可用；密钥形态值仅键名 + `***`。
- AC4 URL 通用启动（local/ssh）出脱敏蓝条；非法 profile → S5 400 红条不建标签。
- AC5 无密码 SSH → 交互密码模态（先框后标签；取消零副作用）。
- AC6 URL 密码 → ack 未勾选不可继续；确认后地址栏 scrub、审计无明文。
- AC7 刷新恢复标签/连接摘要/缓冲；sessionStorage 无密码明文。
- AC8 门禁与测试全绿：run-gates（pilot/style）0 新增违规 + dotnet build 0 error + SmartTerminal 过滤测试绿 + 插件 web build + ForgeSelf.Web check/test + 冒烟 e2e 绿。