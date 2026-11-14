# Grok 在本仓库怎么用

多 agent 默认已开启，不要再找总开关。只读调查用子代理 `ofs-explore`。改动审查用子代理 `ofs-review`，或工作流 `/workflow ofs-review`。子代理要改文件、且会和当前工作区抢同一批文件时，`isolation` 设为 `worktree`。工作树只隔离该子代理的改动，不会自动合并回主工作区。

新会话工作树：`Ctrl+N` 连按两次，选 worktree。或启动时 `grok --worktree=<名字>`（等号不能省）。基于某分支：`grok -w --ref main`。用户配置里 `[hints] new_session_worktree_mode` 为 `ask` 时，`/new` 才会问要不要建工作树；`never` 会跳过提问。

子代理同样遵守：

- 脚本只用 `pwsh`，不用 Windows PowerShell 5.1。
- 不停止、不启动、不杀掉 `:51888` 和用户正在跑的宿主。
- 本地前后端只用 `pwsh -NoProfile -File scripts/dev-stack.ps1`。
- 不改 `~/.grok/config.toml` 里的密钥和模型项。
- 业务代码仍走仓库 `Agents.md` 的 pilot 闸门。本文件只约束 Grok 怎么分工，不代替那套流程。

审查一个 diff 或目录：`/workflow ofs-review {"target":"HEAD"}`。`target` 可以是路径、引用或 `git diff` 能识别的范围。
