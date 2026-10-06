<script setup lang="ts">
/**
 * 总览面板：覆盖度提示 + 今日/本月/总额 + 日趋势 + 维度排行 + 延迟分位 + 错误分布。
 *
 * 两条硬规矩（02-spec FR-1 / BR-2）：
 * 1. `costIsLowerBound` 为真时，金额旁**必须**标「下界」，不能看起来像精确值。
 * 2. 主聊天链路不可观测（`coverage.mainChatObservable === false`）时**必须**显示说明。
 */
import { onMounted, ref } from 'vue'
import { costApi, type CostOverview, type DailyPoint, type DimensionItem, type ErrorStats, type LatencyStats } from '../http'

const loading = ref(false)
const error = ref('')
const ov = ref<CostOverview | null>(null)
const daily = ref<DailyPoint[]>([])
const byModel = ref<DimensionItem[]>([])
const byProvider = ref<DimensionItem[]>([])
const latency = ref<LatencyStats | null>(null)
const errors = ref<ErrorStats | null>(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [o, d, m, p, l, e] = await Promise.all([
      costApi.overview(),
      costApi.daily(),
      costApi.breakdown({ by: 'model', top: 10 }),
      costApi.breakdown({ by: 'provider', top: 10 }),
      costApi.latency(),
      costApi.errors(),
    ])
    ov.value = o
    daily.value = d
    byModel.value = m
    byProvider.value = p
    latency.value = l
    errors.value = e
  } catch (ex) {
    error.value = (ex as Error).message
  } finally {
    loading.value = false
  }
}

function money(v: number | undefined | null) {
  if (v === undefined || v === null) return '—'
  return `¥${Number(v).toFixed(4)}`
}

function ms(v: number | undefined | null) {
  if (v === undefined || v === null) return '—'
  return `${Number(v).toLocaleString()} ms`
}

function maxCost(points: DailyPoint[]) {
  return points.reduce((m, p) => Math.max(m, p.cost), 0)
}

onMounted(load)
</script>

<template>
  <section class="dash">
    <p v-if="error" class="dash__error">{{ error }}</p>

    <!-- 覆盖度：必须显眼，不能藏在角落 -->
    <aside v-if="ov" class="dash__coverage" :class="{ 'is-warn': !ov.coverage.mainChatObservable }">
      <strong>统计口径</strong>
      <span>
        已覆盖 {{ ov.coverage.coveredCalls }} / {{ ov.coverage.totalRecords }} 次调用 ·
        未配单价 {{ ov.coverage.unpricedCalls }} 次
      </span>
      <span v-if="!ov.coverage.mainChatObservable" class="dash__warn">
        ⚠ {{ ov.coverage.note }}
      </span>
    </aside>

    <div v-if="ov" class="dash__cards">
      <article class="card">
        <h4>今日成本</h4>
        <strong>{{ money(ov.todayCost) }}</strong>
      </article>
      <article class="card">
        <h4>本月成本</h4>
        <strong>{{ money(ov.monthCost) }}</strong>
      </article>
      <article class="card">
        <h4>窗口总成本 <span v-if="ov.costIsLowerBound" class="tag tag--warn">下界</span></h4>
        <strong>{{ money(ov.totalCost) }}</strong>
        <small v-if="ov.costIsLowerBound">
          含未配单价模型 {{ ov.unpricedModels.length }} 个，不计入总额
        </small>
      </article>
      <article class="card">
        <h4>轮次 / 失败</h4>
        <strong>{{ ov.turns }} / {{ ov.failCount }}</strong>
        <small>总 token {{ ov.totalTokens.toLocaleString() }}</small>
      </article>
    </div>

    <article v-if="daily.length" class="card">
      <h4>日趋势</h4>
      <div class="bars">
        <div
          v-for="p in daily"
          :key="p.day"
          class="bars__col"
          :title="`${p.day} · ${money(p.cost)} · ${p.turns} 轮${p.costIsLowerBound ? ' · 下界' : ''}`"
        >
          <div
            class="bars__fill"
            :style="{ height: maxCost(daily) ? `${(p.cost / maxCost(daily)) * 100}%` : '0%' }"
          />
          <small>{{ p.day.slice(5) }}</small>
        </div>
      </div>
    </article>

    <div class="dash__two">
      <article class="card">
        <h4>模型维度 Top 10</h4>
        <table>
          <thead>
            <tr><th>模型</th><th>轮次</th><th>成本</th><th>失败</th></tr>
          </thead>
          <tbody>
            <tr v-for="i in byModel" :key="i.key">
              <td>{{ i.key }}</td>
              <td>{{ i.turns }}</td>
              <td>{{ money(i.cost) }}<span v-if="i.costIsLowerBound" class="tag tag--warn">下界</span></td>
              <td>{{ i.failCount }}</td>
            </tr>
            <tr v-if="!byModel.length"><td colspan="4">暂无数据</td></tr>
          </tbody>
        </table>
      </article>

      <article class="card">
        <h4>供应商维度 Top 10</h4>
        <table>
          <thead>
            <tr><th>供应商</th><th>轮次</th><th>成本</th></tr>
          </thead>
          <tbody>
            <tr v-for="i in byProvider" :key="i.key">
              <td>{{ i.key }}</td>
              <td>{{ i.turns }}</td>
              <td>{{ money(i.cost) }}</td>
            </tr>
            <tr v-if="!byProvider.length"><td colspan="3">暂无数据</td></tr>
          </tbody>
        </table>
      </article>
    </div>

    <div class="dash__two">
      <article v-if="latency" class="card">
        <h4>延迟分位</h4>
        <table>
          <thead><tr><th>口径</th><th>P50</th><th>P95</th><th>P99</th><th>样本</th></tr></thead>
          <tbody>
            <tr>
              <td>首字延迟</td>
              <td>{{ ms(latency.firstToken.p50) }}</td>
              <td>{{ ms(latency.firstToken.p95) }}</td>
              <td>{{ ms(latency.firstToken.p99) }}</td>
              <td>{{ latency.firstToken.samples }}</td>
            </tr>
            <tr>
              <td>总耗时</td>
              <td>{{ ms(latency.duration.p50) }}</td>
              <td>{{ ms(latency.duration.p95) }}</td>
              <td>{{ ms(latency.duration.p99) }}</td>
              <td>{{ latency.duration.samples }}</td>
            </tr>
          </tbody>
        </table>
        <small>样本数 0 表示上游未回填，不参与分位计算</small>
      </article>

      <article v-if="errors" class="card">
        <h4>错误率</h4>
        <p>
          <strong>{{ (errors.rate * 100).toFixed(1) }}%</strong>
          （{{ errors.failed }} / {{ errors.total }}）
        </p>
        <p v-if="errors.unknownStatus > 0" class="dash__warn">
          其中 {{ errors.unknownStatus }} 条响应状态未回填（0），不可当作真实失败率
        </p>
        <table>
          <thead><tr><th>错误</th><th>次数</th></tr></thead>
          <tbody>
            <tr v-for="b in errors.buckets" :key="b.category">
              <td>{{ b.category }}</td>
              <td>{{ b.count }}</td>
            </tr>
            <tr v-if="!errors.buckets.length"><td colspan="2">无错误</td></tr>
          </tbody>
        </table>
      </article>
    </div>

    <button type="button" :disabled="loading" @click="load">
      {{ loading ? '刷新中…' : '刷新' }}
    </button>
  </section>
</template>

<style scoped>
.dash { display: flex; flex-direction: column; gap: 12px; }
.dash__error { color: var(--el-color-danger, #f56c6c); }
.dash__cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
.dash__two { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 12px; }
.card { border: 1px solid var(--el-border-color, #e5e7eb); border-radius: 6px; padding: 12px; }
.card h4 { margin: 0 0 8px; font-size: 14px; }
.card table { width: 100%; border-collapse: collapse; font-size: 13px; }
.card th, .card td { text-align: left; padding: 4px 6px; border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5); }
.dash__coverage {
  display: flex; flex-direction: column; gap: 2px;
  border: 1px solid var(--el-border-color, #e5e7eb);
  border-left: 3px solid var(--el-color-info, #909399);
  border-radius: 4px; padding: 8px 12px; font-size: 13px;
}
.dash__coverage.is-warn { border-left-color: var(--el-color-warning, #e6a23c); }
.dash__warn { color: var(--el-color-warning, #e6a23c); }
.tag { font-size: 11px; border-radius: 3px; padding: 0 4px; margin-left: 4px; }
.tag--warn { background: var(--el-color-warning-light-9, #fdf6ec); color: var(--el-color-warning, #e6a23c); }
.bars { display: flex; align-items: flex-end; gap: 4px; height: 120px; }
.bars__col { flex: 1; display: flex; flex-direction: column; justify-content: flex-end; align-items: center; height: 100%; }
.bars__fill { width: 100%; background: var(--el-color-primary, #409eff); border-radius: 2px 2px 0 0; min-height: 2px; }
</style>
