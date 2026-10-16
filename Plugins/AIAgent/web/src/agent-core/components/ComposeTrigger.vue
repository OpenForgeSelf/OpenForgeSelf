<script setup>
/**
 * ComposeTrigger —— 输入区「行内触发面板」骨架（/ 命令、@ 提及共用）。
 *
 * 组件只负责**展示与键盘高亮**，不含任何业务数据源：
 *  - 候选分组由宿主经 `groups` 传入（组件只在面板首次打开时取一次，之后内存过滤）；
 *  - 选中后插入什么文本由宿主的 `onPick` 决定（返回 '' = 不插文本，只挂上下文）。
 * 这样组件拿去给别的宿主用时，不会拖着 AIAgent 的工具/脚本/项目概念走。
 *
 * 键盘由**父组件的 textarea**驱动（本组件只暴露 move/current）：
 * 焦点始终留在输入框里，否则弹层一抢焦点，用户打的字就断了 —— 这是编辑器型
 * 触发面板（IDE 补全、Notion 斜杠菜单）的通行做法。
 */
import { computed, ref, watch, nextTick } from 'vue'

const props = defineProps({
  /** 触发字符（'/' 或 '@'），仅用于展示与无障碍标签。 */
  char: { type: String, required: true },
  /** 候选分组：{ key, title, items: [{ id, name, desc?, meta? }] } */
  groups: { type: Array, default: () => [] },
  /** 当前过滤词（触发字符之后、光标之前的文本）。 */
  query: { type: String, default: '' },
  /** 面板标题（空则不显示头部）。 */
  title: { type: String, default: '' },
})
const emit = defineEmits(['pick', 'close'])
defineExpose({ move, current })

/* 展平后的候选（跨分组连续编号，键盘才能一路往下走）。
   ★ 必须保留**原对象引用**：下面 groupsShown 与模板里的 indexOf 都靠引用同一性找位置，
   一旦这里 `{...it}` 复制一份，高亮索引就会永远算成 -1（键盘看着不动）。 */
const flat = computed(() => {
  const out = []
  props.groups.forEach((g) => {
    ;(g.items || []).forEach((it) => out.push(it))
  })
  return out
})

/** 内存过滤：名称 / 描述 / meta 任一命中即可；空 query 全过。 */
const filtered = computed(() => {
  const q = props.query.trim().toLowerCase()
  if (!q) return flat.value
  return flat.value.filter((it) =>
    [it.name, it.desc, it.meta].some((v) => String(v ?? '').toLowerCase().includes(q))
  )
})

/** 按过滤结果重新分组，空组不渲染（否则会留一堆空标题）。
    同样用引用同一性过滤，与 flat 保持一致。 */
const groupsShown = computed(() => {
  const kept = new Set(filtered.value)
  return props.groups
    .map((g) => ({ ...g, items: (g.items || []).filter((it) => kept.has(it)) }))
    .filter((g) => g.items.length > 0)
})

const index = ref(0)
const listEl = ref(null)

/** 过滤词变化时回到第一条：否则「打了两个字还停在旧的第 7 条」会选错。 */
watch(() => props.query, () => { index.value = 0 })

function move(delta) {
  const n = filtered.value.length
  if (!n) return
  index.value = (index.value + delta + n) % n
  nextTick(scrollActiveIntoView)
}

function current() {
  return filtered.value[index.value] ?? null
}

/** 高亮项要始终可见：长列表里键盘走到底部，视觉上必须跟着滚。 */
function scrollActiveIntoView() {
  const el = listEl.value
  if (!el) return
  const node = el.querySelector('.aiw-trg__opt.is-on')
  if (!node) return
  const top = node.offsetTop
  const bottom = top + node.offsetHeight
  if (top < el.scrollTop) el.scrollTop = top - 4
  else if (bottom > el.scrollTop + el.clientHeight) el.scrollTop = bottom - el.clientHeight + 4
}

function pick(item) {
  emit('pick', item)
}
</script>

<template>
  <div class="aiw-trg" role="presentation">
    <div v-if="title" class="aiw-trg__head">
      <span class="aiw-trg__title">{{ title }}</span>
      <span class="aiw-trg__hint">↑↓ 选择 · Enter 确认 · Esc 关闭</span>
    </div>
    <div ref="listEl" class="aiw-trg__list" role="listbox" :aria-label="title || '候选项'">
      <template v-if="groupsShown.length">
        <div v-for="g in groupsShown" :key="g.key" class="aiw-trg__grp">
          <div class="aiw-trg__grp-t">{{ g.title }}</div>
          <button
            v-for="it in g.items"
            :key="it.id"
            type="button"
            role="option"
            class="aiw-trg__opt"
            :class="{ 'is-on': filtered.indexOf(it) === index }"
            :aria-selected="filtered.indexOf(it) === index ? 'true' : 'false'"
            @click="pick(it)"
            @mouseenter="index = filtered.indexOf(it)"
          >
            <span class="aiw-trg__opt-name">{{ it.name }}</span>
            <span v-if="it.meta" class="aiw-trg__opt-meta">{{ it.meta }}</span>
            <span v-if="it.desc" class="aiw-trg__opt-desc">{{ it.desc }}</span>
          </button>
        </div>
      </template>
      <p v-else class="aiw-trg__empty">无匹配项</p>
    </div>
  </div>
</template>
