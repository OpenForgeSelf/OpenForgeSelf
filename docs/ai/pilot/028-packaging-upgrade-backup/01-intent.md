# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：PILOT-028 ｜ 日期：2026-09-28

## Problem

1. 打包 / 升级备份 / 缓存 / 备份相关规则散落多处（AGENTS.md、agent-workflow.md、035/036/038 功能文档、两个插件技能），无单一真源，规则冲突时无法判定。
2. 空间浪费六处（真源 §2）：
   - 宿主每次升级做**整目录 robocopy 备份**到 `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>\`，永不清除（实测一次升级备份耗时 39s，体量等于整个安装目录）；
   - 更新缓存 `Updates/<tag>/` 应用后从不清理；
   - 插件 `_backups/<id>/<ver>/` 暂存与备份双语义（新版本先复制进 `_backups` 再复制进 `versions/`，两次拷贝）；
   - 插件本身已多版本共存（versions/ 即回滚能力），`_backups` 纯属冗余备份；
   - `ImageRecognitionCache` 会话目录（数据根下）永不清理，只增不减。
3. 打包后程序目录结构非 QQNT 式：公共内容与每次要更新的宿主本体混在一起，更新 = 覆盖式替换（历史教训：运行中宿主锁 DLL 被静默跳过、publish 残留旧版本）。

## Why

- 用户输入30：要求找全相关规范、统一一处真源、评估 QQNT 式目录组织（公共的放外面、宿主每次更新的放 versions/、插件目录跟 versions/ 并排、插件目录不需要备份文件夹）。
- 用户输入31：明确批评「改个文档就提交、不改代码」，要求直接改代码落地优化，「只看最终结果，发布、更新都合理，有统一真源」。

## Expected Outcome

- **真源唯一**：`docs/04-standards/packaging-upgrade-backup.md` 为打包/升级备份/缓存/安装目录结构的唯一真源（已完成，输入30）。
- **批次1（已实施）**：
  1. 插件去 `_backups`：新版本（包源/侧载）直落 `versions/<ver>/`，更新 = 激活已直落版本（切 current 指针），零复制；回滚只认 versions/ 保留版本；删除 BackupPlugin/GetBackupList/RestoreFromBackup 等整条备份链路。
  2. 宿主升级去整目录备份：update-agent.ps1 不再备份整个安装目录；应用成功后清理 `Updates/<tag>/` 暂存；`Backups/` 存量退役清理。
  3. 图片缓存 TTL：ImageRecognitionCache 初始化清理超过 30 天的会话目录。
  4. 相关测试全部绿；文档（真源/035/038/agent-workflow/Plugins README/guides）同步为「去 _backups」语义。
- **批次2（待立项）**：宿主 QQNT 式目录结构（程序根目录公共外置 + `versions/<ver>/` 按版本放宿主 + `plugins/` 与 `versions/` 并排），发布脚本布局改造 + 端到端升级演练。

## Constraints

1. 唯一真源 = `docs/04-standards/packaging-upgrade-backup.md`；其他文档只保留操作流程与踩坑，不重复承载结构事实。
2. **禁止 agent 停/启/杀任何用户宿主进程**（含 `D:\src\tools\ForgeSelf`、`:51888` 实例）；宿主升级一律由 update-agent 自更新。
3. 活动插件目录只放插件自身 DLL；宿主共享 DLL（XCode.dll/NewLife.*.dll/ForgeSelf.*.dll）入插件目录 → 宿主启动即崩。
4. 插件数据目录 = `{数据根}/Plugins/{id}`（随数据走），发布目录不得放数据。
5. 插件多版本共存（versions/ + current）即回滚能力，**不再需要任何额外插件备份**。
6. git 提交/推送须用户明确指示（用户偏好）；本任务只改不提交，工件链齐备后由用户审核。
7. 批次2（宿主结构）属高风险架构变更，**先请示用户拍板再实施**。

## Success Criteria

1. 真源文档存在且被 AGENTS.md 引用（✅ 已完成，输入30）。
2. 插件更新链路无 `_backups`：grep 全仓 `_backups|BackupPlugin` 残留仅剩历史冻结工件（027 pilot）/防御性兼容（脚本、测试注释），活动代码/文档零残留。
3. update-agent.ps1 无整目录备份步骤；`Updates/<tag>/` 应用后清理；`Backups/` 存量退役清理。
4. ImageRecognitionCache 初始化执行 TTL 清理（>30 天会话目录删除）。
5. `ForgeSelf.Api dotnet build` 0 error；插件相关测试过滤集全绿；全量测试失败项与本任务零文件交集。
6. 发布/更新链路行为合理（打 tag 自动发布 / 页面自动更新不变）。
