<template>
  <div class="dpick">
    <div class="dpick__mask" @click="$emit('close')"></div>
    <div class="dpick__box">
      <h3 class="dpick__title">添加项目 · 选择目录</h3>
      <p class="dpick__hint">
        选择一个已存在的项目根目录。sems 直接在插件内登记该项目，不依赖其他页面的动作。
      </p>

      <div class="dpick__bar">
        <button class="dpick__btn" :disabled="!parent || loading" title="返回上级目录" @click="go(parent)">↑ 上级</button>
        <input
          v-model.trim="pathDraft"
          class="dpick__input dpick__input--grow"
          type="text"
          placeholder="绝对路径，如 D:\src\my-proj\Demo"
          @keydown.enter.prevent="go(pathDraft)"
        />
        <button class="dpick__btn" :disabled="loading" @click="go(pathDraft)">{{ loading ? '读取中…' : '进入' }}</button>
      </div>

      <div class="dpick__list" :class="{ 'dpick__list--empty': !directories.length && !error && !loading }">
        <p v-if="loading" class="dpick__empty">读取目录中…</p>
        <p v-else-if="error" class="dpick__err">{{ error }}</p>
        <p v-else-if="!directories.length" class="dpick__empty">该目录下没有子目录，可直接「选择此目录」。</p>
        <button
          v-for="d in directories"
          v-else
          :key="d.path"
          class="dpick__row"
          :class="{ 'dpick__row--current': d.path === current }"
          :title="d.path"
          @click="go(d.path)"
        >
          <span class="dpick__folder">▸</span>
          <span class="dpick__name">{{ d.name }}</span>
        </button>
      </div>

      <label class="dpick__field">
        <span>项目名（可选，留空取目录名）</span>
        <input v-model.trim="name" class="dpick__input dpick__input--grow" type="text" :placeholder="suggestedName" />
      </label>

      <p class="dpick__current" :title="current || ''">
        将登记为项目：<b>{{ current || '（尚未选择目录）' }}</b>
      </p>

      <div class="dpick__foot">
        <button class="dpick__btn" @click="$emit('close')">取消</button>
        <button class="dpick__btn dpick__btn--primary" :disabled="!current || saving" @click="pick">
          {{ saving ? '登记中…' : '选择此目录' }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 目录浏览 + 手工输入路径的选择弹层（sems 自带，不依赖其他插件的端点）。
 * 数据源 = GET /api/projects/browse（只列子目录，不列文件、不递归）；
 * 省略 path 时后端返回本机驱动器，故首屏即可从盘符开始逐级进入。
 * 「进入」某目录即把 current 推到该绝对路径；「选择此目录」把 current + 可选项目名 emit 给父层登记。
 */
import { computed, onMounted, ref } from 'vue'
import { browseDirectories } from './http'
import type { DirectoryEntry } from './types'

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'pick', payload: { root: string; name?: string }): void
}>()

defineProps<{ saving?: boolean }>()

const current = ref('')
const pathDraft = ref('')
const parent = ref<string | null>(null)
const directories = ref<DirectoryEntry[]>([])
const loading = ref(false)
const error = ref('')
const name = ref('')

const suggestedName = computed(() => {
  if (!current.value) return '留空即目录名'
  const segs = current.value.replace(/[\\/]+$/, '').split(/[\\/]/)
  return segs[segs.length - 1] || current.value
})

async function go(path: string | null) {
  if (loading.value) return
  const target = (path ?? '').trim().replace(/^"|"$/g, '')
  loading.value = true
  error.value = ''
  try {
    const resp = await browseDirectories(target || undefined)
    directories.value = resp.directories ?? []
    parent.value = resp.parent ?? null
    // 列举驱动器时没有「当前目录」概念，保持原值供选择
    if (resp.path) {
      current.value = resp.path
      pathDraft.value = resp.path
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

function pick() {
  if (!current.value) return
  emit('pick', { root: current.value, name: name.value || undefined })
}

onMounted(() => {
  void go('')
})
</script>

<style scoped>
.dpick {
  position: fixed;
  inset: 0;
  z-index: 2600;
  display: flex;
  align-items: center;
  justify-content: center;
}

.dpick__mask {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
}

.dpick__box {
  position: relative;
  width: min(620px, calc(100vw - 32px));
  max-height: calc(100vh - 64px);
  display: flex;
  flex-direction: column;
  padding: 18px 20px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 10px;
  background: var(--el-bg-color-overlay, #1d1e1f);
  box-shadow: 0 18px 48px rgba(0, 0, 0, 0.45);
}

.dpick__title {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
  color: var(--el-text-color-primary, #e5eaf3);
}

.dpick__hint {
  margin: 6px 0 12px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.dpick__bar {
  display: flex;
  align-items: center;
  gap: 8px;
}

.dpick__list {
  flex: 1;
  min-height: 150px;
  max-height: 280px;
  margin: 10px 0;
  padding: 4px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color-lighter, #3a3b3c);
  border-radius: 6px;
  background: var(--el-fill-color-light, #222323);
}

.dpick__empty {
  margin: 24px 12px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.dpick__err {
  margin: 10px 0;
  font-size: 12px;
  color: var(--el-color-danger, #f56c6c);
}

.dpick__row {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 10px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: 13px;
  text-align: left;
  cursor: pointer;
}

.dpick__row:hover {
  background: var(--el-fill-color, #262727);
  color: var(--el-color-primary, #ffb84d);
}

.dpick__row--current {
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-fill-color, #262727);
}

.dpick__folder {
  flex-shrink: 0;
}

.dpick__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dpick__field {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.dpick__input {
  height: 30px;
  min-width: 0;
  padding: 0 10px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: var(--el-fill-color-blank, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: 13px;
  font-family: var(--el-font-family-mono, monospace);
}

.dpick__input--grow {
  flex: 1;
}

.dpick__current {
  margin: 10px 0 0;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dpick__foot {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 14px;
}

.dpick__btn {
  height: 30px;
  padding: 0 14px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: 13px;
  cursor: pointer;
}

.dpick__btn:hover:not(:disabled) {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.dpick__btn--primary {
  border-color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary-dark, #1d1e1f);
  font-weight: 600;
}

.dpick__btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
</style>
