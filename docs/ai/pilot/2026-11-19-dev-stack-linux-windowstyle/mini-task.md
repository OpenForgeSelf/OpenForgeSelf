# Mini-task: dev-stack Linux Start-Process WindowStyle

## Intent
Cloud/Linux `pwsh` rejects `Start-Process -WindowStyle Hidden`, aborting origin stack before probes. Unblock `scripts/dev-stack.ps1` on non-Windows without changing product features.

## Spec
- **FR1**: On Windows, keep `-WindowStyle Hidden` for backend/frontend Start-Process.
- **FR2**: On Linux/macOS (`-not $IsWindows`), omit WindowStyle so Start-Process succeeds.
- **FR3**: Ports, env (`FORGESELF_*`, `--console` first), state file `.temp/dev-stack.json` unchanged.
- **Interaction Design**: 无界面交互（仓库脚本基础设施，无用户 UI）。

## Plan
1. `scripts/dev-stack.ps1` — wrap BE/FE Start-Process args in hashtables; set WindowStyle only if `$IsWindows`.
2. Verify: `pwsh scripts/dev-stack.ps1 -SkipBuild` → health 200 + frontend 200 + state json.

## Task
- **Allowed**: `scripts/dev-stack.ps1` only.
- **Forbidden**: product UI/API, killing `:51888`, Windows-only token decrypt changes (out of scope).
- **Verify**:
  ```bash
  export NO_PROXY=localhost,127.0.0.1,::1
  pwsh -NoProfile -File scripts/dev-stack.ps1 -SkipBuild
  # expect backend/frontend 就绪; cat .temp/dev-stack.json
  ```
