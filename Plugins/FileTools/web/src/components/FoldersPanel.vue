<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { useFolderScanStore } from '../stores/fileToolFolders'
import {
  displayRows,
  EMPTY_STATE_TEXT,
  partitionOk,
  partitionRemainder,
  pickEmptyState
} from './foldersModel'
import { SCAN_STATE_NAME, type FolderSizeRow } from '../types/fileTools'

const store = useFolderScanStore()

const snapshotNote = ref('')
const compareFrom = ref<number | null>(null)
const compareTo = ref<number | null>(null)

const topOptions = [20, 50, 100, 200, 500]

const stateName = computed(() => SCAN_STATE_NAME[store.view?.state ?? -1] ?? '')

onMounted(() => {
  void store.loadSnapshots()
})
onUnmounted(() => {
  store.stopPolling()
})

async function askConfirm(message: string, title: string, confirmText = '确定'): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, title, {
      type: 'warning',
      confirmButtonText: confirmText,
      cancelButtonText: '取消'
    })
    return true
  } catch {
    return false
  }
}

async function cancelScan(): Promise<void> {
  await store.confirmCancelScan(() =>
    askConfirm('取消后本次扫描立即停止，已扫到的部分结果仍可保留查看。是否取消？', '取消扫描', '取消扫描')
  )
}

async function saveSnapshot(): Promise<void> {
  await store.saveSnapshot(snapshotNote.value, () =>
    askConfirm(
      '快照会把当前排行写入数据库，之后不可修改（要更新请重新扫描再存一份）。是否保存？',
      '保存快照',
      '保存'
    )
  )
  snapshotNote.value = ''
}

async function removeSnapshot(id: number, rootPath: string): Promise<void> {
  await store.confirmDeleteSnapshot(id, () =>
    askConfirm(
      `只删除这条快照记录（含其排行明细），不会删除磁盘上的 ${rootPath} 任何文件。是否删除？`,
      '删除快照',
      '删除'
    )
  )
}

async function doCompare(): Promise<void> {
  if (compareFrom.value === null || compareTo.value === null) {
    store.error = '请先选择要对比的两个快照'
    return
  }
  await store.runCompare(compareFrom.value, compareTo.value)
}

function bytes(row: FolderSizeRow): string {
  return row.totalFormatted
}

function barWidth(row: FolderSizeRow, maxBytes?: number): string {
  // 条形长度只作视觉比较：扫描视图用本次排行最大值，快照明细用该快照自己的最大值
  const max = maxBytes ?? (store.view && store.view.items.length > 0 ? store.view.items[0].totalBytes : 0)
  if (max <= 0) return row.totalBytes > 0 ? '2%' : '0%'
  return `${Math.max(row.totalBytes > 0 ? 1 : 0, (row.totalBytes / max) * 100).toFixed(2)}%`
}
</script>

<template>
  <section class="folders-panel">
    <div class="toolbar">
      <input
        v-model="store.directory"
        class="path-input"
        type="text"
        placeholder="要统计的目录绝对路径，例如 C:\Projects"
        aria-label="目录路径"
        @keyup.enter="store.startScan()"
      />
      <label class="top-picker">
        Top
        <select v-model.number="store.top" aria-label="排行条数">
          <option v-for="n in topOptions" :key="n" :value="n">{{ n }}</option>
        </select>
      </label>
      <button class="btn primary" :disabled="store.busy || store.isScanning" @click="store.startScan()">
        {{ store.isScanning ? '扫描中…' : '扫描' }}
      </button>
      <button class="btn" :disabled="!store.isScanning" @click="cancelScan()">取消</button>
      <button
        class="btn"
        :disabled="!store.hasResult || store.isScanning || !store.scanId"
        @click="saveSnapshot()"
      >
        保存快照
      </button>
      <input
        v-model="snapshotNote"
        class="note-input"
        type="text"
        placeholder="快照备注（可空）"
        aria-label="快照备注"
      />
    </div>

    <div v-if="store.error" class="error-detail" role="alert">
      <div class="error-title">操作失败（原因留痕，不会一闪而过）</div>
      <pre class="error-text">{{ store.error }}</pre>
      <button class="link-btn" @click="store.clearError()">清除</button>
    </div>

    <div v-if="store.view" class="summary">
      <span>根：<b class="ellipsis" :title="store.view.rootPath">{{ store.view.rootPath }}</b></span>
      <span>总占用：<b>{{ store.view.rootTotalFormatted }}</b></span>
      <span>目录：<b>{{ store.view.directoryCount }}</b></span>
      <span>文件：<b>{{ store.view.fileCount }}</b></span>
      <span>耗时：<b>{{ store.view.durationMs }}</b> ms</span>
      <span v-if="store.view.state !== 2" class="state-tag">{{ stateName }}</span>
      <span v-if="store.view.inaccessibleCount > 0" class="warn-tag">
        无权限条目 {{ store.view.inaccessibleCount }}
      </span>
      <span v-if="store.view.skippedReparseCount > 0" class="warn-tag">
        跳过链接 {{ store.view.skippedReparseCount }}
      </span>
      <span v-if="store.view.truncated" class="warn-tag" :title="store.view.capNote">
        结果已截断{{ store.view.capNote ? `：${store.view.capNote}` : '' }}
      </span>
    </div>

    <div v-if="store.isScanning" class="progress">
      <div class="progress-fill" :style="{ width: `${store.percentDone}%` }" />
    </div>

    <p v-if="store.view?.partial" class="hint">扫描进行中，下表是截至当前已计入的部分结果，会每 1 秒刷新。</p>

    <div v-if="!store.view" class="empty">{{ EMPTY_STATE_TEXT.expired }}</div>
    <div v-else-if="displayRows(store.view).length === 0" class="empty">
      {{ EMPTY_STATE_TEXT[pickEmptyState(store.view)] }}
    </div>

    <template v-else>
      <table class="rank-table">
        <thead>
          <tr>
            <th class="col-name">子目录</th>
            <th class="col-num">占用</th>
            <th class="col-bar">占根目录</th>
            <th class="col-num">本级文件</th>
            <th class="col-num">子目录</th>
            <th class="col-op" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in displayRows(store.view)" :key="row.relativePath || row.name" :class="{ muted: row.isOther || !row.relativePath }">
            <td class="col-name">
              <span class="ellipsis" :title="row.relativePath || row.name">{{ row.name }}</span>
            </td>
            <td class="col-num">{{ bytes(row) }}</td>
            <td class="col-bar">
              <span class="bar" :style="{ width: barWidth(row) }" />
              <span class="pct">{{ row.percentage.toFixed(2) }}%</span>
            </td>
            <td class="col-num">{{ row.fileCount }}</td>
            <td class="col-num">{{ row.dirCount }}</td>
            <td class="col-op">
              <button
                v-if="row.relativePath && !row.isOther"
                class="link-btn"
                title="以该目录为新根重新扫描"
                @click="store.drillInto(row)"
              >
                钻取
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-if="!partitionOk(store.view)" class="warn-line">
        占比未闭合（余量 {{ partitionRemainder(store.view) }} 字节）—— 结果不完整，请以此为已知偏差，勿当作准确占用。
      </p>
    </template>

    <div class="snapshots">
      <div class="section-head">
        <h2>快照</h2>
        <button class="link-btn" @click="store.loadSnapshots()">刷新列表</button>
      </div>
      <div v-if="store.snapshots.length === 0" class="empty small">
        暂无快照。完成一次扫描后点「保存快照」，之后可在这里做占用趋势对比。
      </div>
      <table v-else class="rank-table">
        <thead>
          <tr>
            <th class="col-name">根路径</th>
            <th class="col-num">扫描时间</th>
            <th class="col-num">总占用</th>
            <th class="col-num">条数</th>
            <th class="col-op">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="s in store.snapshots" :key="s.id">
            <td class="col-name">
              <span class="ellipsis" :title="s.rootPath">{{ s.rootPath }}</span>
              <span v-if="s.rootPathMissing" class="warn-tag" title="原扫描路径当前已不存在">原路径已不存在</span>
              <span v-if="s.note" class="note">「{{ s.note }}」</span>
            </td>
            <td class="col-num">{{ new Date(s.scannedAt).toLocaleString() }}</td>
            <td class="col-num">{{ s.rootTotalFormatted }}</td>
            <td class="col-num">{{ s.top }}</td>
            <td class="col-op">
              <button class="link-btn" @click="store.openSnapshot(s.id)">查看</button>
              <button class="link-btn" @click="compareFrom = s.id">设为起点</button>
              <button class="link-btn" @click="compareTo = s.id">设为终点</button>
              <button class="link-btn danger" @click="removeSnapshot(s.id, s.rootPath)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="store.snapshots.length >= 2" class="compare-bar">
        <span>趋势对比：起点 #{{ compareFrom ?? '未选' }} → 终点 #{{ compareTo ?? '未选' }}</span>
        <button class="btn" :disabled="compareFrom === null || compareTo === null" @click="doCompare()">对比</button>
      </div>
      <p v-if="store.compareNote" class="hint">{{ store.compareNote }}</p>
      <table v-if="store.compareRows.length > 0" class="rank-table">
        <thead>
          <tr>
            <th class="col-name">子目录</th>
            <th class="col-num">起点</th>
            <th class="col-num">终点</th>
            <th class="col-num">增减</th>
            <th class="col-num">增减 %</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="c in store.compareRows" :key="c.relativePath">
            <td class="col-name">
              <span class="ellipsis" :title="c.relativePath">{{ c.name }}</span>
              <span v-if="c.missing" class="warn-tag">终点已无</span>
              <span v-if="c.added" class="ok-tag">新增</span>
            </td>
            <td class="col-num">{{ c.fromBytes }}</td>
            <td class="col-num">{{ c.toBytes }}</td>
            <td class="col-num" :class="c.deltaBytes >= 0 ? 'up' : 'down'">
              {{ c.deltaBytes >= 0 ? '+' : '' }}{{ c.deltaBytes }}
            </td>
            <td class="col-num">{{ c.deltaPercent.toFixed(2) }}%</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="store.detail" class="snap-detail">
      <div class="section-head">
        <h2>快照 #{{ store.detail.snapshot.id }} 明细</h2>
        <button class="link-btn" @click="store.detail = null">收起</button>
      </div>
      <table class="rank-table">
        <tbody>
          <tr v-for="row in displayRows(store.detail)" :key="row.relativePath || row.name">
            <td class="col-name"><span class="ellipsis" :title="row.relativePath">{{ row.name }}</span></td>
            <td class="col-num">{{ row.totalFormatted }}</td>
            <td class="col-bar"><span class="bar" :style="{ width: barWidth(row, store.detail.items[0]?.totalBytes) }" /><span class="pct">{{ row.percentage.toFixed(2) }}%</span></td>
            <td class="col-num">{{ row.fileCount }}</td>
            <td class="col-num">{{ row.dirCount }}</td>
            <td class="col-op" />
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.folders-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  flex-shrink: 0;
}

.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.path-input,
.note-input {
  padding: 8px 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background-color: var(--el-bg-color);
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.path-input {
  flex: 1 1 320px;
  min-width: 220px;
}

.note-input {
  flex: 0 1 200px;
}

.top-picker {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.top-picker select {
  padding: 6px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background-color: var(--el-bg-color);
  color: var(--el-text-color-primary);
}

.btn {
  padding: 8px 14px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background-color: var(--el-bg-color);
  color: var(--el-text-color-primary);
  font-size: 14px;
  cursor: pointer;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn.primary {
  background-color: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: #fff;
}

.link-btn {
  background: none;
  border: none;
  color: var(--el-color-primary);
  cursor: pointer;
  font-size: 13px;
  padding: 2px 4px;
}

.link-btn.danger {
  color: var(--el-color-danger);
}

.error-detail {
  border: 1px solid rgba(180, 83, 9, 0.25);
  border-radius: 8px;
  padding: 10px 12px;
  background-color: rgba(180, 83, 9, 0.06);
}

.error-title {
  font-size: 13px;
  color: var(--el-color-warning);
  margin-bottom: 4px;
}

.error-text {
  margin: 0;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
  color: var(--el-text-color-regular);
}

.summary {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.ellipsis {
  display: inline-block;
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
}

.state-tag,
.warn-tag,
.ok-tag {
  padding: 1px 6px;
  border-radius: 10px;
  font-size: 12px;
  background-color: var(--el-fill-color);
  color: var(--el-text-color-secondary);
}

.warn-tag {
  color: var(--el-color-warning);
  background-color: rgba(180, 83, 9, 0.1);
}

.ok-tag {
  color: var(--el-color-success);
  background-color: rgba(103, 194, 58, 0.12);
}

.progress {
  height: 4px;
  border-radius: 2px;
  background-color: var(--el-border-color);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background-color: var(--el-color-primary);
  transition: width 0.4s ease;
}

.hint {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.empty {
  padding: 24px 12px;
  text-align: center;
  font-size: 14px;
  color: var(--el-text-color-secondary);
  border: 1px dashed var(--el-border-color);
  border-radius: 8px;
}

.empty.small {
  padding: 14px;
  font-size: 13px;
}

.rank-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  table-layout: fixed;
}

.rank-table th,
.rank-table td {
  padding: 6px 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  text-align: left;
}

.rank-table th {
  color: var(--el-text-color-secondary);
  font-weight: 500;
}

.col-name {
  width: 32%;
}

.col-num {
  width: 12%;
  text-align: right;
}

.col-bar {
  width: 24%;
}

.col-op {
  width: 20%;
  white-space: nowrap;
}

.rank-table tbody tr.muted {
  color: var(--el-text-color-secondary);
  background-color: var(--el-fill-color-light);
}

.bar {
  display: inline-block;
  height: 8px;
  border-radius: 4px;
  background-color: var(--el-color-primary-light-5);
  vertical-align: middle;
}

.pct {
  margin-left: 6px;
  font-variant-numeric: tabular-nums;
}

.up {
  color: var(--el-color-danger);
}

.down {
  color: var(--el-color-success);
}

.warn-line {
  margin: 0;
  font-size: 12px;
  color: var(--el-color-warning);
}

.section-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.section-head h2 {
  margin: 0;
  font-size: 15px;
  color: var(--el-text-color-primary);
}

.snapshots,
.snap-detail {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color);
}

.compare-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.note {
  margin-left: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

@media (max-width: 640px) {
  .col-num,
  .col-bar {
    display: none;
  }

  .col-name,
  .col-op {
    width: 50%;
  }
}
</style>
