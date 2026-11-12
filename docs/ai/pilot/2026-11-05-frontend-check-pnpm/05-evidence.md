# 05 · Evidence — 2026-11--frontend-check-pnpm

> 证据分级Verified / Inferred / Unknown。

- **Verified** — `git diff` 实证：action-setup@v4（version 10.13.1）位于 setup-node 之前；`corepack enable` 步已删；cache: pnpm 与 cache-dependency-path 保留。
- **Verified** — ForgeSelf.Web/package.json packageManager = pnpm@10.13.1（版本对齐，qa-ci 先前读文件核实；与 release.yml 同参）。
- **Verified** — `pnpm install --frozen-lockfile` + `pnpm run check` 在 wt-qa 跑通：check 输出 0 errors + component-conventions PASS（日志 .temp/pnpm-check-ta.log）。
- **Unknown** — GitHub Actions runner 上的实际运行（本机无 push 凭据，无法触发 CI；下次 push 后由 Frontend Check 实测收口）。
- AC1：达成（diff 实证AC2：（本地）。

##代史
- 首轮：qa-ci 定位 corepack 失效与 setup-node cache 顺序根因（GitHub annotations + 本地复现），改 yml。
- 收尾：check 绿后补齐本轻量工件四件（lead 代落盘，qa 侧长回合未产出写入）。