<script setup lang="ts">
/** 控制台 · Config 配置。 */
import DsInput from '../../components/DsInput.vue'
import DsButton from '../../components/DsButton.vue'
import Icon from '../../components/Icon.vue'

const items = [
  { key: 'service.name', value: 'api-gateway', desc: '服务注册名' },
  { key: 'service.replicas', value: '3', desc: '副本数' },
  { key: 'service.region', value: 'cn-east-1', desc: '部署区域' },
  { key: 'observability.tracing', value: 'on', desc: '链路追踪开关' },
]
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div><h2 class="ds-h2">配置 Config</h2><div class="ds-small">服务运行参数</div></div>

    <div class="ds-surface ds-stack ds-gap-4" style="padding: var(--ds-space-6)">
      <div v-for="it in items" :key="it.key" class="ds-stack ds-gap-2">
        <span class="ds-micro ds-mono">{{ it.key }}</span>
        <DsInput :model-value="it.value" :hint="it.desc" />
      </div>
      <div class="ds-row ds-gap-3">
        <DsButton icon="check">保存配置</DsButton>
        <DsButton variant="secondary">重置</DsButton>
      </div>
    </div>

    <div class="ds-surface ds-row ds-gap-3" style="padding: var(--ds-space-4)">
      <Icon name="settings" :size="18" />
      <span class="ds-small">配置项校验通过设计系统 Input 组件（focus 青色描边 token）。</span>
    </div>
  </div>
</template>
