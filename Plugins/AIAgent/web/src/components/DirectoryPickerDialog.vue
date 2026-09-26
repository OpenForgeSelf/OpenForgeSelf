<template>
  <!-- 目录选择弹窗：点击「浏览」后弹出，逐级浏览宿主磁盘并选定工作目录。
       为什么自研（不用 <input webkitdirectory>）：浏览器文件夹选择器出于安全只返回
       相对路径，拿不到后端 TrySetProjectRoot 需要的绝对路径——因此由后端提供
       磁盘浏览接口（/api/project/browse-directories），此弹窗逐级进入后回传绝对路径。 -->
  <ElDialog
    :model-value="visible"
    title="选择工作目录"
    width="480px"
    :close-on-click-modal="false"
    append-to-body
    @update:model-value="emit('update:visible', $event)"
    @open="onOpen"
  >
    <!-- 当前路径（面包屑可回跳任意层级） -->
    <div class="dpick__crumb">
      <button
        v-for="(seg, i) in crumbs"
        :key="i"
        type="button"
        class="dpick__crumb-item"
        :title="seg.path"
        @click="goTo(seg.path)"
      >
        {{ seg.label }}
      </button>
      <span v-if="loading" class="dpick__loading">加载中…</span>
    </div>

    <!-- 错误提示 -->
    <p v-if="error" class="dpick__error">{{ error }}</p>

    <!-- 目录列表 -->
    <ElScrollbar class="dpick__list">
      <button
        v-for="item in items"
        :key="item.path"
        type="button"
        class="dpick__item"
        :title="item.path"
        @click="goTo(item.path)"
      >
        <Folder :size="14" class="dpick__ico" />
        <span class="dpick__name">{{ item.name }}</span>
      </button>
      <p v-if="!loading && items.length === 0" class="dpick__empty">没有子目录</p>
    </ElScrollbar>

    <template #footer>
      <div class="dpick__foot">
        <span class="dpick__selected" :title="current">{{ current || '尚未选择' }}</span>
        <ElButton size="small" @click="emit('update:visible', false)">取消</ElButton>
        <ElButton type="primary" size="small" :disabled="!current" @click="confirmSelect">
          选择此目录
        </ElButton>
      </div>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
/**
 * 「选择工作目录」弹窗（共享组件：ChatPanel 📁 popover 与 ContextPanel 右栏共用）。
 *
 * 数据源：GET /api/project/browse-directories?path=<绝对路径>（后端磁盘级浏览）。
 * 交互：进入弹窗先列磁盘根（此电脑/各卷）→ 点目录逐级下钻 → 面包屑回跳 →
 *       「选择此目录」把当前绝对路径 emit 给父层（父层走原有 POST /api/project/directory 持久化链路）。
 */
import { computed, ref, watch } from 'vue'
// EP 组件/图标：插件是独立预编译产物，必须显式 import（经宿主共享桥取同一份实例）。
import { ElButton, ElDialog, ElScrollbar } from 'element-plus'
import { Folder } from '@element-plus/icons-vue'
import { apiGet, withQuery } from '../http'

/** 后端 BrowseDirectoryEntry（camelCase 序列化）。 */
interface BrowseDirectoryEntry {
  /** 目录名（磁盘根时为卷名，如 "C:\"）。 */
  name?: string
  /** 目录绝对路径。 */
  path?: string
}

const props = defineProps<{
  /** 弹窗是否可见。 */
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  /** 用户选定某绝对路径（父层负责写后端 + 更新状态）。 */
  (e: 'select', path: string): void
}>()

/** 当前浏览的绝对路径（空串 = 磁盘根「此电脑」）。 */
const current = ref('')
/** 当前层子目录列表。 */
const items = ref<BrowseDirectoryEntry[]>([])
const loading = ref(false)
const error = ref('')

/**
 * 面包屑分段：磁盘根为「此电脑」，其后逐段累计。
 * 首段为盘符（含 ':'）时补右分隔符；Unix 绝对路径（以 / 开头）首段补根 /；
 * UNC（\\server\share）各段用反斜杠拼接；其余中间段统一以 / 追加
 * （Windows 的 Path/Directory API 两种分隔符都接受）。
 * 最后一段用后端原始路径校正，保证与后端大小写/分隔符完全一致。
 */
const crumbs = computed(() => {
  const segs: Array<{ label: string; path: string }> = [{ label: '此电脑', path: '' }]
  if (!current.value) return segs
  const parts = current.value.split(/[\\/]+/).filter(Boolean)
  const rooted = /^[\\/]/.test(current.value)
  const unc = /^\\\\/.test(current.value)
  parts.forEach((p, i) => {
    if (i === 0 && p.includes(':')) {
      segs.push({ label: p, path: `${p}\\` })
    } else if (unc) {
      // UNC：统一反斜杠拼接，避免混用分隔符导致后端 Path 解析失败。
      const prev = segs[segs.length - 1]?.path ?? ''
      segs.push({ label: p, path: `${prev}${prev.endsWith('\\') ? '' : '\\'}${p}` })
    } else if (i === 0 && rooted) {
      segs.push({ label: p, path: `/${p}` })
    } else {
      const prev = segs[segs.length - 1]?.path ?? ''
      const joiner = prev.endsWith('/') || prev.endsWith('\\') ? '' : '/'
      segs.push({ label: p, path: `${prev}${joiner}${p}` })
    }
  })
  // 最后一层用后端原文校正，避免拼接产生的分隔符/大小写差异。
  if (segs.length > 1) segs[segs.length - 1]!.path = current.value
  return segs
})

/** 拉取指定层的子目录（path 为空 = 磁盘根）。 */
async function browse(path: string) {
  loading.value = true
  error.value = ''
  try {
    const data = await apiGet<{ currentPath?: string; parentPath?: string | null; items?: BrowseDirectoryEntry[] }>(
      withQuery('/api/project/browse-directories', { path: path || undefined })
    )
    current.value = data?.currentPath ?? ''
    items.value = data?.items ?? []
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
    items.value = []
  } finally {
    loading.value = false
  }
}

/** 打开弹窗：从磁盘根开始（既有选择不自动回显到浏览位置，避免深层路径慢加载）。 */
function onOpen() {
  current.value = ''
  items.value = []
  error.value = ''
  void browse('')
}

/** 进入某目录。 */
function goTo(path: string) {
  void browse(path)
}

/** 确认选择：把绝对路径交给父层（父层负责写后端 + 更新状态 + 记入最近使用）。 */
function confirmSelect() {
  if (!current.value) return
  emit('select', current.value)
  emit('update:visible', false)
}

/** 弹窗关闭时清掉错误/加载态，下次打开是干净状态。 */
watch(
  () => props.visible,
  (v) => {
    if (!v) {
      error.value = ''
      loading.value = false
    }
  },
)
</script>

<style scoped>
/* 面包屑：可点击回跳，横排可滚动（长路径不撑破弹窗）。 */
.dpick__crumb {
  display: flex;
  align-items: center;
  gap: 2px;
  padding: 6px 8px;
  margin-bottom: 8px;
  overflow-x: auto;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  scrollbar-width: thin;
}

.dpick__crumb-item {
  flex-shrink: 0;
  padding: 1px 4px;
  border: none;
  border-radius: 3px;
  background: transparent;
  color: var(--el-color-primary, #ffb84d);
  font-family: inherit;
  font-size: inherit;
  cursor: pointer;
}

.dpick__crumb-item:hover {
  background: var(--el-fill-color, #262727);
}

.dpick__loading {
  margin-left: auto;
  flex-shrink: 0;
  color: var(--el-text-color-placeholder, #8c959f);
}

.dpick__error {
  margin: 0 0 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-danger, #f56c6c);
}

/* 目录列表：固定高度内部滚动。 */
.dpick__list {
  height: 260px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
}

.dpick__item {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 5px 8px;
  border: none;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: var(--el-font-size-small, 13px);
  text-align: left;
  cursor: pointer;
}

.dpick__item:hover {
  background: var(--el-fill-color, #262727);
}

.dpick__ico {
  flex-shrink: 0;
  width: 14px;
  height: 14px;
  color: var(--el-color-primary, #ffb84d);
}

.dpick__name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dpick__empty {
  margin: 0;
  padding: 12px 8px;
  text-align: center;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.dpick__foot {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.dpick__selected {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-text-color-secondary, #a3a6ad);
}
</style>
