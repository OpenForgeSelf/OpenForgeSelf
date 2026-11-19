# Evidence

| Check | Result | Grade |
| --- | --- | --- |
| `pwsh scripts/dev-stack.ps1 -SkipBuild` | Backend `:7301` 200, Frontend `:7399` 200; state written | **Verified** |
| Prior failure | `Start-Process … -WindowStyle` → parameter not supported on this edition | **Verified** (log `oar-ui-evidence/dev-stack-start.log`) |
| Side use | AgentHub UI screenshots via Playwright against this stack | **Verified** (`/opt/cursor/artifacts/oar-ui-evidence/`) |

Unknown: Windows regression of Hidden window style (not run in this Linux cloud VM).
