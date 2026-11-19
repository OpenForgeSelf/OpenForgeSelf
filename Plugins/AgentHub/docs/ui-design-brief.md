# AgentHub · UI Design Brief（面向设计师）

> 维护：oar-agenthub-ux-real（2026-11-19）。设计师不读代码；只看功能、旅程、交互。

## 功能

Agent 中枢：登记本机外部 Coding Agent → 委派任务 → 看任务/审批 → 查看 Runtime 真实安装态与 Session。

## 用户旅程

1. **Agent 目录**：扫描/登记/授信/探测。每张卡带**厂商品牌图标**（互不相同）。
2. **发起委派**：选已启用 Agent，填提示词，可选 SessionKey 续接。
3. **任务台**：看进度、审批、取消。
4. **Runtime / Session**（两节，一节一事）  
   - **Runtime 库存**：本机安装真相。已装（可执行文件）/ 未装（not_found）/ 内置捆绑。不含测试用 scripted。  
   - **Session 操作**：选会话 → 发送 / Abort / 关闭；Records 流。

## 界面交互行为

| 面 | 行为 |
| --- | --- |
| Runtime 卡「检查」 | 拉取 installation + auth，**展开** JSON 详情 |
| Runtime 卡「模型」 | 拉取 models；失败显示原文，不假装有模型 |
| Runtime 卡「安装」 | 先出计划确认框；取消无副作用；已装则按钮禁用 |
| Session 行 | 选中后右侧出操作区；禁用按钮带原因 |
| 空态 | Runtime 空：提示刷新 Worker；Session 空：引导去委派 |

## 视觉要点

- 品牌图标优先官方 SVG；缺图标用名称首字母，**禁止**全员同一占位图。
- 状态徽标：已安装=绿系、未安装=红系、捆绑/未探测=中性。
- 探针 JSON 默认折叠，避免一屏杂讯。
