---
id: 2026-11-02-memory-timestamp-minvalue
title: 记忆新建/导入时间戳为 0001-01-01，更新/软删不推进 UpdatedAt
pc: PC-022
severity: P2
evidence: Verified
---

# Case：记忆时间戳缺省 0001-01-01（新建/导入不写 CreatedAt/UpdatedAt）

- **日期**：2026-11-02
- **发现渠道**：浏览器逐功能走查（用户指令：逐个点击验证 + 截图）——记忆管理页卡片「创建时间 1/01/01 00:00」。
- **现象**：`POST /api/memory` 新建与 `/api/memory/import` 导入的记忆 `createdAt=updatedAt=0001-01-01T00:00:00`；`PUT /api/memory/{id}` 更新与软删也不推进 `updatedAt`。
- **根因**：`Plugins/MemorySystem/Services/MemoryServiceXCode.cs` 两处 `new Memory{...}`（CreateAsync :82 / ImportMemoriesAsync :408）未赋 `CreatedAt/UpdatedAt`；`UpdateAsync`(:131)/`DeleteAsync`(:175) 的 `Update()` 前未设 `UpdatedAt`。XCode 实体不会自动填时间戳；同文件 `CorrectAsync`(:164) 已正确设置 `UpdatedAt = DateTime.Now`，属于同类遗漏未对齐。
- **修复**：两处插入补 `CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now`；更新/软删补 `entity.UpdatedAt = DateTime.Now`。
- **守卫（G1）**：`ForgeSelf.Api.Tests/Integration/MemoryTimestampGuardTests.cs`（create/import 非 MinValue + update 推进 UpdatedAt；[Collection("XCode")] 真库测试，先红后绿）。
- **同类扫查**：本文件内 4 处写入点全部覆盖；其他插件的插入路径以「实体是否含 CreatedAt 且由代码赋值」为准另行巡检（记 TODO 巡检项，不阻塞本任务）。
- **教训**：XCode 直插实体时时间戳字段必须显式赋值；走查（UI 层）能暴露单测 mock 掉的持久化细节——`MemorySystemIntegrationTests` 全部 mock 了 `IMemoryService`，服务实现层零覆盖，这类缺陷只有真库测试或真实走查能抓到。
