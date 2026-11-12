# 05 · Evidence — 2026-11-05-probe-dll-restore

- **Verified**（qa-ci 实测，字节同源 .temp/tools 桥接副本）：FOUND case exit 0（探 'System.Private.CoreLib'）/ ABSENT case exit 1（探 'NoSuchTypeQaCi123'）/ --expect-absent case exit 0。
- **Verified**：git checkout 59d9f07^ -- scripts/probe-dll-string.cjs 恢复原字节（本提交 diff = 纯新增该文件）。
- AC1：达成（入库 + node --check 由 pre-commit/qa 后续复跑）。AC2：达成（qa-ci 三态）。

## 迭代史
- qa-ci 先期把恢复件放 .temp/tools 桥接（未入库）；lead 本提交正式入库，消除「下次还踩」缺口。