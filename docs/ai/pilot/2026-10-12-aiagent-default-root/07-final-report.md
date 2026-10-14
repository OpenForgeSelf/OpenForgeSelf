# 07-final-report：AI-agent 插件文件三件套默认根修复

- **状态**：✅ COMPLETED_WITH_RISK（单测级完成；真实宿主浏览器走查与发布属 ②-⑥ 闭环，本批范围外）
- **交付物**：`Plugins/AIAgent/Services/ProjectWorkspaceService.cs`（DefaultRoot 三级候选 + ResolveSafePath 根回退/绝对路径直通）、`ProjectFileToolFunctions.cs`（三态封套 + usedRoot/hint）、`ForgeSelf.Api.Tests/Plugins/AIAgentTests/ProjectWorkspaceDefaultRootTests.cs`（16 例新增）。
- **验证**：dotnet build 0 错误（-o 旁路绕开宿主文件锁）；过滤集 `ProjectWorkspaceDefaultRoot` 24/24 通过（含 8 例同类回归）。均 Verified，原文见 05-evidence.md。
- **决策**：默认根候选 ③ 采用 AppContext.BaseDirectory 上溯找 `.slnx`/`.git`（用户指令口径，替换早版「进程 CWD」——CWD 随启动方式漂移）；工具类字段按用户指令用三态封套 + usedRoot 仅成功/可得时出现。背景/理由/准则见 mini-task 留痕与工作日记。
- **NOT-to-do**：未实现 `list_allowed_directories`/`get_working_directory` 标准工具（TODO 另批）；未动 folder_stats（#2 另批）；未动 web/ 与宿主 `ForgeSelf.Api/**`。
- **风险**：上溯根在非常规目录解析不到时有引导兜底；GUI 长会话回退行为 Unknown（未实测）。
- **提交**：改动在工作区，未经授权不 commit（与整体未提交批次一并待授权）。

## Validation
- `dotnet build` 0 错误（-o 旁路绕开宿主文件锁）——Verified
- 过滤集 `ProjectWorkspaceDefaultRoot` 24/24 通过（含 8 例同类回归）——Verified
- 判据：日志正文（通过数），非 exit code。

## Review
- 八问：需求满足（ProjectFileTool 默认根三级候选 + 三态封套）/ 实现最小（仅 ProjectWorkspaceService + ProjectFileToolFunctions + 新测试）/ 验证真实（单测 24/24）/ 边界（未动 web/ 与宿主代码）——均通过。
- **Final Decision：APPROVED**（单测级；真实宿主浏览器走查与发布在后续批次闭环，本批范围外，已在状态行标注 COMPLETED_WITH_RISK）。
