# 05 · 已知风险：密码进入 URL（产品结论）

> **PM 结论（2026-11-04）**：URL 密码 = **内网过渡能力，非推荐默认**；必须落地知情确认与其它缓解。公网/演示默认 `always-ack` 或 `disabled`。

## 1. 为什么危险

| 风险面 | 说明 |
|--------|------|
| 浏览器历史 | 明文密码留在 History / 同步账号 |
| 代理与访问日志 | Referer、反向代理 access.log、APM |
| 截屏 / 分享 | 复制链接即泄露 |
| 进程列表 | 误入 argv 可被窥视（实现上避免 `-p password`，只用 `SSHPASS` 环境） |
| 肩窥 | 确认前地址栏可能曾可见 |

## 2. 必做缓解（已实现 / 须保持）

1. 读入后立即 **`history.replaceState` scrub** password/pwd 等键  
2. 密码 **不进** JSONL / 命令历史 / 系统回显原文；审计仅 `passwordProvided` + 脱敏命令  
3. 只注入进程环境 **`SSHPASS`** + `sshpass -e`；执行结束清空 EphemeralEnv  
4. **首次/每次「我已知晓风险」勾选** 后方可继续（见 02 策略开关）  
5. UI 黄条警告 + 本页文档  
6. 产品主推 **`auth=key`**；深链无密码时默认 **交互式输入**；vault 仍排期  

## 3. 推荐替代（优先推广）

1. SSH 公钥：`profile=ssh&auth=key&identityFile=...`  
2. **交互式遮罩输入密码**（深链无 password 时的默认路径，见 02§6 / 03§5.2）  
3. 堡垒机一次性短票（后续）  
4. 宿主 vault → API 内存传递，永不进 query（排期）  

> URL 明文密码排在所有替代方案之后；通用启动器用 `profile`，不要为「带密码 SSH」单独做一套主协议。  

## 4. 产品经理确认（已勾选）

- [x] URL 密码仅作**内网过渡**；公网/演示默认关闭（`disabled`）或**每次**二次确认（`always-ack`，当前默认）  
- [x] UI 使用密码深链前须「我已知晓风险」勾选（`intranet` 下可首次后记住）  
- [x] 交互输入密码列为深链无密码时的**默认能力**（UI 评审见 06）；vault 仍排期；文案主推密钥  

## 5. 设置速查

```text
localStorage['forge.self.smart-terminal.url-password-mode.v1']
  = 'always-ack' | 'intranet' | 'disabled'

localStorage['forge.self.smart-terminal.url-password-risk-ack.v1']
  = '<ISO timestamp>'   # 仅 intranet 模式跳过重复弹窗
```
