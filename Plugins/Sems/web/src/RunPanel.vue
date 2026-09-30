<template>
  <section class="run">
    <header class="run__head">
      <h2 class="run__title">
        运行面板
        <span class="run__count">{{ runs.length }}</span>
      </h2>
      <div class="run__ops">
        <button class="run__btn" :disabled="checking" @click="check">
          {{ checking ? '检查中…' : '检查' }}
        </button>
        <button class="run__btn run__btn--primary" :disabled="!hasLaunchable" @click="runAll">
          启动全部
        </button>
      </div>
    </header>

    <div v-if="checking && runs.length === 0" class="run__hint">检查中…</div>
    <!-- 空态分级（§3.4-4）：无可启动命令时引导去加命令，而不是笼统「暂无数据」 -->
    <div v-else-if="runs.length === 0 && !hasLaunchable" class="run__hint">
      还没有可运行的命令。请在下方项目卡片展开「管理命令」先添加一条，再回到这里启动或「检查」。
    </div>
    <div v-else-if="runs.length === 0" class="run__hint">
      暂无运行中的进程。可点「启动全部」调起已登记命令，或点「检查」捕获本机已在运行的项目进程。
    </div>

    <ul v-else class="run__list">
      <li v-for="r in runs" :key="runKey(r)" class="run__item">
        <div class="run__info">
          <div class="run__line1">
            <span class="run__proj">{{ r.projectName }}</span>
            <span class="run__cmd">/ {{ r.commandName }}</span>
            <span class="run__origin" :class="originClass(r.origin)">{{ originLabel(r.origin) }}</span>
          </div>
          <div class="run__line2">
            <span class="run__pid">PID {{ r.pid }}</span>
            <span class="run__dur">已运行 {{ duration(r) }}</span>
          </div>
        </div>

        <div class="run__acts">
          <a v-if="hasUrl(r)" class="run__icon" :href="commandUrl(r)" target="_blank" rel="noopener" title="打开访问地址">↗</a>
          <button
            v-if="r.origin === 'Launched'"
            class="run__btn run__btn--stop"
            :disabled="busyPid === r.pid"
            @click="stopLaunched(r)"
          >
            停止
          </button>
          <button
            v-else
            class="run__btn run__btn--stop"
            :disabled="busyPid === r.pid"
            @click="stopExternal(r)"
          >
            停止
          </button>
        </div>
      </li>
    </ul>

    <p class="run__note">
      外部捕获的进程停止将杀除其整个进程树（可能含他人进程），停止前会二次确认。
    </p>
  </section>
</template>

<script setup lang="ts">
/**
 * 运行面板：
 * - 运行列表（GET /api/runs）：项目名 / 命令名 / PID / 已运行时长 / 来源徽标
 * - 启动：POST /api/commands/{id}/run（启动全部由父级遍历命令调起，本面板只上报事件）
 * - 停止：Launched → POST /api/commands/{id}/stop；Detected → POST /api/runs/{pid}/stop
 *   两者一律走 confirmOps 的「先确认后请求」编排（取消时不发任何请求），文案见 confirmOps.ts
 * - 快捷访问图标：r 关联命令有 url 时显示（由父级传入命令 url 对照）
 * - 检查：POST /api/runs/check（loading 态）
 * 不常驻轮询，依赖手动「检查」+ 启动后即时刷新。
 */
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiGet, apiPost } from './http'
import { errorMessage, runWithConfirm, stopExternalMessage, stopSessionMessage } from './confirmOps'
import type { RunSession, RunsResp } from './types'

const props = defineProps<{
  /** 命令 id → 访问 url 映射（由上层从项目列表装配，供快捷访问图标使用）。 */
  commandUrls?: Record<number, string>
  /** 可启动命令总数（由上层装配；0 时禁用「启动全部」并切换空态文案）。 */
  launchableCount?: number
}>()

const runs = ref<RunSession[]>([])
const checking = ref(false)
const busyPid = ref<number | null>(null)

onMounted(refresh)

/** 刷新运行列表，返回当前条数并上报（面板内停止/启动后统计卡必须同源，否则停在旧值）。 */
async function refresh(): Promise<number> {
  try {
    const r = await apiGet<RunsResp>('/api/runs')
    runs.value = r?.runs ?? []
  } catch {
    // 静默：面板失败不影响项目列表（项目区已有错误态与重试）
  }
  emit('count', runs.value.length)
  return runs.value.length
}

async function check() {
  checking.value = true
  try {
    const r = await apiPost<RunsResp>('/api/runs/check', {})
    runs.value = r?.runs ?? []
    emit('count', runs.value.length)
  } catch (e) {
    ElMessage.error(`检查失败：${errorMessage(e)}`)
  } finally {
    checking.value = false
  }
}

/** 列表里 Launched 条目有 commandId；用它找 url。Detected 无命令则无 url。 */
function hasUrl(r: RunSession): boolean {
  if (r.origin !== 'Launched') return false
  const u = r.commandId ? props.commandUrls?.[r.commandId] : undefined
  return !!u
}

function commandUrl(r: RunSession): string {
  return (r.commandId ? props.commandUrls?.[r.commandId] : undefined) ?? '#'
}

const hasLaunchable = computed(() => (props.launchableCount ?? 0) > 0)

/** 父级装配待启动命令列表；这里只上报事件。count = 运行中条数，供统计卡与面板同源。 */
const emit = defineEmits<{
  (e: 'run-all'): void
  (e: 'count', running: number): void
}>()

async function runAll() {
  emit('run-all')
}

/** ElMessageBox 版确认（取消时 reject → 返回 false，让编排零请求）。 */
async function ask(message: string, title: string): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, title, {
      type: 'warning',
      confirmButtonText: '确认停止',
      cancelButtonText: '取消',
    })
    return true
  } catch {
    return false
  }
}

async function stopBy(r: RunSession, path: string, message: string, okText: string) {
  busyPid.value = r.pid
  const result = await runWithConfirm({ title: '停止进程', message, confirm: ask, action: () => apiPost(path, {}) })
  if (result.outcome === 'done') {
    ElMessage.success(okText)
    await refresh()
  } else if (result.outcome === 'failed') {
    ElMessage.error(`停止失败：${result.error}`)
  }
  busyPid.value = null
}

async function stopLaunched(r: RunSession) {
  await stopBy(
    r,
    `/api/commands/${r.commandId}/stop`,
    stopSessionMessage(r.projectName, r.commandName, r.pid),
    `已停止「${r.projectName} / ${r.commandName}」`,
  )
}

async function stopExternal(r: RunSession) {
  await stopBy(
    r,
    `/api/runs/${r.pid}/stop`,
    stopExternalMessage(r.projectName, r.commandName, r.pid),
    `已停止外部进程 PID ${r.pid}`,
  )
}

/** Launched+CommandId 唯一；Detected 用 pid（同项目多命令可能共享 pid 但 commandName 不同）。 */
function runKey(r: RunSession): string {
  return r.origin === 'Launched' && r.commandId ? `L-${r.commandId}` : `D-${r.pid}-${r.commandName}`
}

function originLabel(o: string): string {
  return o === 'Launched' ? '本面板启动' : o === 'Detected' ? '外部捕获' : o
}

function originClass(o: string): string {
  return o === 'Launched' ? 'run__origin--launched' : 'run__origin--detected'
}

function duration(r: RunSession): string {
  const start = new Date(r.startedAt)
  if (Number.isNaN(start.getTime()) || start.getTime() <= 0) return '未知'
  const ms = Date.now() - start.getTime()
  if (ms < 0) return '0s'
  const s = Math.floor(ms / 1000)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}h${m}m`
  if (m > 0) return `${m}m${sec}s`
  return `${sec}s`
}

// 暴露给父级：启动/停止/刷新后要重新拉运行数
defineExpose({ refresh, check })
</script>

<style scoped>
.run {
  margin: 24px 0;
  padding: 16px 18px;
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 10px;
}

.run__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.run__title {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
  color: var(--el-text-color-primary, #e5eaf3);
  display: flex;
  align-items: center;
  gap: 8px;
}

.run__count {
  padding: 0 8px;
  border-radius: 10px;
  font-size: 12px;
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.run__ops {
  display: flex;
  gap: 8px;
}

.run__btn {
  padding: 5px 12px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  cursor: pointer;
  font-size: 13px;
}

.run__btn:hover:not(:disabled) {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.run__btn--primary {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.run__btn--primary:hover:not(:disabled) {
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.run__btn--stop {
  color: var(--el-color-danger, #f56c6c);
  border-color: var(--el-color-danger, #f56c6c);
}

.run__btn--stop:hover:not(:disabled) {
  background: var(--el-color-danger-light, rgba(245, 108, 108, 0.12));
}

.run__btn:disabled {
  opacity: 0.5;
  cursor: default;
}

.run__hint {
  padding: 20px 8px;
  font-size: 13px;
  color: var(--el-text-color-secondary, #a3a6ad);
  text-align: center;
}

.run__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.run__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 8px;
}

.run__info {
  min-width: 0;
}

.run__line1 {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.run__proj {
  font-weight: 600;
  color: var(--el-text-color-primary, #e5eaf3);
}

.run__cmd {
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.run__origin {
  padding: 0 6px;
  border-radius: 3px;
  font-size: 11px;
  line-height: 16px;
}

.run__origin--launched {
  color: var(--el-color-success, #67c23a);
  background: var(--el-color-success-light, rgba(103, 194, 58, 0.12));
}

.run__origin--detected {
  color: var(--el-color-warning, #e6a23c);
  background: var(--el-color-warning-light, rgba(230, 162, 60, 0.14));
}

.run__line2 {
  display: flex;
  gap: 12px;
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.run__acts {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.run__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  color: var(--el-color-primary, #ffb84d);
  text-decoration: none;
  font-size: 14px;
}

.run__icon:hover {
  border-color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.run__note {
  margin: 12px 0 0;
  font-size: 11px;
  color: var(--el-text-color-secondary, #a3a6ad);
}
</style>
