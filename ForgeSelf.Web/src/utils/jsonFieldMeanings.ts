// 聊天记录请求/响应 JSON 常见字段的中文含义，用于「原始数据」树状视图每键对照。
// 覆盖 OpenAI Chat / OpenAI Responses / Anthropic Messages 三种风格的高频字段；
// 未知字段不显示含义（返回空字符串）。
export const FIELD_MEANINGS: Record<string, string> = {
  // 通用标识
  id: '唯一标识',
  object: '对象类型',
  created: '创建时间（Unix 时间戳）',
  created_at: '创建时间',
  model: '模型名称',

  // 消息结构
  messages: '对话消息列表',
  message: '单条消息体',
  system: '系统提示词',
  input: '输入内容（Responses API）',
  role: '角色（system/user/assistant/tool）',
  content: '消息文本内容',

  // 工具调用（OpenAI Chat）
  tool_calls: '模型发起的工具/函数调用列表',
  tool_call_id: '工具调用标识（关联调用与结果）',
  function: '函数调用定义',
  name: '工具/函数名称',
  arguments: '调用参数（JSON 字符串）',

  // 工具调用（Anthropic / Responses）
  type: '类型标识',
  tool_use: '工具使用（Anthropic）',
  tool_result: '工具执行结果（Anthropic）',
  input_text: '输入文本（Responses）',
  output_text: '输出文本（Responses）',
  function_call: '函数调用（Responses）',
  function_call_output: '函数调用结果（Responses）',
  call_id: '调用 ID（Responses）',
  output: '输出内容（Responses）',
  input_image: '输入图片（Responses）',

  // 响应结构
  choices: '候选回复列表',
  delta: '流式增量内容',
  index: '候选序号',
  finish_reason: '结束原因（stop/tool_calls/length 等）',
  stop_reason: '结束原因（Anthropic）',
  logprobs: '对数概率',

  // Token 用量
  usage: 'Token 用量统计',
  prompt_tokens: '输入 Token 数',
  completion_tokens: '输出 Token 数',
  total_tokens: '总 Token 数',
  input_tokens: '输入 Token 数（Anthropic）',
  output_tokens: '输出 Token 数（Anthropic）',
  prompt_tokens_details: '输入 Token 明细',
  completion_tokens_details: '输出 Token 明细',

  // 采样参数
  stream: '是否流式输出',
  temperature: '采样温度',
  top_p: '核采样概率阈值',
  max_tokens: '最大生成 Token 数',
  n: '生成候选数量',
  stop: '停止词',

  // 推理内容
  reasoning_content: '思维链/推理内容（DeepSeek 等）',
  reasoning: '推理内容',
  thinking: '思考过程（Anthropic）',

  // 错误与状态
  error: '错误信息',
  status: '状态码',
  annotations: '注解信息',

  // 元数据
  session_id: '会话标识',
  request_id: '请求标识',
  metadata: '附加元数据'
}
