<script setup>
import { computed, nextTick, ref, watch } from 'vue'
import { renderMarkdown } from '../utils/markdown'
import BlockRenderer from './blocks/BlockRenderer.vue'
import BlockBoundary from './blocks/BlockBoundary.vue'

const props = defineProps({
  message: { type: Object, required: true },
  avatar: { type: String, default: '' },
  config: { type: Object, required: true },
})
const emit = defineEmits(['retry', 'resume', 'resolve'])

const isUser = computed(() => props.message.role === 'user')
const html = computed(() => renderMarkdown(props.message.content || ''))

/* ---------- 状态矩阵（041 状态矩阵专项） ----------
   以前这条组件只认 role（user/assistant），**不认任何状态** ——
   于是「生成失败」「用户中断」「低置信度」「权限不足」这四类 AI 场景最常见的态，
   要么根本不显示，要么伪装成一条正常回复（中断的半截回复就是这么进历史的）。

   契约（由宿主/useChat 写入 message，组件只负责展示，不自己推断）：
     status     : 'sending' | 'streaming' | 'done' | 'failed' | 'stopped'
     confidence : 0~1 的数字，<0.5 视为低置信度（阈值放这里，宿主不必重复算）
     denied     : 权限不足时的说明文案

   ★ 为什么阈值定 0.5 且写死在组件里：低置信度提示如果满天飞就等于没有提示，
     阈值必须单一来源，否则宿主 A 用 0.3、宿主 B 用 0.6，同一个模型两种观感。 */
const LOW_CONFIDENCE = 0.5
const status = computed(() => String(props.message.status || ''))
const isSending = computed(() => status.value === 'sending')
/** 流式：兼容两种写法 —— 新契约的 status:'streaming'，以及宿主直接给 streaming:true */
const isStreaming = computed(() => status.value === 'streaming' || props.message.streaming === true)
const isFailed = computed(() => status.value === 'failed')
const isStopped = computed(() => status.value === 'stopped')
const isLowConf = computed(
  () => typeof props.message.confidence === 'number' && props.message.confidence < LOW_CONFIDENCE
)
const deniedText = computed(() => String(props.message.denied || ''))

/** 状态文案。失败要带上后端给的原因（「502」比「失败了」有用得多）。 */
const statusText = computed(() => {
  if (isFailed.value) {
    const reason = String(props.message.error || '')
    return reason ? `生成失败 · ${reason}` : '生成失败'
  }
  if (isStopped.value) return '已停止（你中断了这次生成）'
  if (isSending.value) return '已送出'
  return ''
})
const hasStatus = computed(() => isSending.value || isFailed.value || isStopped.value)
/** 有 blocks 就渲染富内容块，否则退回纯 Markdown 文本 */
const hasBlocks = computed(() => Array.isArray(props.message.blocks) && props.message.blocks.length > 0)
/** 无障碍（v1.11.0）：读屏在 role="log" 里逐条播报时，得先知道「这是谁说的」 */
const whoLabel = computed(() => (isUser.value ? '你发送的消息' : '助手回复'))
const timeText = computed(() => {
  const d = new Date(props.message.time || Date.now())
  const pad = (n) => String(n).padStart(2, '0')
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`
})

/* ---------- 长回复折叠（展示精简） ----------
   组件源文档里「消息级折叠」属下期候选（未实现），AIAgent 页未接入 blocks，
   所以这里的做法是行业通用形态：助手正文超过阈值高度时截成一屏并给「展开全文」。
   三条约束：① 只折助手、不折用户（用户发的再短也要看得全）；
            ② 流式输出中不折（正在生成的文字被截断比啰嗦更糟）；
            ③ 折叠只是视觉裁剪，DOM 全文仍在 —— 复制、Ctrl+F、读屏都不受影响。 */
const COLLAPSE_MAX_HEIGHT = 240
const bubbleEl = ref(null)
const overflowing = ref(false)
const expanded = ref(false)
const streaming = computed(() => props.message.streaming === true)
const collapsed = computed(() => overflowing.value && !expanded.value && !streaming.value)

/** v-html 渲染完才能真正量到高度，所以一律等 nextTick 再测。 */
function measure() {
  nextTick(() => {
    const el = bubbleEl.value
    /* 用户消息永不折叠：那是他自己敲的字，折起来只会让人以为发出去了别的东西。 */
    if (!el || isUser.value) {
      overflowing.value = false
      return
    }
    overflowing.value = el.scrollHeight > COLLAPSE_MAX_HEIGHT + 8
  })
}
watch(
  () => [props.message.content, hasBlocks.value, streaming.value],
  () => measure(),
  { immediate: true }
)
</script>

<template>
  <div
    class="aiw-msg"
    :class="[
      isUser ? 'aiw-msg--user' : 'aiw-msg--bot',
      {
        'is-noavatar': !isUser && config.showBotAvatar === false,
        'is-sending': isSending,
        'is-streaming': isStreaming,
        'is-failed': isFailed,
        'is-stopped': isStopped,
        'is-lowconf': isLowConf,
        'is-denied': !!deniedText,
      },
    ]"
    :data-mid="message.id"
    role="group"
    :aria-label="whoLabel"
  >
    <div v-if="!isUser && config.showBotAvatar !== false" class="aiw-msg__avatar">
      <img v-if="avatar" :src="avatar" alt="" />
      <svg v-else viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 2.5a1 1 0 0 1 1 1V5a5.5 5.5 0 0 1 4.5 5.4V11h.5a2.5 2.5 0 0 1 2.5 2.5v4A2.5 2.5 0 0 1 18 20H6a2.5 2.5 0 0 1-2.5-2.5v-4A2.5 2.5 0 0 1 6 11h.5v-.6A5.5 5.5 0 0 1 11 4.99V3.5a1 1 0 0 1 1-1ZM9 13.2a1.3 1.3 0 1 0 0 2.6 1.3 1.3 0 0 0 0-2.6Zm6 0a1.3 1.3 0 1 0 0 2.6 1.3 1.3 0 0 0 0-2.6Z" fill="currentColor" />
      </svg>
    </div>
    <div class="aiw-msg__col" :class="{ 'is-rich': hasBlocks }">
      <!-- 遥控来源必须在气泡上看得见：屏前的人要知道这句话不是本机打的 -->
      <span v-if="isUser && message.source === 'remote'" class="aiw-msg__src">遥控</span>
      <div v-if="hasBlocks" class="aiw-blocks">
        <BlockBoundary v-for="(b, i) in message.blocks" :key="i" :label="b.type === 'chart' ? '图表' : '内容块'">
          <BlockRenderer :block="b" :config="config" @retry="emit('retry')" @resolve="emit('resolve', $event)" />
        </BlockBoundary>
      </div>
      <!-- ★ 有块也要渲染正文：工具卡与答案文本是同一轮的回答的两部分。
           原来这里是 v-else —— 只要出现一张工具卡，整段正文就被藏掉，
           用户只看到「调用了 3 个工具」却看不到结论。 -->
      <div
        v-if="!hasBlocks || String(message.content || '').trim()"
        class="aiw-msg__fold"
        :class="{ 'is-collapsed': collapsed }"
      >
        <div ref="bubbleEl" class="aiw-msg__bubble" v-html="html"></div>
        <button
          v-if="overflowing && !streaming"
          type="button"
          class="aiw-msg__toggle"
          :aria-expanded="expanded ? 'true' : 'false'"
          @click="expanded = !expanded"
        >
          {{ expanded ? '收起' : '展开全文' }}
        </button>
      </div>
      <!-- 失败 / 中断 / 发送中：这是**这一条**消息自己的状态。
           不能只靠输入框上方那条全局 error 条 —— 全局条会被下一条顶掉，
           而这条消息还挂在上面，看上去跟正常的一模一样。 -->
      <div
        v-if="hasStatus"
        class="aiw-msg__status"
        :class="{ 'is-failed': isFailed, 'is-stopped': isStopped, 'is-sending': isSending }"
        :role="isFailed ? 'alert' : 'status'"
      >
        <span>{{ statusText }}</span>
        <!-- 能续跑就优先给「继续执行」：重试会把半截成果全丢，续跑不会 -->
        <button
          v-if="isFailed && message.resumable"
          type="button"
          class="aiw-msg__retry"
          @click="emit('resume', message.id)"
        >
          继续执行
        </button>
        <button
          v-if="isFailed"
          type="button"
          class="aiw-msg__retry"
          @click="emit('retry', message.id)"
        >
          重试
        </button>
      </div>
      <!-- 低置信度 / 权限不足共用一条提示位，但语义不同：
           前者是「这话别全信」（内容质量），后者是「这事没让做」（能力被拦）。 -->
      <div v-if="isLowConf" class="aiw-msg__warn is-low">这条把握不大，建议核实</div>
      <div v-if="deniedText" class="aiw-msg__warn is-denied">{{ deniedText }}</div>
      <div class="aiw-msg__time">{{ timeText }}</div>
    </div>
  </div>
</template>
