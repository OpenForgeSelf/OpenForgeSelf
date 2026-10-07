<script setup lang="ts">
/**
 * 预算面板：CRUD + 实时达成率。
 *
 * 超支以**页面内横幅**呈现（FR-3.6），**不发通知**——
 * 观测类信息不该打断用户，通知权留给业务事件。
 */
import { onMounted, ref } from 'vue'
import { costApi, type BudgetItem } from '../http'

interface Achievement {
  name: string
  scope: string
  target: string
  period: string
  limitAmount: number
  achievement: {
    usedAmount: number
    remaining: number
    usageRatio: number
    level: 'Ok' | 'Warning' | 'Exceeded'
    shouldAlert: boolean
  }
}

const rows = ref<BudgetItem[]>([])
const achievement = ref<Achievement[]>([])
const error = ref('')

const draft = ref({ name: '', scope: 'global', target: '', limitAmount: 100, period: 'month', alertThreshold: 0.8 })
const editing = ref<string | null>(null)

async function load() {
  error.value = ''
  try {
    rows.value = await costApi.listBudgets()
    achievement.value = (await costApi.budgetAchievement()) as Achievement[]
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

async function submit() {
  error.value = ''
  try {
    if (editing.value) await costApi.updateBudget({ ...draft.value, name: editing.value })
    else await costApi.createBudget({ ...draft.value })
    editing.value = null
    draft.value = { name: '', scope: 'global', target: '', limitAmount: 100, period: 'month', alertThreshold: 0.8 }
    await load()
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

async function remove(name: string) {
  if (!window.confirm(`删除预算「${name}」？`)) return
  error.value = ''
  try {
    await costApi.deleteBudget(name)
    await load()
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

function pct(v: number) {
  return `${(v * 100).toFixed(1)}%`
}

onMounted(load)
</script>

<template>
  <section class="panel">
    <p v-if="error" class="panel__error">{{ error }}</p>

    <!-- 超支/预警横幅：页面内呈现，不发通知 -->
    <aside v-for="a in achievement.filter(x => x.achievement.shouldAlert)" :key="a.name" class="banner"
      :class="a.achievement.level === 'Exceeded' ? 'banner--over' : 'banner--warn'">
      <strong>{{ a.name }}</strong>
      已用 {{ a.achievement.usedAmount }} / 限额 {{ a.limitAmount }}（{{ pct(a.achievement.usageRatio) }}）
      <span v-if="a.achievement.level === 'Exceeded'">—— 已超支</span>
    </aside>

    <form class="panel__form" @submit.prevent="submit">
      <input v-model="draft.name" placeholder="规则名" :disabled="!!editing" required />
      <select v-model="draft.scope">
        <option value="global">全局</option>
        <option value="model">按模型</option>
        <option value="provider">按供应商</option>
      </select>
      <input v-if="draft.scope !== 'global'" v-model="draft.target" placeholder="目标（模型/供应商名）" required />
      <input v-model.number="draft.limitAmount" type="number" min="0" step="0.01" placeholder="限额" required />
      <select v-model="draft.period">
        <option value="day">日</option>
        <option value="month">月</option>
        <option value="quarter">季</option>
        <option value="year">年</option>
        <option value="all">全部</option>
      </select>
      <input v-model.number="draft.alertThreshold" type="number" min="0.01" max="1" step="0.05" placeholder="预警比例" required />
      <button type="submit">{{ editing ? '更新' : '新增' }}</button>
      <button v-if="editing" type="button" @click="editing = null">取消</button>
    </form>

    <table>
      <thead>
        <tr><th>规则</th><th>作用域</th><th>目标</th><th>周期</th><th>限额</th><th>预警比</th><th>已用</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="b in rows" :key="b.name">
          <td>{{ b.name }}</td>
          <td>{{ b.scope }}</td>
          <td>{{ b.target || '—' }}</td>
          <td>{{ b.period }}</td>
          <td>{{ b.limitAmount }}</td>
          <td>{{ pct(b.alertThreshold) }}</td>
          <td>
            <template v-if="achievement.find(a => a.name === b.name)">
              {{ achievement.find(a => a.name === b.name)!.achievement.usedAmount }}
            </template>
            <template v-else>—</template>
          </td>
          <td>
            <button type="button" @click="editing = b.name; draft = {
              name: b.name, scope: b.scope, target: b.target,
              limitAmount: b.limitAmount, period: b.period, alertThreshold: b.alertThreshold }">编辑</button>
            <button type="button" @click="remove(b.name)">删除</button>
          </td>
        </tr>
        <tr v-if="!rows.length"><td colspan="8">尚无预算规则</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 10px; }
.panel__error { color: var(--el-color-danger, #f56c6c); }
.panel__form { display: flex; gap: 8px; flex-wrap: wrap; }
.banner { border-radius: 4px; padding: 8px 12px; font-size: 13px; border: 1px solid transparent; }
.banner--warn { background: var(--el-color-warning-light-9, #fdf6ec); color: var(--el-color-warning, #e6a23c); }
.banner--over { background: var(--el-color-danger-light-9, #fef0f0); color: var(--el-color-danger, #f56c6c); }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { text-align: left; padding: 4px 6px; border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5); }
</style>
