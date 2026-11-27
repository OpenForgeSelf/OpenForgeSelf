# 06-review：发布链 SHA256 certutil 修复

## 审查结论

- **Final Decision：APPROVED**（2026-11-27，次日全链打包复证通过后自审）。

## 审查要点

1. **正确性**：`Get-FileSha256` 返回值与 `Get-FileHash .Hash.ToLower()` 逐字节一致（AC1 双源比对）；下游 SHA256SUMS.txt 格式与 update-agent 校验逻辑零改动。
2. **失败路径**：certutil 不存在/非零退出 → 抛错终止发布（fail-fast），不静默产出错误哈希。
3. **影响面**：仅 release 脚本 3 文件，业务代码零改动；`release-lib.ps1` 为公共库，新增函数无副作用。
4. **备选方案回顾**：流式分块自算哈希（自实现易错、无必要）与 .NET 流式 `HashAlgorithm`（仍在同进程，未消除内存压力根因）均弃用；certutil 独立子进程为根因级方案。

## 遗留

无。
