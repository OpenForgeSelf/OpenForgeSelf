# Evidence

> Stage 7 evidence will be filled after the focused test and implementation pass.
# Evidence

## 1 Red

- `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PilotExemptionAllowlist_MustBeSharedByCommitAndCi" --no-restore --verbosity minimal`
- Result: FAIL（来源等级：Verified）
- Cause: `scripts/pilot-exemption-allowlist.json` and `scripts/check-pilot-exemption.ps1` did not exist.
- The test also confirmed the old implementation needed no compile namespace before reaching behavior.

## 2 Green

- Same focused dotnet test command.
- Result: PASS（来源等级：Verified）
- Output: 1 passed, 0 failed, 0 skipped.

## 3 Direct Allowlist Probes

- Allowed-only list containing `.gitignore`: PASS, exit 0.
- Mixed list containing `.gitignore` and `ForgeSelf.Api/Program.cs`: FAIL, exit 1; unexempt output contains `ForgeSelf.Api/Program.cs`.
- Custom temp config with `TeamTool/**`: custom `TeamTool/settings.txt` allowed and exit 0.

## 4 Hardcoding Review

- `scripts/hooks/commit-msg` no longer filters by hardcoded docs/README/TODO/.forgeself/Markdown categories.
- `scripts/verify-pilot-gate.ps1` no longer uses `Test-IsDoc`; commit and scope checks consume the unexempt list returned by the shared checker.
- Result: PASS（来源等级：Verified）。

## Known Limitations

- Full repository test suite was not rerun; this is a repository-script and git-hook infrastructure change, validated by one focused repository guard plus direct checker probes.
- Push result is still pending real remote verification.
