<script setup lang="ts">
// 铸己匣品牌 logo 复用组件（从宿主 components/AppLogo.vue 迁入插件自带界面）。
// ⚠ 必须用 :src 绑定运行时字符串：写成静态 `src="/logo/..."` 会被 Vite 当资源解析，
// 插件构建直接失败（plugin-development §3.2 迁移改写表实证）。
// 宿主 public/logo 与 wwwroot/logo 在运行时按绝对路径可达，插件与宿主同源，故可用。
const logoUrl = '/logo/logo-128.png'

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
    <img :src="logoUrl" alt="铸己匣" />
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
