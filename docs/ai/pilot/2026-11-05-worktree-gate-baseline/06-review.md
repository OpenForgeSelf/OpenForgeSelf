# 06 Review（task-14）

1. 同步会不会吃掉分支的合法棘轮抬升？不会——分支自 merge-base 改过基线（-Update 合法抬升）或本次暂存改基线 → 跳过同步尊重分支版本。
2. 只动工作区安全吗？git checkout -- 恢复两个已跟踪 JSON，不碰暂存区/提交内容；拒绝路径与通过路径都恢复。
3. main 不可用（detached/无 main/无 origin/main）？BASE_SHA 或 MAIN_REF 为空 → 跳过同步 = 保持旧行为（fail-open 保守方向）。
4. globalSetup 抛错会不会破坏合法回退？config 存在（后续启动态）时永不抛，只对「init 空+config 缺」的必败态 fail-fast——比 38 分钟全量假红便宜。
5. Final Decision: **APPROVED**（自审；worktree 真实验收 + 双档门禁）。
6. **db-dev merge 卡点裁决记录（lead 转）**：四 pilot 目录用 hook 同参逐个直跑复现——qa-ci 三目录全 PASS（无需豁免/补件），唯一 FAIL=st-deadsession 缺「无界面交互」显式声明（-EnforceInteraction 项）；已补节修复并提交 main（3a7ab62）。结论：非 hook 参数漂移、非轻量目录豁免问题——verify-pilot 与 hook 严格度本就一致（WARN→FAIL 只差 -Enforce* 开关，hook 开关是设计意图）。
