# Evidence

## AC1

Verified。`pnpm exec vitest run src/stores/tabs.test.ts src/router/__tests__/dynamicPlugins.test.ts`：Test Files 2 passed，Tests 17 passed。日志 `.temp/tmp/agent-page-tests.log`。其中包含默认标签「智能体」。

随后又改了设置与聊天文案，补跑上述两个文件加上 `src/__tests__/ChatView.test.ts`：Test Files 3 passed，Tests 34 passed。日志 `.temp/tmp/agent-page-tests-2.log`。

问题类：`check-problem-classes.ps1` 输出 `OK：21 个 PC`。`ProblemClassRegistryTests --no-build` 通过 5。带构建的那次被本机开发栈进程锁住 `ForgeSelf.dll`，没有停该栈。日志 `.temp/tmp/pc021-check.log`、`.temp/tmp/pc021-registry-2.log`。

## AC2

Verified。同一批 17 个测试里新增用例「未匹配兜底路由不把插件声明路径判成冲突」，断言路径为 `/ai-agent`。

## AC3

Verified。开发栈 `http://localhost:7399`（后端 7301）。带上本栈已有密钥后打开 `/ai-agent`：无障碍树顶栏按钮名为「智能体」；主区域有「新会话」「输入消息」、对话框名「智能体」、问候语「你好，我是铸己匣智能体」。截图时间 2026-11-02 17:57，页内标题「智能体 v1.9.9 就绪」。

设置页：分类按钮名为「智能体」；通用页说明为「智能体使用的默认推理模型」「智能体的系统级指令」。

修冲突检测之前的对照（Verified）：同一地址主区域文案是「这个页面没有加载出来」。更早无密钥时主区域是空注释，清单 401。

## 其他

`pnpm run check`：0 errors，153 warnings。日志 `.temp/tmp/agent-page-check.log`。这次改名在该次 check 之后，文案替换不涉及类型。

`pnpm run test`：Test Files 90 passed，Tests 1042 passed。日志 `.temp/tmp/agent-page-vitest.log`。该次在设置/聊天改名之前。改名后的聊天断言见 AC1 的第二次日志。

`pnpm run design:check`：style-tokens PASS，design-compliance PASS。日志 `.temp/tmp/agent-page-design.log`。

插件界面：`Plugins/AIAgent/web` 的 `pnpm run build` 产出 `dist/index.js`，日志 `.temp/tmp/aiagent-web-build.log`（built in 1.02s）。

首页 `http://localhost:7399/home` 功能格里仍有一项名为「AI Agent」（Verified）。顶栏仍是「智能体」。该项来自 Home 插件自己的功能表，本回合没有改插件源码。

未跑全量 e2e。未发布。未停 :51888。
