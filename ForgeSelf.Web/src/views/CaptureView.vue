<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Delete } from '@element-plus/icons-vue'
import { captureApi } from '@/services/captureApi'
import type {
  ListenerConfig,
  CreateListenerRequest,
  CaptureSessionSummary
} from '@/types/capture'
import ListenerCard from '@/components/capture/ListenerCard.vue'
import ListenerEditDialog from '@/components/capture/ListenerEditDialog.vue'
import CaptureSessionDrawer from '@/components/capture/CaptureSessionDrawer.vue'

/**
 * 抓包代理主页面（Fiddler 风格）：
 * 左栏监听器管理（新建/启停/编辑/删除 + CA 证书安装提示），
 * 右栏抓包记录列表（协议/方法/URL/状态/耗时），点击行查看完整请求详情。
 */

// ---- 监听器 ----
const listeners = ref<ListenerConfig[]>([])
const listenersLoading = ref(false)
const dialogVisible = ref(false)
const editingListener = ref<ListenerConfig | null>(null)

// ---- 抓包记录 ----
const sessions = ref<CaptureSessionSummary[]>([])
const sessionsLoading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const filterListenerId = ref<number | undefined>(undefined)
const filterProtocol = ref('')

// ---- 详情抽屉 ----
const drawerVisible = ref(false)
const activeSessionId = ref<number | null>(null)

// ---- 协议过滤选项（后端动态产生，前端仅提供常用筛选项） ----
const protocolOptions = ['HTTP', 'HTTPS', 'TCP'] as const

async function loadListeners() {
  listenersLoading.value = true
  try {
    listeners.value = await captureApi.fetchListeners()
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '加载监听器失败')
  } finally {
    listenersLoading.value = false
  }
}

async function loadSessions() {
  sessionsLoading.value = true
  try {
    const result = await captureApi.fetchSessions({
      listenerId: filterListenerId.value,
      protocol: filterProtocol.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    sessions.value = result.items
    total.value = result.total
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '加载抓包记录失败')
  } finally {
    sessionsLoading.value = false
  }
}

function refreshAll() {
  loadListeners()
  loadSessions()
}

// ---- 监听器操作 ----
function openCreateDialog() {
  editingListener.value = null
  dialogVisible.value = true
}

function openEditDialog(listener: ListenerConfig) {
  editingListener.value = listener
  dialogVisible.value = true
}

async function handleSubmit(payload: { id?: number; data: CreateListenerRequest }) {
  try {
    if (payload.id != null) {
      await captureApi.updateListener(payload.id, payload.data)
      ElMessage.success('更新监听器成功')
    } else {
      await captureApi.createListener(payload.data)
      ElMessage.success('创建监听器成功')
    }
    dialogVisible.value = false
    refreshAll()
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '保存失败')
  }
}

async function handleToggle(listener: ListenerConfig) {
  try {
    if (listener.isRunning) {
      await captureApi.stopListener(listener.id)
      ElMessage.success(`已停止监听 ${listener.name}`)
    } else {
      await captureApi.startListener(listener.id)
      ElMessage.success(`已启动监听 ${listener.name}`)
    }
    refreshAll()
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '操作失败')
  }
}

async function handleDelete(listener: ListenerConfig) {
  try {
    await ElMessageBox.confirm(
      `确认删除监听器「${listener.name}」吗？此操作不可撤销。`,
      '删除确认',
      {
        type: 'warning',
        confirmButtonText: '删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }
    )
    await captureApi.deleteListener(listener.id)
    ElMessage.success('删除成功')
    refreshAll()
  } catch (e) {
    if (e === 'cancel' || e === 'close') return
    ElMessage.error(e instanceof Error ? e.message : '删除失败')
  }
}

// ---- CA 证书 ----
async function handleDownloadCaCert() {
  try {
    const info = await captureApi.fetchCaCert()
    // 下载 PEM 到本地
    const blob = new Blob([info.pem], { type: 'application/x-pem-file' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'proxycapture-ca.cer'
    a.click()
    URL.revokeObjectURL(url)
    ElMessage.success('CA 证书已下载，请安装到「受信任的根证书颁发机构」')
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '下载 CA 证书失败')
  }
}

// ---- 抓包记录操作 ----
async function handleClearSessions() {
  try {
    await ElMessageBox.confirm('确认清空全部抓包记录吗？此操作不可撤销。', '清空确认', {
      type: 'warning',
      confirmButtonText: '清空',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger'
    })
    await captureApi.clearSessions(filterListenerId.value)
    ElMessage.success('已清空抓包记录')
    loadSessions()
  } catch (e) {
    if (e === 'cancel' || e === 'close') return
    ElMessage.error(e instanceof Error ? e.message : '清空失败')
  }
}

function openSessionDetail(row: CaptureSessionSummary) {
  activeSessionId.value = row.id
  drawerVisible.value = true
}

function handlePageChange(p: number) {
  page.value = p
  loadSessions()
}

function handleFilterChange() {
  page.value = 1
  loadSessions()
}

// 格式化时间（本地时区）
function formatTime(iso: string): string {
  return new Date(iso).toLocaleString()
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

onMounted(() => {
  refreshAll()
})
</script>

<template>
  <div class="capture-view p-6 max-w-[1400px] mx-auto">
    <header class="flex items-center justify-between mb-4">
      <h1 class="!text-2xl !font-bold !m-0 text-[var(--el-text-color-primary)]">抓包代理</h1>
      <ElButton type="primary" :icon="Plus" @click="openCreateDialog">新建监听器</ElButton>
    </header>

    <div class="capture-layout">
      <!-- 左栏：监听器管理 -->
      <aside class="capture-side">
        <ElCard shadow="never" class="!mb-4" body-class="!p-3">
          <div class="text-sm text-[var(--el-text-color-secondary)] leading-relaxed">
            <p class="!m-0">
              配置 hosts 指向监听地址后，本插件将按 Fiddler 方式记录全部请求。
            </p>
            <p class="!m-0">
              未配置目标时仅抓包（HTTP 回 200 占位）；配置目标后原样转发。
            </p>
          </div>
          <ElButton size="small" class="!mt-2 w-full" @click="handleDownloadCaCert">
            下载 HTTPS 解密 CA 证书
          </ElButton>
        </ElCard>

        <div v-loading="listenersLoading" class="listener-list">
          <ElEmpty v-if="!listeners.length && !listenersLoading" description="暂无监听器">
            <ElButton type="primary" size="small" :icon="Plus" @click="openCreateDialog">
              新建第一个监听器
            </ElButton>
          </ElEmpty>
          <ListenerCard
            v-for="listener in listeners"
            :key="listener.id"
            :listener="listener"
            @toggle="handleToggle"
            @edit="openEditDialog"
            @delete="handleDelete"
          />
        </div>
      </aside>

      <!-- 右栏：抓包记录 -->
      <section class="capture-main">
        <ElCard shadow="never" body-class="!p-3 !pb-0">
          <div class="flex items-center justify-between flex-wrap gap-3">
            <div class="flex items-center gap-3 flex-wrap">
              <ElSelect
                :model-value="filterListenerId"
                placeholder="全部监听器"
                clearable
                style="width: 160px"
                @update:model-value="(v: number | undefined) => { if (v !== filterListenerId) filterListenerId = v }"
                @change="handleFilterChange"
              >
                <ElOption v-for="l in listeners" :key="l.id" :label="`${l.name} (:${l.listenPort})`" :value="l.id" />
              </ElSelect>
              <ElSelect
                :model-value="filterProtocol"
                placeholder="全部协议"
                clearable
                style="width: 120px"
                @update:model-value="(v: string) => { if (v !== filterProtocol) filterProtocol = v }"
                @change="handleFilterChange"
              >
                <ElOption v-for="p in protocolOptions" :key="p" :label="p" :value="p" />
              </ElSelect>
              <span class="text-sm text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
            </div>
            <div class="flex items-center gap-2">
              <ElButton size="small" :icon="Refresh" @click="loadSessions">刷新</ElButton>
              <ElButton
                size="small"
                type="danger"
                plain
                :icon="Delete"
                @click="handleClearSessions"
              >
                清空
              </ElButton>
            </div>
          </div>
        </ElCard>

        <ElCard shadow="never" body-class="!p-0">
          <ElTable
            v-loading="sessionsLoading"
            :data="sessions"
            size="small"
            class="capture-table"
            @row-click="openSessionDetail"
          >
            <ElTableColumn prop="id" label="#" width="64" />
            <ElTableColumn prop="protocol" label="协议" width="76">
              <template #default="{ row }">
                <ElTag size="small" effect="plain">{{ row.protocol }}</ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn prop="method" label="方法" width="80">
              <template #default="{ row }">{{ row.method || '-' }}</template>
            </ElTableColumn>
            <ElTableColumn prop="url" label="URL" min-width="280" show-overflow-tooltip />
            <ElTableColumn prop="statusCode" label="状态" width="72">
              <template #default="{ row }">
                <ElTag
                  v-if="row.statusCode"
                  size="small"
                  :type="row.statusCode >= 400 ? 'danger' : row.statusCode >= 200 ? 'success' : 'info'"
                  effect="plain"
                >
                  {{ row.statusCode }}
                </ElTag>
                <span v-else>-</span>
              </template>
            </ElTableColumn>
            <ElTableColumn prop="forwarded" label="转发" width="72">
              <template #default="{ row }">
                <ElTag :type="row.forwarded ? 'success' : 'info'" size="small" effect="plain">
                  {{ row.forwarded ? '是' : '否' }}
                </ElTag>
              </template>
            </ElTableColumn>
            <ElTableColumn prop="requestBytes" label="大小" width="96">
              <template #default="{ row }">{{ formatBytes(row.requestBytes + row.responseBytes) }}</template>
            </ElTableColumn>
            <ElTableColumn prop="durationMs" label="耗时" width="80">
              <template #default="{ row }">{{ row.durationMs }} ms</template>
            </ElTableColumn>
            <ElTableColumn prop="timestamp" label="时间" width="168">
              <template #default="{ row }">{{ formatTime(row.timestamp) }}</template>
            </ElTableColumn>
          </ElTable>

          <div
            v-if="total > pageSize"
            class="!py-3 flex justify-center border-t border-[var(--el-border-color-lighter)]"
          >
            <ElPagination
              :current-page="page"
              :page-size="pageSize"
              :total="total"
              layout="prev, pager, next"
              @current-change="handlePageChange"
            />
          </div>
        </ElCard>
      </section>
    </div>

    <ListenerEditDialog
      v-model:visible="dialogVisible"
      :listener="editingListener"
      @submit="handleSubmit"
      @cancel="dialogVisible = false"
    />

    <CaptureSessionDrawer
      v-model:visible="drawerVisible"
      :session-id="activeSessionId"
    />
  </div>
</template>

<style scoped>
.capture-layout {
  display: grid;
  grid-template-columns: 340px 1fr;
  gap: 16px;
  align-items: start;
}
.capture-side {
  min-width: 0;
}
.capture-main {
  min-width: 0;
}
.capture-table {
  cursor: pointer;
}
@media (max-width: 960px) {
  .capture-layout {
    grid-template-columns: 1fr;
  }
}
</style>
