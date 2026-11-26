<script setup lang="ts">
/**
 * 定时任务只读页。打开 /scheduler?taskId= 时显示这一条任务和最近一次日志。
 * 不提供编辑。
 */
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import PageHead from '@/components/PageHead.vue'
import { authFetch } from '@/services/authFetch'
import { schedulerTaskFromQuery } from '@/services/runtimeDeepLink'

interface TaskView {
  name: string
  status: string
  nextRunTime: string
}

interface LogView {
  startTime: string
  endTime: string
  status: string
  message: string
}

const route = useRoute()
const loading = ref(true)
const errorText = ref('')
const task = ref<TaskView | null>(null)
const log = ref<LogView | null>(null)
const running = ref<Array<{ taskId: string; name: string; startTime: string }>>([])

const STATUS: Record<string, string> = {
  '0': '启用',
  '1': '停用',
  '2': '暂停',
  '3': '出错',
  Enabled: '启用',
  Disabled: '停用',
  Paused: '暂停',
  Error: '出错',
  Running: '执行中',
  Success: '成功',
  Failed: '失败',
}

function label(value: unknown): string {
  const key = value == null ? '' : String(value)
  return STATUS[key] || key || '—'
}

function text(row: Record<string, unknown> | null, key: string): string {
  const value = row?.[key]
  return value == null || value === '' ? '—' : String(value)
}

onMounted(async () => {
  const taskId = schedulerTaskFromQuery(route.query as Record<string, unknown>)
  try {
    if (!taskId) {
      const res = await authFetch('/api/scheduler/running')
      if (!res.ok) {
        errorText.value = res.status === 404 ? '定时任务未启用' : '需要访问密钥或读取失败'
        return
      }
      const json = (await res.json()) as { data?: Array<Record<string, unknown>> }
      running.value = (json.data ?? []).map((row) => ({
        taskId: text(row, 'taskId'),
        name: text(row, 'name'),
        startTime: text(row, 'startTime'),
      }))
      return
    }
    const [taskRes, logRes] = await Promise.all([
      authFetch(`/api/scheduler/tasks/${encodeURIComponent(taskId)}`),
      authFetch(`/api/scheduler/tasks/${encodeURIComponent(taskId)}/logs?page=1&pageSize=1`),
    ])
    if (taskRes.status === 404) {
      errorText.value = '找不到这个定时任务'
      return
    }
    if (!taskRes.ok) {
      errorText.value = '读取定时任务失败'
      return
    }
    const taskJson = (await taskRes.json()) as { data?: Record<string, unknown> }
    const row = taskJson.data ?? null
    task.value = {
      name: text(row, 'name'),
      status: label(row?.status),
      nextRunTime: text(row, 'nextRunTime'),
    }
    if (logRes.ok) {
      const logJson = (await logRes.json()) as { data?: { items?: Array<Record<string, unknown>> } }
      const first = logJson.data?.items?.[0]
      if (first) {
        log.value = {
          startTime: text(first, 'startTime'),
          endTime: text(first, 'endTime'),
          status: label(first.status),
          message: text(first, 'errorMessage') !== '—' ? text(first, 'errorMessage') : text(first, 'resultMessage'),
        }
      }
    }
  } catch (e) {
    errorText.value = e instanceof Error ? e.message : '读取失败'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="sched">
    <PageHead eyebrow="Scheduler · 定时任务" title="这一次触发" />
    <p v-if="loading" class="sched-muted">正在读取</p>
    <p v-else-if="errorText" class="sched-muted">{{ errorText }}</p>
    <template v-else-if="task">
      <p class="sched-name">{{ task.name }}</p>
      <p class="sched-muted">状态 {{ task.status }} · 下次 {{ task.nextRunTime }}</p>
      <p v-if="log" class="sched-muted">
        最近一次 {{ log.status }} · {{ log.startTime }} → {{ log.endTime }}
        <span v-if="log.message !== '—'"> · {{ log.message }}</span>
      </p>
      <p v-else class="sched-muted">还没有执行日志</p>
    </template>
    <template v-else>
      <p v-if="!running.length" class="sched-muted">现在没有正在执行的定时任务</p>
      <ul v-else class="sched-list">
        <li v-for="row in running" :key="row.taskId">{{ row.name }} · {{ row.startTime }}</li>
      </ul>
    </template>
  </div>
</template>

<style scoped>
.sched-name {
  margin: 0 0 8px;
  color: var(--fg-primary);
  font-weight: 600;
}

.sched-muted {
  margin: 0 0 8px;
  color: var(--fg-tertiary);
  font-size: 13px;
}

.sched-list {
  margin: 0;
  padding-left: 18px;
  color: var(--fg-primary);
  font-size: 13px;
}
</style>
