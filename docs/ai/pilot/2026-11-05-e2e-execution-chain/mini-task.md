# mini-task：插件冒烟 e2e 真执行覆盖（task-11）

- **Intent**：昨夜三缺陷（PermissionMode 枚举 400/执行链 P0/持久化 P1）全部逃过页面加载型冒烟——把「冒烟必触达执行链」立为技能铁则 + SmartTerminal 补主链用例。
- **Spec**：① e2e-testing SKILL.md 新节「冒烟必须触达执行链」：POST 主资源→POST 执行动作→读回断言副作用三段最低标准 + 契约回归锚（请求体带最近修过的字符串枚举）+ 唯一标记防残留假绿 + 无执行链插件才许降级并注释理由。② smart-terminal.spec.ts 新用例「main chain: create session → run echo → assert stdout/exit」：真实 POST 链，permissionMode:'AskWrite' 字符串契约锚，commands 记录断言 status=completed + exitCode=0。
- **Plan**：读技能+控制器 DTO → 写用例 → 快档 2/2 绿 → 技能条款入库。
- **Task**：Allowed=.agents/skills/e2e-testing/SKILL.md, ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts; Forbidden=e2e/global-setup.ts（免中档）, 其他插件 spec。
- **risk**: low —— 纯新增用例+文档条款，不碰共享基建
- **expected_files**: .agents/skills/e2e-testing/SKILL.md; ForgeSelf.Web/e2e/plugins/smart-terminal/smart-terminal.spec.ts
- **feature**: 100-INFRA-1（e2e 门禁增强）
- **rollback**: git revert 单提交即回滚；用例独立不牵连既有 28 用例

## 无界面交互

本任务不新增 UI 交互；新用例经宿主 REST（POST /api/smart-terminal/sessions → POST sessions/{id}/run → GET output/commands）操作，鉴权 Bearer token，断言 stdout/exit 码；页面仅承载既有标题断言。
