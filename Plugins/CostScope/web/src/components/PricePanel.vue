<script setup lang="ts">
/**
 * 单价目录面板：CRUD + 未配单价模型提示。
 *
 * 关键语义（FR-3.5）：删除单价后该模型历史成本转「未知」且**可撤销**——
 * 所以删除前必须提示这一点，否则用户会以为历史成本被抹掉了。
 */
import { onMounted, ref } from 'vue'
import { costApi, type PriceItem } from '../http'

const rows = ref<PriceItem[]>([])
const error = ref('')
const notice = ref('')

const draft = ref({ model: '', provider: '', inputPricePer1M: 0, outputPricePer1M: 0 })
const editing = ref<string | null>(null)

async function load() {
  error.value = ''
  try {
    rows.value = await costApi.listPrices()
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

async function submit() {
  error.value = ''
  notice.value = ''
  try {
    if (editing.value) {
      await costApi.updatePrice({ ...draft.value, model: editing.value })
      notice.value = `已更新 ${editing.value}；历史成本将按新单价重算`
    } else {
      await costApi.createPrice({ ...draft.value, isEnabled: true })
      notice.value = `已新增 ${draft.value.model}`
    }
    draft.value = { model: '', provider: '', inputPricePer1M: 0, outputPricePer1M: 0 }
    editing.value = null
    await load()
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

function edit(p: PriceItem) {
  editing.value = p.model
  draft.value = {
    model: p.model,
    provider: p.provider,
    inputPricePer1M: p.inputPricePer1M,
    outputPricePer1M: p.outputPricePer1M,
  }
}

function cancel() {
  editing.value = null
  draft.value = { model: '', provider: '', inputPricePer1M: 0, outputPricePer1M: 0 }
}

async function remove(p: PriceItem) {
  if (!window.confirm(`删除 ${p.model} 的单价？\n该模型历史成本将转为「未知」（不删除任何用量行），补回单价即可恢复核算。`)) return
  error.value = ''
  try {
    await costApi.deletePrice(p.model)
    notice.value = `已删除 ${p.model} 的单价，历史成本转未知`
    await load()
  } catch (ex) {
    error.value = (ex as Error).message
  }
}

onMounted(load)
</script>

<template>
  <section class="panel">
    <p v-if="error" class="panel__error">{{ error }}</p>
    <p v-if="notice" class="panel__notice">{{ notice }}</p>

    <form class="panel__form" @submit.prevent="editing ? submit() : submit()">
      <input v-model="draft.model" placeholder="模型名" :disabled="!!editing" required />
      <input v-model="draft.provider" placeholder="供应商" required />
      <input v-model.number="draft.inputPricePer1M" type="number" step="0.0001" min="0" placeholder="输入单价 / 1M" required />
      <input v-model.number="draft.outputPricePer1M" type="number" step="0.0001" min="0" placeholder="输出单价 / 1M" required />
      <button type="submit">{{ editing ? '更新（PUT）' : '新增（POST）' }}</button>
      <button v-if="editing" type="button" @click="cancel">取消</button>
    </form>
    <small v-if="editing">正在更新已有条目「{{ editing }}」——新增通道遇到同名会返回 400，改价请走更新。</small>

    <table>
      <thead>
        <tr><th>模型</th><th>供应商</th><th>输入/1M</th><th>输出/1M</th><th>状态</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-for="p in rows" :key="p.model">
          <td>{{ p.model }}</td>
          <td>{{ p.provider }}</td>
          <td>{{ p.inputPricePer1M }}</td>
          <td>{{ p.outputPricePer1M }}</td>
          <td>{{ p.isEnabled ? '启用' : '停用' }}</td>
          <td>
            <button type="button" @click="edit(p)">编辑</button>
            <button type="button" @click="remove(p)">删除</button>
          </td>
        </tr>
        <tr v-if="!rows.length"><td colspan="6">尚未配置单价——所有模型成本都会是「未知（下界）」，不会被静默计 0</td></tr>
      </tbody>
    </table>
  </section>
</template>

<style scoped>
.panel { display: flex; flex-direction: column; gap: 10px; }
.panel__error { color: var(--el-color-danger, #f56c6c); }
.panel__notice { color: var(--el-color-success, #67c23a); }
.panel__form { display: flex; gap: 8px; flex-wrap: wrap; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { text-align: left; padding: 4px 6px; border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5); }
</style>
