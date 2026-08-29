<script setup lang="ts">
// 铸己匣品牌 logo 复用组件。
// 图片取自 public/logo/logo-128.png（由源图生成的多尺寸之一），
// 通过 public 路径引用，dev 与构建后（wwwroot/logo）均可直接访问。
withDefaults(
  defineProps<{
    /** 显示尺寸（正方形，单位 px） */
    size?: number
  }>(),
  { size: 28 }
)
</script>

<template>
  <span class="app-logo" :style="{ width: size + 'px', height: size + 'px' }">
    <img src="/logo/logo-128.png" alt="铸己匣" />
  </span>
</template>

<style scoped>
.app-logo {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
  border-radius: 6px;
  line-height: 0;
}

.app-logo img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
}
</style>
