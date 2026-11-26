<script setup lang="ts">
import { ref } from 'vue'
import { splitProse, toolExpandText } from './session.logic'
import type { AgentStuck, AgentThreadItem } from './types'

defineProps<{
  items: AgentThreadItem[]
}>()
const emit = defineEmits<{
  decide: [id: string, approved: boolean]
  intervene: [action: 'continue' | 'skip' | 'override' | 'retry' | 'cancel']
}>()

const openTools = ref<Record<string, boolean>>({})
const stuckInput = defineModel<string>('stuckInput', { default: '' })

function toggle(id: string) {
  openTools.value = { ...openTools.value, [id]: !openTools.value[id] }
}

function can(stuck: AgentStuck, action: AgentStuck['allowedActions'][number]) {
  return stuck.allowedActions.includes(action)
}
</script>

<template>
  <div class="aa-thread">
    <div class="aa-col">
      <template v-for="item in items" :key="item.id">
        <div v-if="item.kind === 'user'" class="aa-msg user">
          <div class="aa-bubble">{{ item.text }}</div>
        </div>
        <div v-else-if="item.kind === 'assistant'" class="aa-msg assistant">
          <div class="aa-prose">
            <template v-for="(part, i) in splitProse(item.text)" :key="i">
              <span v-if="part.type === 'text'">{{ part.text }}</span>
              <pre v-else class="aa-fence" :data-lang="part.language"><code>{{ part.content }}</code></pre>
            </template>
          </div>
        </div>
        <div v-else-if="item.kind === 'tool'" class="aa-msg assistant">
          <button type="button" class="aa-tool" @click="toggle(item.id)">
            {{ item.tool.name }} · {{ item.tool.summary }}
          </button>
          <pre v-if="openTools[item.id] && toolExpandText(item.tool)" class="aa-tool-detail">{{ toolExpandText(item.tool) }}</pre>
        </div>
        <div v-else-if="item.kind === 'approval'" class="aa-msg assistant">
          <div class="aa-card">
            <h3>需要确认后再执行</h3>
            <p v-if="item.approval.risk">{{ item.approval.risk }}</p>
            <p>{{ item.approval.summary }}</p>
            <div v-if="!item.approval.resolved" class="aa-actions">
              <button type="button" class="aa-btn pri" @click="emit('decide', item.id, true)">批准后执行</button>
              <button type="button" class="aa-btn ghost" @click="emit('decide', item.id, false)">取消</button>
            </div>
            <p v-else>{{ item.approval.approved ? '已批准' : '已取消' }}</p>
          </div>
        </div>
        <div v-else-if="item.kind === 'stuck'" class="aa-msg assistant">
          <div class="aa-card">
            <h3>{{ item.stuck.outcome === 'cancelled' ? '已取消' : '已卡住' }}</h3>
            <p>{{ item.stuck.reason }}</p>
            <template v-if="!item.stuck.resolved">
              <textarea
                v-if="can(item.stuck, 'continue')"
                v-model="stuckInput"
                class="aa-prose"
                style="width:100%;min-height:64px;background:transparent;border:1px solid var(--border-subtle);border-radius:var(--el-border-radius-base);color:inherit;padding:8px;margin-bottom:8px"
                placeholder="补充信息后继续"
              />
              <div class="aa-actions">
                <button v-if="can(item.stuck, 'continue')" type="button" class="aa-btn pri" @click="emit('intervene', 'continue')">补充后继续</button>
                <button v-if="can(item.stuck, 'skip')" type="button" class="aa-btn" @click="emit('intervene', 'skip')">跳过</button>
                <button v-if="can(item.stuck, 'cancel')" type="button" class="aa-btn ghost" @click="emit('intervene', 'cancel')">取消</button>
              </div>
            </template>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>
