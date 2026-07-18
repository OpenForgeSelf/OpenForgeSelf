<script setup lang="ts">
import { ref, onMounted } from 'vue'
import type { ChatRecord, ChatRecordsResponse } from '@/types/chatRecords'
import { chatRecordsApi } from '@/services/chatRecordsApi'
import ChatRecordsList from '@/components/chatrecords/ChatRecordsList.vue'
import ChatRecordDetail from '@/components/chatrecords/ChatRecordDetail.vue'

const sessionId = ref('')
const style = ref('')
const fromDate = ref('')
const toDate = ref('')
const page = ref(1)
const pageSize = ref(20)

const records = ref<ChatRecordsResponse['records']>([])
const total = ref(0)
const loading = ref(false)
const selectedRecord = ref<ChatRecord | null>(null)
const detailLoading = ref(false)
const showDetail = ref(false)

const styleOptions = [
  { value: '', label: '全部' },
  { value: 'OpenAI_Chat', label: 'OpenAI Chat' },
  { value: 'OpenAI_Responses', label: 'OpenAI Responses' },
  { value: 'Anthropic_Messages', label: 'Anthropic Messages' }
]

async function fetchRecords() {
  loading.value = true
  try {
    const response = await chatRecordsApi.getRecords({
      sessionId: sessionId.value || undefined,
      style: style.value || undefined,
      from: fromDate.value || undefined,
      to: toDate.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    records.value = response.records
    total.value = response.total
  } catch (err) {
    console.error('获取聊天记录失败:', err)
  } finally {
    loading.value = false
  }
}

async function handleViewDetail(id: number) {
  detailLoading.value = true
  showDetail.value = true
  try {
    selectedRecord.value = await chatRecordsApi.getRecordById(id)
  } catch (err) {
    console.error('获取记录详情失败:', err)
  } finally {
    detailLoading.value = false
  }
}

function handlePageChange(newPage: number) {
  page.value = newPage
  fetchRecords()
}

function closeDetail() {
  showDetail.value = false
  selectedRecord.value = null
}

function handleSearch() {
  page.value = 1
  fetchRecords()
}

function handleReset() {
  sessionId.value = ''
  style.value = ''
  fromDate.value = ''
  toDate.value = ''
  page.value = 1
  fetchRecords()
}

onMounted(() => {
  fetchRecords()
})
</script>

<template>
  <div class="chat-records-view">
    <header class="view-header">
      <h1 class="view-title">
        <span class="title-icon">💬</span>
        聊天记录查看
      </h1>
      <p class="view-subtitle">查看和管理 API 调用记录，包括 OpenAI Chat、OpenAI Responses 和 Anthropic Messages 格式</p>
    </header>

    <div class="filter-bar">
      <div class="filter-row">
        <div class="filter-item">
          <label class="filter-label">SessionId</label>
          <input
            v-model="sessionId"
            type="text"
            class="filter-input"
            placeholder="输入 SessionId"
            @keyup.enter="handleSearch"
          />
        </div>

        <div class="filter-item">
          <label class="filter-label">Style</label>
          <select v-model="style" class="filter-select">
            <option v-for="opt in styleOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <div class="filter-item">
          <label class="filter-label">开始日期</label>
          <input
            v-model="fromDate"
            type="date"
            class="filter-input"
          />
        </div>

        <div class="filter-item">
          <label class="filter-label">结束日期</label>
          <input
            v-model="toDate"
            type="date"
            class="filter-input"
          />
        </div>
      </div>

      <div class="filter-actions">
        <button class="search-btn" @click="handleSearch">
          <i class="fa-solid fa-search" />
          搜索
        </button>
        <button class="reset-btn" @click="handleReset">
          <i class="fa-solid fa-rotate-left" />
          重置
        </button>
      </div>
    </div>

    <div class="content-area">
      <div class="list-section" :class="{ 'with-detail': showDetail }">
        <ChatRecordsList
          :records="records"
          :total="total"
          :page="page"
          :page-size="pageSize"
          :loading="loading"
          @view-detail="handleViewDetail"
          @page-change="handlePageChange"
        />
      </div>

      <div v-if="showDetail" class="detail-section">
        <div class="detail-header">
          <h3 class="detail-title">详情</h3>
          <button class="close-btn" @click="closeDetail">
            <i class="fa-solid fa-xmark" />
          </button>
        </div>
        <div class="detail-content">
          <div v-if="detailLoading" class="detail-loading">
            <div class="spinner" />
            <span>加载中...</span>
          </div>
          <ChatRecordDetail v-else-if="selectedRecord" :record="selectedRecord" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.chat-records-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

.view-header {
  flex-shrink: 0;
}

.view-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.title-icon {
  font-size: 28px;
}

.view-subtitle {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.filter-bar {
  flex-shrink: 0;
  padding: 16px;
  background: var(--bg-secondary);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
}

.filter-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 180px;
}

.filter-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-muted);
}

.filter-input,
.filter-select {
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  font-size: 14px;
  background: var(--bg-card);
}

.filter-input:focus,
.filter-select:focus {
  outline: none;
  border-color: var(--primary-color);
}

.filter-actions {
  display: flex;
  gap: 8px;
}

.search-btn,
.reset-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.search-btn {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

.search-btn:hover {
  background: var(--primary-hover);
}

.reset-btn {
  background: var(--text-muted);
  color: var(--primary-contrast);
}

.reset-btn:hover {
  opacity: 0.85;
}

.content-area {
  flex: 1;
  display: flex;
  gap: 16px;
  min-height: 0;
  overflow: hidden;
}

.list-section {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.list-section.with-detail {
  flex: 0 0 60%;
}

.detail-section {
  flex: 0 0 38%;
  display: flex;
  flex-direction: column;
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.detail-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.close-btn {
  padding: 6px 10px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  background: var(--border-color);
  color: var(--text-secondary);
}

.detail-content {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.detail-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  height: 100%;
  color: var(--text-muted);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid var(--border-color);
  border-top-color: var(--primary-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>