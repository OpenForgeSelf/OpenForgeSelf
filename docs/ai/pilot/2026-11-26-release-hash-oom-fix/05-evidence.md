# 05-evidence：发布链 SHA256 certutil 修复

## 验证记录

| # | 验证项 | 命令/手段 | 结果 | 等级 |
|---|---|---|---|---|
| 1 | AC1 新函数与独立基线一致 | `sha256sum` 独立算 145MB zip 为 `5c188a75…da45c`；`Get-FileSha256` 输出比对 | 一致，PASS | Verified |
| 2 | AC1 SHA256SUMS.txt 生成 | 写入 `artifacts/release/SHA256SUMS.txt` 并读回校验格式 | PASS | Verified |
| 3 | AC2 次日全链打包复证 | 2026-11-27 打包 2.4.22.2611261743：package-release -Sign 全绿，zip+sha256 步 10.8s（昨日同点 OOM） | PASS，OOM 未复现 | Verified |
| 4 | AC3 UpdateDir 复算走新函数 | 拷贝 zip/SHA256SUMS/RELEASE-NOTES 到 updates 后复算 | verified=1，PASS | Verified |
| 5 | 签名链无回归 | Authenticode 签名 6/6 通过、校验通过 6/6 | PASS | Verified |

## 来源说明

- 全部证据来自本地真实打包执行（非 mock）；日志留存 `.temp/rel-package.log`、`.temp/release-local-20261126-pm.log`（gitignored）。
