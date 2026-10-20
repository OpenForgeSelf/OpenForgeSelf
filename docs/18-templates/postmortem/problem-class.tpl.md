---
id: PC-NNN
title: <问题类一句话标题>
tags: [<tag1>, <tag2>]
invariant: <一句话不变量——违反了什么>
status: open            # open | guarded-partial | guarded | structural
first_seen: YYYY-MM-DD
recurrences: [<case-id>, ...]   # ≥2 触发强制 G2；≥3 触发 G4 重构提案（ProblemClassRegistryTests 断言）
cases: [<case-id>, ...]         # 已归档 case（cases/ 目录内实体文件，必须存在）
signature:                      # 供 scripts/check-problem-classes.ps1 执行；一行一个 flow-map
  - {id: S1, kind: grep, glob: "<相对路径glob>", regex: "<正则>", expect: no-match}
  - {id: S2, kind: test, path: "<测试或守卫脚本的真实路径>"}
allowlist: []                   # 存量豁免：- {path: "<文件>", reason: "<为什么合法>", expires: "YYYY-MM-DD"}；过期即红
guards:
  - {level: G2, path: "<守卫真实路径>", status: active}    # active | planned（planned 必须另配 none+todo+expires 语义见技能 07-recording-spec）
lens:                           # 新功能 Spec/Plan 触碰 tags 时的必答题（Failure Modes 预填源）
  - <问题1？>
  - <问题2？>
---
# PC-NNN <标题>

> 本文件是「这一类问题」的唯一真源：规则、扫描方法、修法。事件经过在 case，不在这里。
> 状态机与守卫等级语义见技能 `.agents/skills/problem-class-closure/references/07-recording-spec.md`。

## 1 不变量与诱因

不变量（front-matter `invariant` 的展开）；Agent/人为什么会犯（认知缺口、框架 API 诱导、文档缺口）。

## 2 特征签名与扫描方法

front-matter `signature` 的书写说明：为什么选这些正则/枚举入口、已知盲区（如动态反射解析不出来的场景）、扫描范围矩阵（技能 references/02）。

## 3 Canonical fix

≤1 屏的标准修法，附参考实现 `文件:行`。被否方案与理由（若有）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
