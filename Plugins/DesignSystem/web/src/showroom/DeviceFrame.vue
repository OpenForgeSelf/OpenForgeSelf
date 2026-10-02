<script setup lang="ts">
/**
 * 设备框（FR11）：只表达"这段界面在多大屏上"。
 *
 * 为什么是纯 CSS 外框而不画一条假系统栏：假栏会让人以为这是"手机预览"，而它只是宽度约束；
 * 三档宽度取自 `scenes.ts` 的 `DEVICES`（唯一真源），组件里不许再写一份数字。
 * 框宽按真实像素给（不 `max-width` 压扁），超宽由舞台的横向滚动承接（§FR9 窄屏规则）。
 */
import { computed } from 'vue'
import { deviceById, type DeviceId } from './scenes'

const props = defineProps<{ device: DeviceId }>()

/** 该档的框宽（px）；找不到档位时回落桌面宽度（不编一个数） */
const width = computed(() => deviceById(props.device)?.width ?? deviceById('desktop')!.width)
</script>

<template>
  <div class="ds-frame" data-stage-frame :data-device="device" :style="{ width: `${width}px` }">
    <slot />
  </div>
</template>

<style scoped>
.ds-frame {
  display: block;
  flex: none;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  overflow: hidden;
  background: var(--ds-surface-1);
}
</style>