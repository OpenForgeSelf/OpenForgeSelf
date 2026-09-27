# 批次B · 多供应商 AI 供给目录 — 02-spec（S2 规格 · 需求分析师落稿）

- task-id: `batch-b-provider-catalog`
- 作者：需求分析师｜落稿：2026-09-27
- 状态：**草案定稿留档，待闸门1（U-1~U-5）用户批复；批复前不得进入 S5**
- 裁决基线：seq123 群内闸门1审批包「本批范围」节 + seq124 私信 6 条入册裁决与勘误 + seq131 第 3 点修正（本文逐条对账）
- 需求来源：seq121 用户「供应商可选可配 + key 加密落库」

## 0. 引用勘误声明（seq124/131，开发按本节口径执行）

| # | 原错误引用 | 勘误后口径（仓库实证） |
| --- | --- | --- |
| E-1 | 「AiPilotService 生产直读明文」 | 全仓 `git grep -i aipilot`（HEAD+工作树）=0 命中，该类不存在。AI:ApiKey 真实消费方 = `ForgeSelf.Api/Services/ConfigurationService.cs`:43/:67（兜底 seed）+ :102（回吐）+ 测试直读 3 处（AgentFrameworkToolCallIntegrationTests.cs:51、ChatRecordRealLLMTests.cs:51、ConfigurationServiceTests.cs fixture）。定性：**兜底配置残留 + 测试直读**；处置对象以「枚举全部调用点后的真实类」表述 |
| E-2 | 「AIProviderController 静态 MaskApiKey」 | 不存在。真实掩码模式 = `AIProviderController.cs`:237 `_encryption.Mask(_encryption.Decrypt(e.ApiKey))`（实例注入服务）。新增读取路径**必须复用同一注入服务，禁止造第二个掩码实现** |
| E-3 | 「实体 ProviderType=OpenAI/Ollama/AzureOpenAI/Custom 4 值枚举」（seq124 勘误3 本身有误，seq131 撤回） | 实体 `ForgeSelf.Api/Entities/AIProvider.cs`:47 ProviderType 为 **String 列**，值域即列注释三值 OpenAI/Anthropic/Custom；Abstractions `AIConfigModels.cs`:27 `enum AIProviderType` = 同三值。系别结论不变：**两套类型系别（实体 String 列 vs Abstractions 枚举）勿嫁接，任何 type→协议映射必须声明作用对象（以实体 String 值域为准）**；`OpenAICompatibleProvider.cs`:275/280 被注释的 Ollama/AzureOpenAI case 为干扰源，不得作为值集依据 |
| E-4 | 闸门1包 U-1 措辞「写死的 4 值枚举」 | 不准确（更正通知由项管哥发用户）：实体已是 String 列，硬死点在 前端下拉（AiProvidersPanel.vue:543-545）+ 目录文件 + 设置页三处；**U-1 方案 A 实际无 schema 枚举迁移，比原描述更轻**。决策点与推荐不变 |

## 1. 目标与不做什么

目标：把「AI 供应商」从散点硬编码升级为数据目录——DeepSeek/Kimi 等页面可选可配；key 统一加密落库且明文永不出库；OpenAI 兼容调用链保持不变。

明确不做（超出本批即违规）：Anthropic/Claude 真实外发协议客户端（下一批）；allowed_tools 生效过滤（下一批单独小任务）；appsettings AI 兜底移除（本批只标废弃入 TODO，下轮删）；端口/文档长期策略。

## 2. Functional Requirements（对账 seq123「本批范围」逐条）

- FR-1 目录文件：新增供应商目录数据文件，**随安装包升级**；目录行含 vendor_code、显示名、默认 endpoint、协议类型映射（映射作用对象=实体 String 值域，见 E-3）。DeepSeek/Kimi 等以目录行标识、走现有 OpenAI 兼容协议，不新增后端枚举（U-1A）。
- FR-2 AIProvider 实体扩展字段：`vendor_code` / `is_visible` / `allowed_tools` / `updated_at`。实体改动一律 `Data/Model.xml → xcode` 生成，禁手改生成件（plugin-development 铁律9；宿主实体同规）。
- FR-3 key 四路加固：①新建=提交即加密入库；②更新=空 body/缺字段 → 保留旧密文不覆写；③读取=所有 API 响应仅回掩码（复用 E-2 实证模式，格式 sk-abc****xyz）；④历史数据一次性迁移（FR-6）。
- FR-4 目录可见性与「先选后用」（U-2A）：Anthropic/Claude（千帆同理）目录可见可选，页面显式标注「真实外发下一批支持」；本批不承诺外发。
- FR-5 默认模型集中管理（U-5 分级）：默认供应商/默认模型落位 provider 表单源真源，设置页配置立即生效；ConfigurationService AI 兜底路径保留但标废弃（注释 + TODO 登记），不新增直读明文调用点。
- FR-6 历史数据一次性迁移：既有 AIProvider 行补齐新列默认值；ApiKey 密文原样保留（迁移前后逐行 hash 一致）。
- FR-7 设置页：供应商单选（来自目录）+ provider 表单（现有 AiProvidersPanel 扩展）。
- FR-8 allowed_tools（U-4A）：本批只落字段 + UI 透传，不做行为过滤。
- FR-9 测试矩阵：后端单测（含迁移与四路 key 路径）+ 集成测试 + 2 条插件/宿主层 e2e（目录可选可配主链路、key 掩码不出库）。

## 3. Input / Output

- Input：设置页表单（provider 创建/更新/删除）、目录文件（安装包内只读种子）、迁移触发（宿主启动幂等检测）。
- Output：provider CRUD API 响应（ApiKey 恒为掩码字段）、前端下拉/目录列表（is_visible 过滤）、日志（迁移结果，禁输出任何 key 明文/密文）。

## 4. Business Rules

- BR-1 明文永不出库：任何 API 响应、日志、错误信息不得含 ApiKey 明文；掩码统一走 `_encryption.Mask(_encryption.Decrypt(...))` 单一实现。
- BR-2 更新语义：PUT 缺省/空 ApiKey = 保留旧值；显式非空 = 重新加密覆盖。
- BR-3 type→协议映射以实体 ProviderType String 值域（OpenAI/Anthropic/Custom）为准；目录行的协议类型必须落在该值域内；禁止把 Abstractions 枚举或注释 case 当值集。
- BR-4 目录与用户数据分层：目录文件只读升级不覆盖用户已建 provider 行；is_visible 控制展示不删数据。

## 5. Boundary / Error Handling

- 迁移幂等：重复启动不重复迁移；迁移失败不阻断宿主启动，记 FAIL 日志并入健康可见性。
- 目录缺失/损坏：设置页回落显示既有 provider 行 + 显式空态提示，不白屏。
- 空 vendor_code / 重复 vendor_code：创建请求 4xx 带原因；前端表单预校验。
- 更新不存在的 provider id：404，与现行为一致。
- 兼容性：旧客户端脚本若曾读明文 key 将断（U-3 已知代价，用户批复为准）。

## 6. Compatibility / NFR

- OpenAI 兼容调用链零行为变化；现有 provider 迁移后立即可用。
- 无新增第三方依赖；性能敏感点仅目录读取（本地文件/内存缓存）。

## 7. Acceptance Criteria — 入册版 AC-9~14

（seq124 裁决「AC-9~14 全部符合规范入册」；草案原文双方同批丢失（私信未入库），项管哥 seq138 核对通过、零回炉，确认**本节重建版为唯一入册文本**，此后任务书/测审一律引用 02-spec §7，双源风险已关闭；AC-11④ 直查 DB 强化断言获采纳；「运行实例以 :7102 真源」为任务书横切验收前提，不占 AC 编号。）

- AC-9（U-1A）：新增 DeepSeek/Kimi 等仅以目录行（vendor_code）出现；后端 ProviderType 值域与 Abstractions 枚举保持三值不变，全仓无新增供应商枚举；U-1A 落地无 schema 枚举迁移（E-4）。
- AC-10（U-2A）：Anthropic/Claude 目录项页面可见可选且带「下一批支持外发」标注；选择保存成功、配置回读一致；本批不出现外发成功断言。
- AC-11（U-3/FR-3）：①provider 读接口响应 ApiKey 恒为掩码（sk-abc****xyz），全仓 grep 无第二个掩码实现；②PUT 空 ApiKey 后 request 复核密文 hash 未变；③明文 key 在响应/日志 0 命中；④新建提交即入库为密文（直查 DB 断言非明文）。
- AC-12（U-4A/FR-8）：allowed_tools 可写入、UI 回显透传；不带该字段时工具调用行为与变更前基线完全一致（回归零 diff）。
- AC-13（U-5/FR-5）：默认供应商/默认模型仅从 provider 表读取，设置页修改后新会话即生效（无需重启）；ConfigurationService 兜底路径标废弃注释 + TODO 在册；无新增 AI:ApiKey 生产直读点（调用点枚举清单入 05-evidence）。
- AC-14（FR-6 迁移）：既有 AIProvider 行新列按默认值补齐、ApiKey 密文逐行 hash 迁移前后一致；迁移幂等（二次启动 0 变更）；迁移失败路径有日志且宿主可正常启动。

（A-1~A-8 主体验收标准以 seq123/124 既有口径为准，开发产出 Intent/Spec 收录时合并编号。）

## 8. Unknown（显式登记，禁止发明）

- U-1~U-5 用户批复结果（闸门1 未回）——批准后若有非推荐项，本 spec FR/AC 相应节回炉修订。
- 目录文件具体格式与落点（JSON/表、安装包内路径）：属 S3 Plan 阶段开发定位项，本 spec 只锁定「随安装包升级 + 只读种子 + 不覆盖用户数据」约束。
- 供应商初始目录清单（首批收录哪些 vendor_code）：待闸门1 批复后由任务书给定，不在分析侧拍板。
