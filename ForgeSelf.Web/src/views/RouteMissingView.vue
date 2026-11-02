<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <section class="route-missing">
    <h1>这个页面没有加载出来</h1>
    <p>
      路径 <code>{{ route.path }}</code> 没有对应的页面。
      插件页面要等清单注册之后才出现。清单拉取失败时（常见是本页还没有访问密钥），这里会一直是空的。
    </p>
    <p>从托盘重新打开主界面，或在地址栏带上一次性密钥后再进入。</p>
  </section>
</template>

<style scoped>
.route-missing {
  max-width: 36rem;
  margin: 4rem auto;
  padding: 0 1.5rem;
  color: var(--el-text-color-primary);
}

h1 {
  margin: 0 0 0.75rem;
  font-size: 1.25rem;
  font-weight: 600;
}

p {
  margin: 0 0 0.75rem;
  color: var(--el-text-color-regular);
  line-height: 1.6;
}

code {
  color: var(--el-text-color-primary);
}
</style>
