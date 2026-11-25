# 06 Review

## 技术核对（结论先行）
修复正确：根因是 exe 轨 agent 运行时踩在 staged 上，而步骤 8 先删 staged，步骤 9 首次加载 `System.Collections.Specialized` 时 DLL 已不存在。交换顺序让重启发生在运行时尚完整时，语义上也更合理（先让新版本跑起来，再清理旧暂存）。

## 逐项复核
- 反例验证（用户质疑）：SDK11 假设——实测 SDK10/11 打 agent 均 195193B，推翻；签名假设——实测签名/未签名都能启动到参数解析，推翻；"我的 csproj 改动导致"——改动只影响编译期 SelfContained 声明，与 BCL 加载无因果，但它修通 NETSDK1151 让流程第一次走到重启步骤，属"暴露者"非"致因"。
- 历史轨型复核：ps1（Exe=）五次真机成功，exe（ExeName=）只单测过——证明缺陷是 exe 轨首次真机暴露，非回归。
- 最小改动：2 行顺序交换 + 注释 + 守卫，无架构改动，ps1 轨与其它模块不受影响。
- 守卫有效性：`IndexOf` 顺序断言 + 阳性对照（`psi.EnvironmentVariables` 行存在）防假绿。

## 遗留
- 真机更新闭环未实测（沙箱 MSB4166 环境约束，见 05 Known Limitations）。
- 回滚/启动健康检查缺失（用户诉求，另批）。
- PC-034 双文件假红、外部 -Sign 死锁进程：存量/他人资产，未碰。

## Final Decision
**APPROVED**：修复针对根因、最小改动、守卫与台账全绿（PC 40/40，Registry 6/6，G2 1/1）；真机闭环受沙箱环境限制未在本轮完成，已记 TODO，不阻塞代码交付。AC1–AC4 Verified。
