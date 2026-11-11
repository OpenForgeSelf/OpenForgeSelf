# 01 Intent — 2026-11-09-json-facade-unify

## Problem
序列化/反序列化/解析调用散点直调 System.Text.Json：`JsonSerializer.*` 614 处、`JsonDocument.Parse` 198 处、自定义 `new JsonSerializerOptions` 18 处、Utf8JsonWriter/JsonWriterOptions 8 处，分布 155 个生产文件。批1（PC-027）只统一了「写出转义」一个切面；命名策略、大小写容错、注释容忍、深度、缩进、忽略条件等其余策略仍散在各调用点——将来任何全局 JSON 策略调整都要再改几百处。

## Why（用户输入9 原文 2026-11-09 15:56）
「将全部用到序列化、反序列化的地方，都改为本项目的封装，统一处理，避免将来修改又全部改一遍」

## Expected Outcome
- 生产代码（除 `ForgeJson.cs` 自身）零 `JsonSerializer.*` 直调、零 `new JsonSerializerOptions/JsonWriterOptions/JsonDocumentOptions`、零 `JsonDocument.Parse` 直调、零无参 `ToJsonString()`——全部经 `ForgeSelf.Abstractions.ForgeJson`。
- 守卫 v2 结构性强制（违例即红）；将来改策略 = 只改 ForgeJson.cs 一个文件。

## Constraints
- 语义零漂移：只收敛入口，不改任何序列化行为（论证见 02 Business Rules / Compatibility）。
- 热路径不引入每调用 options 分配（static readonly）。
- 不碰 Tests（守卫测试除外）/web/plugin.json/e2e/docs；不发版、不 git 提交（闸门3 = 用户）。
- 工作区叠加批1 + 并行批未提交项：机械单行改写，回滚按文件清单逐文件（见 04 rollback）。

## Success Criteria
AC1–AC6（见 02）全绿：守卫 v2 RED→GREEN、build 0 error、全量测试净新增红 = 0、verify-pilot 八件齐。

## 闸门1 依据
用户输入9 直接指令为需求来源；本批面（155 文件 + Parse 域）显著大于批1，按规范 00-04 先行、**停下等用户显式拍板后才 Implement**。拍板项：① `JsonDocument.Parse` 198 处是否随批收敛（默认：入批）；② 未提交工作区上叠加或先提交批1（默认：用户先提交批1 再开扫）。
