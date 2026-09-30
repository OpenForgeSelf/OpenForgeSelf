<script setup lang="ts">
/**
 * 空态/错误态/加载态统一外壳。
 *
 * 为什么单独一个组件：插件界面是远程加载的独立产物，出问题时必须说清是哪一层 ——
 * 没登录（401）／后端没起／插件未建表／项目为空。历史上最坏的一次是"转圈不停"，
 * 用户以为插件坏了，实际是宿主 token 过期。这里强制把原因和下一步写出来。
 *
 * 文案一律放在脚本里的查表，模板只做插值：多行三元写在 {{ }} 里会让 SFC 工具链
 * （vue-tsc / 编辑器高亮）解析错位，实测踩过。
 */
import { computed } from 'vue'

type PanelKind = 'loading' | 'empty' | 'error' | 'unauthorized'

const props = withDefaults(
  defineProps<{
    state?: PanelKind
    title?: string
    hint?: string
  }>(),
  { state: 'empty', title: '', hint: '' },
)

const KINDS: Record<PanelKind, { icon: string; title: string; hint: string; live: string }> = {
  loading: { icon: '◌', title: '正在读取设计系统库…', hint: '数据来自宿主后端；若长时间未完成，请确认宿主正在运行。', live: 'polite' },
  unauthorized: {
    icon: '🔒',
    title: '未登录宿主，设计系统接口拒绝访问',
    hint: '设计系统端点要求 ApiKey 鉴权。请先在宿主里登录（token 存在 localStorage 的 forge_api_token 键），再回到本页点刷新。',
    live: 'assertive',
  },
  error: {
    icon: '!',
    title: '后端返回错误',
    hint: '错误原文见下方红字；若持续失败，检查宿主是否已加载 design-system 插件、插件库表是否建好。',
    live: 'assertive',
  },
  empty: {
    icon: '·',
    title: '这里还没有内容',
    hint: '到「项目与生成」页新建或选择一个设计系统项目后，这里就会出现数据。',
    live: 'assertive',
  },
}

const kind = computed<PanelKind>(() => props.state ?? 'empty')
const icon = computed(() => KINDS[kind.value].icon)
const titleText = computed(() => props.title || KINDS[kind.value].title)
const hintText = computed(() => props.hint || KINDS[kind.value].hint)
const liveRole = computed(() => KINDS[kind.value].live)
</script>

<template>
  <div class="ps" :class="'ps--' + kind" role="status" :aria-live="liveRole">
    <div class="ps__icon" aria-hidden="true">{{ icon }}</div>
    <div class="ps__text">
      <div class="ds-small ps__title">{{ titleText }}</div>
      <div class="ds-micro ps__hint">{{ hintText }}</div>
    </div>
  </div>
</template>

<style scoped>
.ps {
  display: flex;
  align-items: flex-start;
  gap: var(--ds-space-3);
  padding: var(--ds-space-5);
  border: 1px dashed var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
}
.ps--error {
  border-color: var(--ds-danger);
  border-style: solid;
}
.ps--unauthorized {
  border-color: var(--ds-warning);
}
.ps--loading {
  border-style: solid;
}
.ps__icon {
  font-size: 18px;
  line-height: 1.2;
}
.ps__title {
  font-weight: var(--ds-fw-medium, 500);
}
.ps__hint {
  color: var(--ds-fg-3);
  margin-top: 2px;
}
</style>
