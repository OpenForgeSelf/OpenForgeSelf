# 06-review · 复盘

## Final Decision
**APPROVED**——三阶段目标达成：门禁三缺口归零且各配守卫（Verified）、3 条规则下沉 lint 并压缩 prose、死引用清理完毕；遗留项已记 TODO，不阻塞交付。

## 做对了什么
- 门禁修复每项配行为守卫（临时仓库/临时文件实跑断言），不是"改完看起来对"。
- 固化迁移遵守「同一事实只允许一处承载」：守卫落地即压缩 prose 为指针。
- `eng.pwsh-only` 正则收窄为只拦调用形态（`powershell(.exe)?\s+[-/]`），避免误拦"PowerShell 7"提及——lint 可信度优先于覆盖面。

## 教训（已沉淀）
1. **给 UTF-8 文件补 BOM 只允许字节级操作**（EF BB BF 前缀），禁止 Get-Content/Set-Content 文本往返——编码双重转换会把脚本毁成 mojibake 且破坏语法结构。本次由守卫测试当场抓红，修复后转绿。
2. **hook 源（scripts/hooks/*）更新后必须重跑 `install-git-hooks.ps1`** 同步 `.git/hooks/`——实证 OUT-OF-SYNC 会静默漏新门禁（演示时第 5 道门缺席即此因）。已记 TODO 候选 postinstall 自动比对同步。
3. PowerShell 单元素数组解包陷阱（`return $rules` / `ReadAllLines`）会把数组变标量、遍历变逐字符——凡返回集合一律逗号包裹 `return ,$x`。

## 遗留（详见 TODO.md「工程流程改造遗留三项」）
- denylist 灾后重建仅 6 词条（凭记忆），待备份盘点核对完整性。
- 全量 32 个既有测试失败待分类清理（非本任务范围）。
- hook 源更新后手动同步缺口，候选 postinstall/CI 自动比对。

## 剩余 prose 瘦身候选（本次未做，需用户拍板）
① a-workflow-docs-report.md §A10 plugin-team-sop 章节；② README.md §A8/A10 地址表行；③ b-engineering-daily.md B1 与其他分册重复条款；④ .agents/skills/functional-walkthrough 的 MindHub 外项目残留。
