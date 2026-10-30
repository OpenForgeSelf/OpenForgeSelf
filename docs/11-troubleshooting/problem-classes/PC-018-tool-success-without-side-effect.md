---
id: PC-018
title: 执行体报 success 但真实副作用未落定（write_file 零落盘）
tags: [plugin-tools,success-integrity,state-sync]
invariant: 工具/执行体返回 success 的前提是真实副作用已落定（磁盘写、进程终态、库行提交）；响应体不是副作用发生的证据，验证一律以副作用对象本身回读为准。
status: guarded
first_seen: 2026-10-30
recurrences: []
cases: [2026-10-30-tool-success-without-side-effect]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs, status: active}
lens:
  - 新增/改写有副作用的工具端点后，是否有「以副作用对象回读为准」的持久化断言（而不是只断言响应 success）？
  - 该执行体的 success 分支与副作用调用之间，是否存在可被合并/重构静默删除的语句窗口（拼内容 → 建目录 → 构造响应）？
  - 修复「报错类」缺陷后，是否做了成功路径的端到端复验？（上游报错会遮蔽深层静默缺陷——本类即由 PC-017 修复后首次成功路径复测现形）
  - 与 PC-004（Agent 委派成功回报 ≠ 进程真实退出，候选待定性）同类属：本类是工具执行体侧的首例实证；PC-004 定性后评估两案合并。
---
# PC-018 执行体报 success 但真实副作用未落定

> 事件经过见 `../cases/2026-10-30-tool-success-without-side-effect.md`。

## 1 不变量与诱因

**不变量**：`success:true` 必须是「副作用已发生」的陈述，不是「代码走到了成功分支」的陈述。凡有外部副作用的执行体（写文件、起进程、写库），其成功响应必须以副作用对象的**回读**来验证——响应体自己证明不了任何事。

**诱因**：①坏合并/大段重构时，副作用调用语句（如 `File.WriteAllText`）恰处在「拼内容 → 建父目录 → 构造响应」之间的几行窗口里，是合并残迹最爱消失的位置，肉眼极难看出「少了一句」；②响应体里的 `writtenLength`/`totalLines` 是内存串的属性，与磁盘无关——拼好的串让成功分支「看起来很完整」；③**上游把路挡死时（本例 = PC-017 schema 坏，网关层必挂），该缺陷被完全遮蔽**——直到上游修复后的首次端到端复测才现形。

## 2 特征签名与扫描方法

- **S1（test，G2）**：`FileToolsWritePersistTests` 四用例——新建 / 行区间替换 / 文末追加三个用例全部以 `File.ReadAllText` 磁盘回读为准（不信响应体），负例断言「不存在文件从中间行写必须失败」。红灯探针已实证守卫有效：stash 掉修复后 3 failed / 1 passed（三个落盘断言全红）。
- **盲区**：只守 write_file；FileTools 其余有副作用的端点（压缩/清理/重命名等）尚无同类持久化断言——扩面记 TODO。

## 3 Canonical fix

成功分支里副作用调用必须真实存在，且验收以回读为准。参考实现：`Plugins/FileTools/Services/FileToolFunctions.cs:325`（`File.WriteAllText(absPath, final)`，其后才是响应体）。被否方案：以响应体回传字段当凭证——`writtenLength` 是内存串长度，与磁盘无关（正是本次假成功的样子）。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/Unit/FileToolsWritePersistTests.cs` | `dotnet test --filter FileToolsWritePersistTests`（FileTools 域过滤集） | 仅 write_file；其余副作用工具待扩面（TODO） |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| 2026-10-30-tool-success-without-side-effect | 2026-10-30 | write_file 报 success 但磁盘无文件（PC-017 修复后首次 fs 复测现形） | 否（首见，此前无守卫；曾被 PC-017 网关错误遮蔽） |
