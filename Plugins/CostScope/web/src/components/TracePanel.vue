<script setup lang="ts">
/**
 * Trace 面板：waterfall 时间线。
 *
 * 诚实性要求（BC-4 / A10 口径）：宿主轮次**没有** `AgentRunId` 外键，
 * 关联是「同一会话键 + 时间窗邻近」的**推断**。因此本面板**顶部固定显示**
 * `correlationNote`，绝不把推断呈现成事实。
 */
import { computed, onMounted, ref } from 'vue'
import { costApi, type TraceWaterfall } from '../http'

const wf = ref<TraceWaterfall | null>(null)
const error = ref('')
const agentRunId = ref('')

async function load() {
  error.value = ''
  try {
    wf.value = await costApi.trace(agentRunId.value ? { agentRunId: agentRunId.value } : {})
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

/** 以最早节点为原点，把每个节点换算成偏移毫秒，画成甘特式条。 */
const timeline = computed(() => {
  const nodes = wf.value?.nodes ?? []
  if (!nodes.length) return []
  const t0 = Math.min(...nodes.map(n => new Date(n.start).getTime()))
  const span = Math.max(1, Math.max(...nodes.map(n => new Date(n.start).getTime() + n.durationMs)) - t0)
  return nodes.map(n => {
    const offset = new Date(n.start).getTime() - t0
    return {
      ...n,
      offsetMs: offset,
      widthPct: Math.max(2, (n.durationMs / span) * 100),
      leftPct: (offset / span) * 100,
    }
  })
})

onMounted(load)
</script>

<template>
  <section class="panel">
    <p v-if="error" class="panel__error">{{ error }}</p>

    <div class="panel__bar">
      <input v-model="agentRunId" placeholder="AgentRun ID（留空则取关联最多的运行）" />
      <button type="button" @click="load">查询</button>
    </div>

    <aside v-if="wf" class="notice">
      <div>
        <strong>关联状态：</strong>
        {{ wf.agentRunId ? `运行 ${wf.agentRunId}` : '未关联到任何 AgentRun' }}
        · 关联轮次 {{ wf.linkedTurns }} · 未关联 {{ wf.unlinkedTurns }}
      </div>
      <div class="notice__note">{{ wf.correlationNote }}</div>
    </aside>

    <div v-if="timeline.length" class="tl">
      <div v-for="n in timeline" :key="`${n.kind}-${n.start}-${n.label}`" class="tl__row">
        <span class="tl__label" :title="n.label">{{ n.kind === 'Llm' ? 'LLM' : '工具' }} · {{ n.label }}</span>
        <div class="tl__track">
          <div
            class="tl__bar"
            :class="n.kind === 'Llm' ? 'tl__bar--llm' : 'tl__bar--tool'"
            :style="{ left: `${n.leftPct}%`, width: `${n.widthPct}%` }"
            :title="`${n.durationMs} ms`"
          />
        </div>
        <span class="tl__meta">
          {{ n.durationMs }} ms
          <template v-if="n.tokens !== null">· {{ n.tokens }} tok</template>
        </span>
      </div>
    </div>
    <p v-else-if="wf" class="panel__empty">该窗口内没有可展示的 trace 节点</p>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 10px; }
.panel__error { color: var(--el-color-danger, #f56c6c); }
.panel__bar { display: flex; gap: 8px; }
.notice { border: 1px solid var(--el-border-color, #e5e7eb); border-left: 3px solid var(--el-color-warning, #e6a23c); border-radius: 4px; padding: 8px 12px; font-size: 13px; }
.notice__note { color: var(--el-color-warning, #e6a23c); }
.tl { display: flex; flex-direction: column; gap: 6px; }
.tl__row { display: grid; grid-template-columns: 220px 1fr 140px; align-items: center; gap: 8px; font-size: 13px; }
.tl__label { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tl__track { position: relative; height: 14px; background: var(--el-fill-color-lighter, #fafafa); border-radius: 3px; }
.tl__bar { position: absolute; top: 0; height: 100%; border-radius: 3px; }
.tl__bar--llm { background: var(--el-color-primary, #409eff); }
.tl__bar--tool { background: var(--el-color-success, #67c23a); }
.tl__meta { color: var(--el-color-info, #909399); text-align: right; }
</style>
