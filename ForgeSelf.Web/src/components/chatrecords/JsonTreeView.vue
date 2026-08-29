<script setup lang="ts">
import { ref, computed } from 'vue'
import { FIELD_MEANINGS } from '@/utils/jsonFieldMeanings'

const props = withDefaults(defineProps<{
  data: unknown
  name?: string
  depth?: number
  defaultExpanded?: boolean
}>(), {
  name: '',
  depth: 0,
  defaultExpanded: false
})

// 节点展开/收起状态（默认收起）
const expanded = ref(props.defaultExpanded)

const isArray = computed(() => Array.isArray(props.data))
const isObject = computed(() => props.data !== null && typeof props.data === 'object')

// 子节点列表（对象取键值对，数组取下标）
const childEntries = computed<{ key: string; value: unknown }[]>(() => {
  if (!isObject.value) return []
  if (isArray.value) {
    return (props.data as unknown[]).map((v, i) => ({ key: String(i), value: v }))
  }
  return Object.entries(props.data as Record<string, unknown>).map(([k, v]) => ({ key: k, value: v }))
})

const typeLabel = computed(() => {
  if (props.data === null) return 'null'
  if (isArray.value) return `数组[${childEntries.value.length}]`
  if (isObject.value) return `对象{${childEntries.value.length}}`
  if (typeof props.data === 'number') return '数字'
  if (typeof props.data === 'boolean') return '布尔'
  if (typeof props.data === 'string') return '字符串'
  return typeof props.data
})

const primitiveText = computed(() => {
  if (props.data === null) return 'null'
  if (typeof props.data === 'string') return `"${props.data}"`
  return String(props.data)
})

// 该键的中文含义（对照用）
const meaning = computed(() => {
  const key = props.name
  if (!key) return ''
  return FIELD_MEANINGS[key] ?? ''
})

function toggle(): void {
  expanded.value = !expanded.value
}
</script>

<template>
  <div class="json-tree-node" :class="{ 'is-root': depth === 0 }">
    <!-- 复合类型（对象/数组）：可折叠 -->
    <div
      v-if="isObject"
      class="node-row"
      role="button"
      tabindex="0"
      @click="toggle"
      @keydown.enter.prevent="toggle"
    >
      <span class="twisty">{{ expanded ? '▾' : '▸' }}</span>
      <span v-if="name" class="node-key">{{ name }}</span>
      <span class="node-type">{{ typeLabel }}</span>
      <span v-if="meaning" class="node-meaning">· {{ meaning }}</span>
    </div>

    <!-- 基本类型：叶子 -->
    <div v-else class="node-row leaf">
      <span class="twisty-spacer" />
      <span v-if="name" class="node-key">{{ name }}</span>
      <span class="node-type">{{ typeLabel }}</span>
      <span v-if="meaning" class="node-meaning">· {{ meaning }}</span>
      <span class="node-value">: {{ primitiveText }}</span>
    </div>

    <!-- 子节点（默认收起，展开后递归渲染） -->
    <div v-if="isObject && expanded" class="node-children">
      <JsonTreeView
        v-for="(entry, i) in childEntries"
        :key="i"
        :data="entry.value"
        :name="entry.key"
        :depth="depth + 1"
        :default-expanded="false"
      />
    </div>
  </div>
</template>

<style scoped>
.json-tree-node {
  font-size: 13px;
  line-height: 1.6;
  overflow-x: auto;           /* 整树横向滚动 */
}

.node-row {
  display: flex;
  align-items: baseline;
  gap: 6px;
  padding: 2px 4px;
  border-radius: 4px;
  cursor: pointer;
  user-select: none;
  min-width: 0;           /* 允许 flex 子项收缩到小于内容宽度 */
}
.node-row:hover {
  background: var(--el-fill-color-light);
}
.node-row.leaf {
  cursor: default;
}
.node-row.leaf:hover {
  background: transparent;
}

.twisty {
  width: 14px;
  flex-shrink: 0;
  display: inline-block;
  color: var(--el-text-color-secondary);
  font-size: 11px;
}
.twisty-spacer {
  width: 14px;
  flex-shrink: 0;
  display: inline-block;
}

.node-key {
  font-weight: 600;
  color: var(--el-color-primary);
  white-space: nowrap;        /* 键名不折行 */
  overflow: hidden;
  text-overflow: ellipsis;    /* 超长键名省略号截断 */
}
.node-type {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  flex-shrink: 0;
}
.node-meaning {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  font-style: italic;
}
.node-value {
  color: var(--el-text-color-primary);
  word-break: break-all;
  white-space: pre-wrap;      /* 保留换行与空格 */
  overflow-x: auto;           /* 超长值横向滚动 */
  max-width: calc(100% - 180px); /* 留出 key+type+meaning 空间 */
}

.node-children {
  margin-left: 14px;
  padding-left: 8px;
  border-left: 1px solid var(--el-border-color-lighter);
}
</style>
