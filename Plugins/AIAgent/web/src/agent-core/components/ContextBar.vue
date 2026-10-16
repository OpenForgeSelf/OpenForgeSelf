<script setup>
/**
 * ContextBar —— 输入框**下方**的上下文图标行（041 §3.1）。
 *
 * 与 ContextChips 的分工（这是本批最关键的设计决定）：
 *  - ContextChips 在输入框**上方**，语义是「所见即所发的已选项摘要」，有值才出现；
 *  - ContextBar 在输入框**下方**，语义是「**入口**」，恒显示 —— 因为用户必须能点开去选。
 * 上一批只做了 chips，用户点开页面找不到入口，判为「功能没做」，根因就在这里。
 *
 * 组件仍不认识「项目 / Agent / 模型」这些 AIAgent 概念：
 * 入口项、面板内容、选中动作全部由宿主经 `items` / `panels` 注入。
 */
import { computed, ref, watch, onBeforeUnmount } from 'vue'

const props = defineProps({
  /** 入口项：{ key, icon, label, count?, active?, detail? } */
  items: { type: Array, default: () => [] },
  /**
   * 面板内容：{ [key]: { title, hint?, items:[{ id, name, desc?, active? }], empty? } }
   * 组件只负责「列出 + 勾选 + 搜索框」，不认识项目/Agent/模型这些概念。
   */
  panels: { type: Object, default: () => ({}) },
  /** 权限胶囊：{ modes:[{key,label,desc}], value, onChange } */
  access: { type: Object, default: null },
})
const emit = defineEmits(['open', 'close', 'pick', 'access-change'])

/** 当前展开的面板 key（一次只开一个，跟主流 composer 一致） */
const openKey = ref(null)
/** 各面板内的搜索词（按 key 分开，切面板不串词） */
const queries = ref({})

const activePanel = computed(() => (openKey.value ? props.panels?.[openKey.value] ?? null : null))
const query = computed(() => (openKey.value ? queries.value[openKey.value] ?? '' : ''))
/** 面板内按搜索词过滤后的条目（组件内过滤，宿主不必重复实现） */
const visibleRows = computed(() => {
  const list = activePanel.value?.items ?? []
  const q = String(query.value).trim().toLowerCase()
  if (!q) return list
  return list.filter((r) => `${r.name ?? ''} ${r.desc ?? ''}`.toLowerCase().includes(q))
})

function toggle(key) {
  if (openKey.value === key) {
    openKey.value = null
    emit('close', key)
    return
  }
  openKey.value = key
  emit('open', key)
}

function pick(row) {
  emit('pick', { key: openKey.value, id: row.id })
}

/* 搜索是组件内行为：直接使用过滤后的列表，不能依赖宿主同步组件私有 query。 */
const panelRows = visibleRows
const panelEmpty = computed(() => {
  const spec = openKey.value ? props.panels?.[openKey.value] : null
  return !panelRows.value.length ? spec?.empty || '暂无可选项' : ''
})
const panelMulti = computed(() => {
  const spec = openKey.value ? props.panels?.[openKey.value] : null
  return !!spec?.multi
})

/* 弹层翻转：下方空间不足时改为向上弹，否则窗口下半截的选项点不到。
   判定在打开时算一次（面板高度固定 300px 上限，不需要监听 resize）。 */
const flipUp = ref(false)
function decideDirection() {
  const el = rootEl.value
  if (!el) return
  const r = el.getBoundingClientRect()
  const spaceBelow = window.innerHeight - r.bottom
  flipUp.value = spaceBelow < 320
}
watch(openKey, (v) => {
  if (typeof document === 'undefined') return
  if (v) {
    decideDirection()
    document.addEventListener('pointerdown', onDocPointer, true)
  } else document.removeEventListener('pointerdown', onDocPointer, true)
})

const accessLabel = computed(() => {
  const cur = (props.access?.modes ?? []).find((m) => m.key === props.access?.value)
  return cur?.label ?? props.access?.value ?? ''
})

/** 点面板外部关闭：一次注册、随组件生命周期清理（不挂 window 而忘了 remove 是常见泄漏源）。 */
const rootEl = ref(null)
function onDocPointer(e) {
  if (!openKey.value) return
  const el = rootEl.value
  if (el && !el.contains(e.target)) {
    openKey.value = null
    emit('close', null)
  }
}
onBeforeUnmount(() => {
  if (typeof document === 'undefined') return
  document.removeEventListener('pointerdown', onDocPointer, true)
})
watch(openKey, (v) => {
  if (typeof document === 'undefined') return
  if (v) document.addEventListener('pointerdown', onDocPointer, true)
  else document.removeEventListener('pointerdown', onDocPointer, true)
})
</script>

<template>
  <div ref="rootEl" class="aiw-ctxbar" role="group" aria-label="上下文与权限">
    <!-- 入口图标行 -->
    <div class="aiw-ctxbar__row">
      <!-- 每个入口是「按钮 + 弹出面板」两兄弟：面板不能放进 <button> 里（嵌套交互元素无效 HTML，
           且面板内有 checkbox/radio，点按会被按钮的 toggle 吃掉） -->
      <span
        v-for="it in items"
        :key="it.key"
        class="aiw-ctxitem"
        :class="{ 'is-open': openKey === it.key }"
      >
        <button
          type="button"
          class="aiw-ctxbtn"
          :class="{ 'is-active': it.active }"
          :aria-pressed="it.active ? 'true' : 'false'"
          :aria-expanded="openKey === it.key ? 'true' : 'false'"
          :aria-haspopup="true"
          :title="it.detail ? `${it.label}：${it.detail}` : it.label"
          @mousedown.prevent
          @click="toggle(it.key)"
        >
          <span class="aiw-ctxbtn__ico" aria-hidden="true">{{ it.icon }}</span>
          <span class="aiw-sr-only">{{ it.label }}</span>
          <span v-if="it.count" class="aiw-ctxbtn__n">{{ it.count > 99 ? '99+' : it.count }}</span>
        </button>
        <span v-if="openKey === it.key" class="aiw-ctxpop" :class="{ 'is-up': flipUp }">
          <div class="aiw-ctxmenu">
            <div class="aiw-ctxmenu__t">{{ panels[it.key]?.title || it.label }}</div>
            <input
              v-if="(panels[it.key]?.items || []).length > 8"
              v-model="queries[it.key]"
              class="aiw-ctxmenu__q"
              type="search"
              :placeholder="`搜索${it.label}`"
              :aria-label="`搜索${it.label}`"
            />
            <div class="aiw-ctxmenu__list">
              <button
                v-for="row in panelRows"
                :key="row.id"
                type="button"
                class="aiw-ctxmenu__row"
                :class="{ 'is-on': !!row.active }"
                :aria-pressed="row.active ? 'true' : 'false'"
                role="menuitemcheckbox"
                @mousedown.prevent
                @click="pick(row)"
              >
                <span v-if="panelMulti" class="aiw-ctxmenu__box" :class="{ 'is-on': !!row.active }" aria-hidden="true">
                  <svg v-if="row.active" viewBox="0 0 24 24"><path d="M9.5 16.5 5 12l1.4-1.4 3.1 3.1 8.1-8.1L19 7l-9.5 9.5Z" fill="currentColor" /></svg>
                </span>
                <span class="aiw-ctxmenu__body">
                  <span class="aiw-ctxmenu__name">{{ row.name }}</span>
                  <span v-if="row.desc" class="aiw-ctxmenu__desc">{{ row.desc }}</span>
                </span>
                <span v-if="!panelMulti && row.active" class="aiw-ctxmenu__tick" aria-hidden="true">✓</span>
              </button>
              <p v-if="panelEmpty" class="aiw-ctxpop__empty">{{ panelEmpty }}</p>
            </div>
          </div>
        </span>
      </span>

      <!-- 权限胶囊：三档单选 -->
      <span v-if="access" class="aiw-ctxitem aiw-ctxitem--access" :class="{ 'is-open': openKey === 'access' }">
        <button
          type="button"
          class="aiw-accessbtn"
          :class="`aiw-accessbtn--${access.value}`"
          :title="`权限：${accessLabel}`"
          aria-haspopup="true"
          :aria-expanded="openKey === 'access' ? 'true' : 'false'"
          @mousedown.prevent
          @click="toggle('access')"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M12 2 4 5.3v6c0 4.6 3.3 9 8 10.7 4.7-1.7 8-6.1 8-10.7v-6L12 2Zm0 2.2 6 2.5v4.6c0 3.5-2.4 6.9-6 8.3-3.6-1.4-6-4.8-6-8.3V6.7l6-2.5Z" fill="currentColor" />
          </svg>
          <span>{{ accessLabel }}</span>
        </button>
        <span v-if="openKey === 'access'" class="aiw-ctxpop aiw-ctxpop--right" :class="{ 'is-up': flipUp }">
          <div class="aiw-ctxmenu">
            <div class="aiw-ctxmenu__t">权限模式</div>
            <button
              v-for="m in access.modes"
              :key="m.key"
              type="button"
              class="aiw-ctxmenu__row"
              :class="{ 'is-on': m.key === access.value }"
              role="menuitemradio"
              :aria-checked="m.key === access.value ? 'true' : 'false'"
              @mousedown.prevent
              @click="emit('access-change', m.key); openKey = null"
            >
              <span class="aiw-ctxmenu__name">{{ m.label }}</span>
              <span class="aiw-ctxmenu__desc">{{ m.desc }}</span>
            </button>
          </div>
        </span>
      </span>
    </div>
  </div>
</template>

<style scoped>
/* 组件样式统一走 ../styles/widget.css 的 .aiw-* 命名空间（与 MessageItem/ContextChips 同批），
   此处只保留布局骨架，视觉规则集中在 widget.css 便于宿主主题统一覆写。 */
.aiw-ctxbar {
  display: flex;
  flex-direction: column;
}
</style>
