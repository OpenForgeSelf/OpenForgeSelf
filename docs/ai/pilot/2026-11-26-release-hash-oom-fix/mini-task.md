# mini-task: 发布链 SHA256 计算改 certutil 子进程，根治 Get-FileHash OOM（输入1 · 2026-11-26）

> 级别：轻量（3 个发布脚本小改）。Intent/Spec/Plan/Task 合并本文件；Evidence/Review 单独产出。

## 五要素

- **Intent（为什么/做什么）**：本地打包 `release-local.ps1` 全链跑到 zip+sha256 步时 `Get-FileHash` 对 145MB zip 抛 `System.OutOfMemoryException`。发布链是长跑 pwsh 进程（签名 37.6s 等多步累积内存压力），`Get-FileHash` 在同进程内分配大缓冲易 OOM。改为 certutil 独立子进程算 SHA256：进程内存干净、系统自带无依赖，根因级消除发布链中断点。
- **Spec（规格）**：
  - `release-lib.ps1` 新增 `Get-FileSha256 <path>`：内部调 `certutil -hashfile <path> SHA256`，解析输出取 64 位 hex 并小写返回；certutil 失败时抛错（不静默返回空串）。
  - 两处调用点替换：`package-release.ps1`（zip+sha256 步）、`release-local.ps1`（UpdateDir 复算验证步，同款 OOM 风险一并替换）。
  - 结果兼容：返回值与 `Get-FileHash ... .Hash.ToLower()` 完全一致（64 位小写 hex），SHA256SUMS.txt 格式不变。
  - **Interaction Design**：无界面交互（纯发布脚本改动，不涉及任何 UI）。
- **Plan（真实文件）**：
  - `scripts/release/release-lib.ps1`：新增 `Get-FileSha256` 函数。
  - `scripts/release/package-release.ps1`：哈希计算行替换。
  - `scripts/release/release-local.ps1`：UpdateDir 验证行替换。
- **Task（边界）**：Allowed=上述 3 个发布脚本 + pilot 工件；Forbidden=打包流程步骤顺序、签名逻辑、updates 目录结构、任何业务代码。
- **Acceptance Criteria**：
  - [x] AC1 `Get-FileSha256` 输出与独立基线（`sha256sum`）一致（05 实测一致：5c188a75…da45c）
  - [x] AC2 次日全链打包中 zip+sha256 步顺利通过（昨日 OOM 点，实测 10.8s 完成，未再 OOM）
  - [x] AC3 UpdateDir 复算验证路径走新函数且 PASS（05：verified=1）

## 适用问题类

无（发布工具链健壮性修复，非产品缺陷/回归；同类排查范围限于 release 脚本内 Get-FileHash 调用，两处均已替换）。关联环境事实：145MB zip + 长跑进程内存压力为触发条件。

## 闸门1

用户输入1（2026-11-26 13:33「本地打包脚本执行到 zip + sha256 步骤失败……帮我继续」）+ 修复方案在续跑中当场上报并获默认认可（修复后产物全链验证通过、次日复跑复证）= 批准。
