<script setup lang="ts">
/**
 * S03 新建 · 文件夹浏览 / S05 导入 · 文件夹浏览（交接包 FR-09 / FR-14）
 * 目录树来自真实 GET /api/projects/browse（sems 原有能力），不再是内置假目录。
 * 同时保留原 DirectoryPickerDialog（带手工路径与显示名）作为「目录选择器」入口。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ic } from '../icons'
import { store, browse, go, toast, registerProject } from '../workbenchStore'
import DirectoryPickerDialog from '../../DirectoryPickerDialog.vue'

const props = defineProps<{ mode: 'create' | 'import' }>()
const picking = ref(false)
const busy = ref(false)

const crumbs = computed(() => store.folder.crumbs)
const isCreate = computed(() => props.mode === 'create')

function crumbPath(i: number): string {
  const cwd = store.folder.cwd || '/workspace'
  const parts = cwd.replace(/\\/g, '/').split('/').filter(Boolean)
  if (/^[A-Za-z]:$/.test(parts[0] ?? '')) {
    return i === 0 ? parts[0] + '/' : parts.slice(0, i + 1).join('/')
  }
  return '/' + parts.slice(0, i + 1).join('/')
}

function enter(entry: { name: string; path: string }) {
  void browse(entry.path)
}

function up() {
  if (store.folder.parent) void browse(store.folder.parent)
}

function cwdPath() {
  return store.folder.cwd || (store.mode === 'demo' ? '/workspace' : '（驱动器根目录）')
}

function startWizard() {
  store.wizard.root = store.folder.cwd
  store.wizard.step = 0
  if (!store.wizard.name) store.wizard.name = ''
  go('/create/wizard')
}

async function importHere() {
  if (busy.value) return
  busy.value = true
  const p = await registerProject(store.folder.cwd)
  busy.value = false
  if (p) {
    toast(`已导入项目「${p.name}」`)
    go('/projects')
  }
}

async function onPick(payload: { root: string; name?: string }) {
  if (busy.value) return
  busy.value = true
  const p = await registerProject(payload.root, payload.name)
  busy.value = false
  picking.value = false
  if (p) {
    toast(`已登记项目「${p.name}」（原目录选择器）`)
    go('/projects')
  }
}

onMounted(() => { void browse() })
watch(() => props.mode, () => { void browse() })
</script>

<template>
  <div class="manager-body">
    <div class="browser">
      <div class="breadcrumb">
        <button v-for="(c, i) in crumbs" :key="c + i" class="crumb" :class="{ last: i === crumbs.length - 1 }"
                @click="browse(crumbPath(i))">{{ c }}</button>
        <template v-if="crumbs.length > 1">
          <span class="text-muted">/</span>
        </template>
      </div>
      <div class="wb-toolbar" style="justify-content:space-between;margin-bottom:12px">
        <div class="browser-path">{{ cwdPath() }}</div>
        <div class="wb-toolbar">
          <button class="btn btn-secondary" :disabled="!store.folder.parent" @click="up()">
            <span v-html="ic('back', 'sm')"></span>上一级
          </button>
          <button class="btn btn-secondary" :disabled="store.folder.loading" @click="browse(store.folder.cwd)">
            <span v-html="ic('refresh', 'sm')"></span>刷新
          </button>
          <button class="btn btn-secondary" @click="picking = true">
            <span v-html="ic('folder', 'sm')"></span>目录选择器
          </button>
        </div>
      </div>

      <div v-if="store.folder.loading" class="folder-empty"><span class="wb-spin"></span> 正在读取目录…</div>
      <div v-else-if="store.folder.error" class="folder-empty">
        目录读取失败：{{ store.folder.error }}
        <button class="btn btn-secondary" style="margin-left:8px" @click="browse(store.folder.cwd)">重试</button>
      </div>
      <div v-else-if="!store.folder.directories.length" class="folder-empty">此目录为空（可直接作为项目根目录）</div>
      <div v-else class="folder-grid">
        <button v-for="d in store.folder.directories" :key="d.path" class="folder-item" @click="enter(d)">
          <span v-html="ic('folder')"></span><span>{{ d.name }}</span>
        </button>
      </div>

      <div class="browser-actions">
        <button v-if="isCreate" class="btn btn-primary btn-lg" :disabled="busy" @click="startWizard()">
          <span v-html="ic('plus')"></span>在此新建项目
        </button>
        <button v-else class="btn btn-primary btn-lg" :disabled="busy" @click="importHere()">
          <span v-html="ic('download')"></span>导入此文件夹
        </button>
      </div>

      <p class="wb-hint">
        {{ isCreate ? '新建' : '导入' }} = 把一个已存在的目录登记为项目档案（sems 原有能力），
        不创建文件、不执行脚手架命令；新建走 2 步向导，填写的类型 / 标签 / 描述会写回真实档案。
      </p>
    </div>

    <DirectoryPickerDialog v-if="picking" :saving="busy" @close="picking = false" @pick="onPick" />
  </div>
</template>
