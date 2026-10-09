<script setup lang="ts">
/**
 * 执行记录时间线：一条任务「谁、做了什么、结果如何、改了哪些文件、风险、遗留」的台账。
 *
 * - 记录是 append-only：这里只有"追加 + 查看"，没有编辑与删除入口（历史不许被改写）；
 * - 表单字段与后端 `CreateTaskExecutionRequest` 一一对应，agent 用 REST/工具函数写进来的记录同形状；
 * - 「做了什么」是必填，其余可空：空栏就显示空，不做"看起来完整"的包装。
 */
import { reactive, ref } from 'vue'
import * as store from '../store'
import { formatElapsed, relativeTime, shortPath } from '../actions'
import { showToast } from '../notify'
import type { ExecutionDraft, TodoItem } from '../types'

const props = defineProps<{ task: TodoItem }>()
const s = store.state

const open = ref(false)
const draft = reactive<ExecutionDraft>({
  action: '', result: '', detail: '', filesChangedText: '', verification: '',
  risks: '', residuals: '', evidence: '', nextStep: '', stageTo: ''
})

const stageTargets = ref<string[]>(['Running', 'Review', 'Done', 'Blocked', 'Cancelled'])

function reset(): void {
  draft.action = ''
  draft.result = ''
  draft.detail = ''
  draft.filesChangedText = ''
  draft.verification = ''
  draft.risks = ''
  draft.residuals = ''
  draft.evidence = ''
  draft.nextStep = ''
  draft.stageTo = ''
}

async function submit(): Promise<void> {
  if (draft.stageTo === 'Blocked' && !(draft.blockReason ?? '').trim()) {
    showToast('流转选「Blocked」时必须填阻塞原因', 'warning')
    return
  }
  const ok = await store.appendRecord(props.task, { ...draft, actor: draft.actor?.trim() || 'manual' })
  if (ok) {
    reset()
    open.value = false
  }
}
</script>

<template>
  <div class="tl" data-test="execution-timeline">
    <div class="tl-head">
      <span class="tl-title">执行记录</span>
      <span class="tl-dim">{{ s.recordsTotal }} 条</span>
      <button type="button" class="tl-link" data-test="toggle-record-form" @click="open = !open">
        {{ open ? '收起' : '补记一条' }}
      </button>
      <button type="button" class="tl-link" @click="store.loadRecords()">刷新</button>
    </div>

    <form v-if="open" class="tl-form" @submit.prevent="submit">
      <label class="tl-label">做了什么操作 *
        <input v-model="draft.action" class="tl-input" maxlength="300" data-test="record-action"
          placeholder="一句话，如：改 TodoService 并跑定向测试">
      </label>
      <div class="tl-grid">
        <label class="tl-label">执行者
          <input v-model="draft.actor" class="tl-input" maxlength="100" placeholder="manual / agent 名">
        </label>
        <label class="tl-label">同时流转阶段
          <select v-model="draft.stageTo" class="tl-input">
            <option value="">不改状态</option>
            <option v-for="t in stageTargets" :key="t" :value="t">{{ t }}</option>
          </select>
        </label>
        <label class="tl-label">耗时（毫秒）
          <input v-model.number="draft.elapsedMs" class="tl-input" type="number" min="0">
        </label>
      </div>
      <label class="tl-label">结果
        <textarea v-model="draft.result" class="tl-input tl-area" rows="2" placeholder="什么结果（含输出要点）"></textarea>
      </label>
      <label class="tl-label">操作明细
        <textarea v-model="draft.detail" class="tl-input tl-area" rows="2" placeholder="步骤 / 命令 / 说明"></textarea>
      </label>
      <label class="tl-label">改了哪些文件
        <textarea v-model="draft.filesChangedText" class="tl-input tl-area" rows="2"
          data-test="record-files" placeholder="一行一个路径，可带 A/M/D 前缀"></textarea>
      </label>
      <label class="tl-label">验证
        <textarea v-model="draft.verification" class="tl-input tl-area" rows="2"
          placeholder="跑了什么命令 + 真实结果（PASS/FAIL）"></textarea>
      </label>
      <div class="tl-grid">
        <label class="tl-label">风险
          <input v-model="draft.risks" class="tl-input" placeholder="遗留的隐患、需谁复核">
        </label>
        <label class="tl-label">遗留 / 未做
          <input v-model="draft.residuals" class="tl-input" placeholder="还没做的部分">
        </label>
      </div>
      <div class="tl-grid">
        <label class="tl-label">证据
          <input v-model="draft.evidence" class="tl-input" placeholder="日志 / 截图 / 工件路径 / Release 链接">
        </label>
        <label class="tl-label">下一步
          <input v-model="draft.nextStep" class="tl-input" placeholder="回流入口（对齐工作日记「下一步」）">
        </label>
      </div>
      <label v-if="draft.stageTo === 'Blocked'" class="tl-label">阻塞原因 *
        <input v-model="draft.blockReason" class="tl-input" data-test="record-block-reason"
          placeholder="阻塞在哪、缺什么、等谁">
      </label>
      <div class="tl-row">
        <button type="submit" class="tl-btn is-primary" data-test="record-submit" :disabled="!draft.action.trim() || s.operating">
          追加记录
        </button>
        <button type="button" class="tl-btn" @click="reset()">清空</button>
        <span v-if="!draft.action.trim()" class="tl-dim">「做了什么操作」必填</span>
      </div>
    </form>

    <p v-if="!s.records.length" class="tl-empty">
      还没有执行记录 —— agent 或人在做完一步后写一条，任务历史才连得起来（记录只追加，不可改写）。
    </p>

    <ol v-else class="tl-list">
      <li v-for="r in s.records" :key="r.id" class="tl-item">
        <div class="tl-item-head">
          <span class="tl-seq">#{{ r.seq }}</span>
          <b class="tl-action" :title="r.action">{{ r.action }}</b>
          <span v-if="r.actor" class="tl-actor">{{ r.actor }}</span>
          <span v-if="r.stageTo" class="tl-stage">{{ r.stageFrom || '—' }} → {{ r.stageTo }}</span>
          <span class="tl-dim tl-time">{{ relativeTime(r.createdAt) }}</span>
        </div>
        <p v-if="r.result" class="tl-line"><span class="tl-k">结果</span>{{ r.result }}</p>
        <p v-if="r.detail" class="tl-line"><span class="tl-k">明细</span>{{ r.detail }}</p>
        <div v-if="r.filesChanged.length" class="tl-line">
          <span class="tl-k">改动</span>
          <span class="tl-files">
            <code v-for="(f, i) in r.filesChanged" :key="`${r.id}-${i}`" :title="f.path">
              {{ f.change ? `${f.change} ` : '' }}{{ shortPath(f.path, 40) }}
            </code>
          </span>
        </div>
        <p v-else-if="r.filesChangedRaw" class="tl-line"><span class="tl-k">改动</span>{{ r.filesChangedRaw }}</p>
        <p v-if="r.verification" class="tl-line"><span class="tl-k">验证</span>{{ r.verification }}</p>
        <p v-if="r.risks" class="tl-line"><span class="tl-k">风险</span>{{ r.risks }}</p>
        <p v-if="r.residuals" class="tl-line"><span class="tl-k">遗留</span>{{ r.residuals }}</p>
        <p v-if="r.evidence" class="tl-line"><span class="tl-k">证据</span>{{ r.evidence }}</p>
        <p v-if="r.blockReason" class="tl-line"><span class="tl-k">阻塞</span>{{ r.blockReason }}</p>
        <p v-if="r.nextStep" class="tl-line"><span class="tl-k">下一步</span>{{ r.nextStep }}</p>
        <p v-if="r.elapsedMs" class="tl-line"><span class="tl-k">耗时</span>{{ formatElapsed(r.elapsedMs) }}</p>
      </li>
    </ol>
  </div>
</template>

<style scoped>
.tl { display: flex; flex-direction: column; gap: 8px; }
.tl-head { display: flex; align-items: center; gap: 10px; }
.tl-title { font-size: 13px; font-weight: 600; }
.tl-dim { color: var(--el-text-color-secondary); font-size: 12px; }
.tl-time { margin-left: auto; }
.tl-link { border: 0; background: transparent; color: var(--el-color-primary); cursor: pointer; font-size: 12px; padding: 0; }

.tl-form { display: flex; flex-direction: column; gap: 6px; padding: 10px; border: 1px dashed var(--el-border-color); border-radius: var(--el-border-radius-base, 4px); }
.tl-grid { display: flex; gap: 8px; flex-wrap: wrap; }
.tl-grid > * { flex: 1; min-width: 140px; }
.tl-label { display: flex; flex-direction: column; gap: 3px; font-size: 12px; color: var(--el-text-color-secondary); }
.tl-input {
  width: 100%; box-sizing: border-box; border: 1px solid var(--el-border-color);
  background: var(--el-bg-color); color: var(--el-text-color-primary);
  border-radius: var(--el-border-radius-base, 4px); padding: 4px 8px; font-size: 13px; line-height: 1.6;
}
.tl-area { resize: vertical; min-height: 40px; }
.tl-row { display: flex; align-items: center; gap: 8px; }
.tl-btn { border: 1px solid var(--el-border-color); background: var(--el-bg-color); color: var(--el-text-color-regular); border-radius: var(--el-border-radius-base, 4px); padding: 5px 12px; font-size: 13px; cursor: pointer; }
.tl-btn:disabled { opacity: .55; cursor: not-allowed; }
.tl-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: #fff; }

.tl-empty { margin: 0; font-size: 12px; color: var(--el-text-color-secondary); line-height: 1.7; }
.tl-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.tl-item { border: 1px solid var(--el-border-color-lighter); border-radius: var(--el-border-radius-base, 4px); padding: 8px 10px; display: flex; flex-direction: column; gap: 3px; }
.tl-item-head { display: flex; align-items: center; gap: 8px; min-width: 0; }
.tl-seq { font-size: 11px; color: var(--el-text-color-secondary); font-family: ui-monospace, Menlo, Consolas, monospace; }
.tl-action { font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.tl-actor, .tl-stage { font-size: 11px; padding: 0 6px; border-radius: 3px; background: var(--el-fill-color-light); color: var(--el-text-color-secondary); flex-shrink: 0; }
.tl-stage { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.tl-line { margin: 0; font-size: 12px; line-height: 1.7; color: var(--el-text-color-regular); word-break: break-word; }
.tl-k { color: var(--el-text-color-secondary); margin-right: 6px; }
.tl-files { display: inline-flex; flex-direction: column; gap: 1px; vertical-align: top; }
.tl-files code { font-size: 11.5px; font-family: ui-monospace, Menlo, Consolas, monospace; word-break: break-all; }
</style>
