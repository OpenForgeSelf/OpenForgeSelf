# 05 Evidence（来源等级：Verified = 本机实测原始输出）

## 传感器 1：单测（Verified）
- `dotnet test ForgeSelf.Api.Tests --filter SearchTools|UniversalTool`（worktree wt/t_20bc15cf）
- 结果：exit=0，54/54 绿；全量日志存 t_b0a5e1fe 卡工作区。

## 传感器 2：live 行为对照（Verified，18891 多实例新二进制，dll sha256 eaf3e803…）
| # | 探针 | 修复前 | 修复后 |
|---|---|---|---|
| 1 | `database` | total=4 | total=4 保持（score 4→9 属 id 面预期增益，判非回归） |
| 2 | 中文词组 | 0 | total=3 |
| 3 | `database sql` | **0** | **6**（AC1 达成，matchedIn 含 id/描述面） |
| 5 | `mcp-center` | — | matchedIn 含 id |
| 6 | `zzz_no_hit_xxx` | 0 | 0（守卫语不回归，AC3） |
| 7 | 全角空格分隔 | 0 | total=6（AC2） |
| 9 | 守卫语 a/b | — | 不回归 |
- 对照结论：9/9 全过。中文/全角探针用 `--data-binary @file` 落盘物证（inline 字节流曾致假阴性，已用字节物证自纠，非一次跑绿）。
- 保真三验：18891 注册表 167 工具 = live 167；/health 同构；加载 dll sha256 eaf3e803… = 54 绿单测同一产物。

## 部署边界（非本卡证据，仅声明）
- live 18890 仍运行 11-26 旧二进制（versions/2.4.22.2611261743）——修复未部署，宿主更新由用户执行；qa 将以 probes-old 基线复跑部署面验证。

## AC 覆盖映射
AC1→#3 行；AC2→#2/#7 行；AC3→#6/#9 行；AC4→传感器 1。
