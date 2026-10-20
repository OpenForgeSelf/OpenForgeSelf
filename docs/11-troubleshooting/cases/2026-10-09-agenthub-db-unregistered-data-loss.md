---
id: 2026-10-09-agenthub-db-unregistered-data-loss
title: AgentHub/ImGateway 库未登记 PluginDbs，宿主升级后「agent 登记数据全丢」
pc: PC-003
severity: P1
evidence: Verified
---
# Case：2026-10-09 插件库未登记 PluginDbs 致升级丢数据

> 规则/修法/扫描方法见 `../problem-classes/PC-003-plugin-db-unregistered-data-loss.md`，本档只讲这一次发生了什么。

## 1 现象

用户运行实例（`D:\src\tools\OpenForgeSelf`）宿主升级到 2.3.4 后，AgentHub 插件「agent 登记」列表为空。实测现场：`AgentHub.db` 落在旧版本目录 `versions\<ver>\Data\`（2.3.2=512KB、2.3.3=385KB 有数据；2.3.4 起每版本 57KB 空库），新版本目录首次启动新建空库。出处：`ForgeSelf.Api/Plugins/README.md` §三 实证段（2026-10-09，Verified）。

## 2 复现红灯

发布态目录对比 + 文件大小证据即复现（512KB→57KB 空库切换沿版本目录一目了然）；ImGateway 同型漏登记同时被发现。当时无自动化测试（Verified，README 记录为现场实测）。

## 3 根因（5 Whys + 第 6 问）

`AgentHub`/`ImGateway` 连接名未登记 `XCodeConfig.PluginDbs` → 无连接串 → `DAL.Create` 走 XCode 默认派生 `{程序基目录}/Data/{conn}.db`，发布态基目录=`versions/<ver>/`，库随版本走。
- Why1 数据丢？新目录读的是新建空库。Why2 空库？旧库留在旧版本目录。Why3 为什么在版本目录？连接串缺省派生基目录。Why4 为什么没登记？登记规则只在 README §三 文字里，插件自带建表代码即可跑通开发态（Debug 基目录下 Data/ "看起来正常"）。Why5 为什么开发态看不出？开发态基目录不随版本切换，丢失路径不暴露。
- **第 6 问**：`XCodeConfigTests` 只断条目形态，不校验「插件侧引用 ⊆ 登记」的关系；没有 Stage 问「新插件库登记了吗」→ 归因「文档有规则但无守卫、无索引」。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| AgentHub（本 case） | 是 | 已修：PluginDbs 登记 + 旧库拷回数据根 + 重启 |
| ImGateway | 是（同批发现） | 已修：同上（README §三 修复指引即两案总结） |
| TodoTracker/AIAgent 等其余插件 | 否 | 已登记，升级不丢（README 实证段对照组） |
| 2026-10-17 建类时全量枚举 | 存量 12 连接名 = PluginDbs 12 key，无新增红 | G2 测试落地即绿 |

## 5 修复与被否方案

修法 = PC-003 canonical fix（登记一行 + 存量迁移拷库）。被否：插件运行时自注册连接串（绕过单点真源，制造第二套登记面）。

## 6 重构提案与结论

R0 不重构——登记入口 `PluginDbs` 已是单点，缺的是关系校验，以 G2 枚举测试补齐（2026-10-17）。

## 7 守卫落地

G2 `ProblemClassEnumerationTests.PluginDalConnNames_MustBeRegisteredInPluginDbs`（active，2026-10-17，阳性对照命中≥10）。此前仅有 README §三 文字规则（文档不算守卫）。

## 8 逃逸分析

N/A 首次（规则 2026-10-09 当日才沉淀进 README；两个插件同时漏中说明「靠读文档登记」本身就是脆弱面）。

## 9 基建改进

促成 README §三「登记=落数据根唯一入口」警示段的写成；本批将其升格为 PC-003 + 机器守卫，并给 PC lens 提供「新插件带持久库吗」必答项。

## 10 复用赋能

一切新插件（plugin-development / plugin-frontend-scaffold 流程）带持久库时必须回答 PC-003 lens 三问；数据目录/迁移类发布缺陷 Review 时对照本 case。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 现象、根因、修复、对照 | Verified | `ForgeSelf.Api/Plugins/README.md` §三（2026-10-09 实证段） |
| 存量 12 连接名与 PluginDbs 一致 | Verified | 2026-10-17 预扫描（`ConnName`/`DAL.Create` 枚举 = PluginDbs.Keys） |
