# 01 Intent（task-13）

昨夜发行两轮卡在「真源补拷」手动步骤（0401/0430 空转 apply）；磁盘 0.06GB 时 92MB 拷贝失败被误读成文件问题；今晨 lead 实战再挖出 ESBUILD_BINARY_PATH 穿透 store 硬链接致 esbuild@0.25.0 永久损坏（fresh install 必炸）+ worktree -UpdateDir 盲区。目标：把四处人肉/静默坑全部固化进脚本，验收=水位中止测试正确 + 一轮发布三件套落位且 SHA256 复算一致。
