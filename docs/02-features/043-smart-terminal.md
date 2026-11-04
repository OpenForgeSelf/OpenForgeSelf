# 043 · 智能终端（smart-terminal）

> 状态：随 2026-11-04-smart-terminal pilot 入库（分支 feat/smart-terminal）。插件定位 = **可审批的命令会话**（本机/SSH 多标签 + URL 通用启动器），非完整 PTY/Windows Terminal。

## 功能点登记（与 pilot 02-spec 一致）
- FR-1 基础多标签：新建/切换/重命名/关闭；每标签独立 shell、cwd、输出、历史、审批队列；会话隔离。
- FR-2 三级权限：Auto / AskWrite（默认）/ AskHighRisk；Agent 经会话级 st_set_permission_mode 调整。
- FR-3 JSONL 审计与脱敏：全量审计 + UI 抽屉（过滤/搜索）；密钥形态值只留键名 + ***。
- FR-4 跨平台一等壳：Linux bash/zsh/sh；Windows cmd/powershell/pwsh（WT profiles 探测）；不可用仍列出。
- FR-5 通用 URL 启动器：profile=local|ssh|wsl-ssh + step/run + env/cwd/title；type/cmd 旧参兼容；custom: 协议位预留。
- FR-6 认证决策树：交互式密码默认；auth=key；URL+sshpass 内网过渡 + 风险知情确认；S5 先框后标签；地址栏密码擦除；零持久化。
- FR-7 刷新恢复：标签/连接摘要（无密码）/近期输出缓冲（1000 行或 100KB，sessionStorage）；关标签即焚。
- FR-8 宿主集成：api/smart-terminal（18 端点，类级 [Authorize("ApiKeyPolicy")]）+ st_* 工具 8 件 + 版本徽标 + CT 画廊。

## 用户动线
菜单「智能终端」（/smart-terminal）进入 → + 建标签 → 输入命令（写命令走审批）→ 审计抽屉查记录；URL 深链直达指定 profile 会话；F5 恢复现场。

## 入口与权限
- 页面路由 /smart-terminal（plugin.json frontend.route 唯一声明）；API /api/smart-terminal（ApiKeyPolicy）。
- 工具面：st_* 8 件（MCP 工具，toolPrefix=st）。

## 验收基准
插件 docs/06-UI交互评审稿.md S0–S9 + S-R（CT 画廊故事与 e2e 冒烟按其定稿）；pilot 2026-11-04-smart-terminal AC1-AC8。