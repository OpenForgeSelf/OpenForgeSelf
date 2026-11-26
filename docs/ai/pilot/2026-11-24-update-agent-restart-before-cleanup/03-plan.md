# 03 Plan

## 闸门
- Gate1：用户「可」授权方案 A（交换步骤 8/9）——reviewer 决策。
- Gate2：实现后守卫/门禁验证。
- Gate3：收口。

## 步骤
1. Implement：交换 UpdateAgentRunner.cs 步骤 8/9 + 注释写明不可交换。
2. Guard：新增 Pc050 G2 单测。
3. Verify：单测 + 单组件 publish + check-problem-classes + Registry。
4. Archive：PC-050 + case + 三处索引 + 05/06/07。
5. 收口：日志 + TODO + 汇报；完整 zip 重打包受沙箱 MSB4166 约束另记 TODO。

## 决策点
- 曾考虑方案 B（提前预热 EnvironmentVariables）、C（运行时独立 agent-run）、D（agent 自包含）——均否，理由见 case §5 / PC-050 §3。
