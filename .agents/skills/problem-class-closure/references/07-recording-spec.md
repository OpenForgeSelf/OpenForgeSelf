# 07 记录规范（P6：目录、命名、front-matter、登记、分工）

## 目录与命名

```
docs/11-troubleshooting/
├── index.md                  # 现象速查（报错关键字入口；每行指向 case/PC 或标散例）
├── problem-classes/
│   ├── README.md             # 台账（tags 检索表 + 总表 + 悬空检查 + 状态语义）
│   └── PC-NNN-<slug>.md      # id 三位递增，slug 全小写连字符
└── cases/
    ├── README.md             # 时间线索引
    └── YYYY-MM-DD-<slug>.md  # front-matter id 与文件名逐字一致
docs/18-templates/postmortem/ # 模板（schema 单一来源）
```

- PC slug 描述**不变量的违反面**（`cross-plugin-contract-shared-table`），不描述某次现象（`workflow-500`）；
- case slug 描述**这一次发生了什么**（`gateway-list-todos-scope`）；
- 编号只增不复用；候选未定性的 PC 也占号（status: open，如 PC-004）。

## front-matter 填写规约（机器可读子集，格式约束以模板为准）

- **不使用 YAML 库**：`check-problem-classes.ps1` / Registry 测试用正则逐行解析——键值一行一条，flow-map（`{...}`）必须单行，嵌套结构用"一行一个 flow-map 的列表"；
- `status` 只能取 `open | guarded-partial | guarded | structural`；
- `guards[].todo` 的文本必须能在 `TODO.md` **逐字检索到**（Registry 测试 grep 该片段）；
- `allowlist[]` 每项必须带 `expires`（`YYYY-MM-DD`），过期即红——填日期时按"下一次评估批次"给，不填 2099 这种假期限；
- `lens` 是**给下个任务的问题句**（"我提供/消费的契约是否经 ctx.Register？"），写成疑问句，2-4 条，直接可抄进 Spec Failure Modes 节。

## 三处登记（缺一即 Registry 测试红）

新建/更新 PC 或 case 后必须同步：① `problem-classes/README.md` 台账行；② `cases/README.md` 索引行（仅 case）；③ `index.md` 速查行（新报错关键字入口时）。可跑脚手架 `scripts/new-problem-case.ps1` 自动挂/建 + 更新索引。

## 分工铁律（同一事实只放一处）

| 载体 | 承载 | 不承载 |
|---|---|---|
| case | 这一次的现象/经过/扫查表/逃逸 | 规则、修法细节（引 PC） |
| PC | 不变量、签名、canonical fix、守卫台账、lens | 事件经过（引 case） |
| `index.md` / 两处 README | 指针与状态 | 根因正文 |
| AGENTS.md / 各技能 | **一行指针** | 规则复述 |
| 规则本体真源（如 packaging-upgrade-backup.md、AGENTS §2.3） | 不变量正文 | ——PC 指向它，不复制（如 PC-006/007） |

## 07-final-report 三个新节（缺陷类任务必填）

- **问题类归档**：PC id + case id + 守卫状态（或"无需建类 + 理由"）；
- **重构提案**：按 `04-refactor-proposal.md` 格式（无也写"无+理由"）；
- **悬空教训清单**：必须为空——报告正文任何"建议/遗留/Minor"都在此表落成 TODO id 或 PC id，空表才放行（pre-commit `-PilotTask` 校验此三节存在性）。
