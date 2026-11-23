<script setup lang="ts">
/**
 * GitHub 绑定对话框（绑定 UX 一轮）：包一层 ElDialog 承载 GithubBindPanel，
 * 关闭时若已完成绑定回传账号给父级。
 */
import { ref, watch } from 'vue'
import GithubBindPanel from './GithubBindPanel.vue'

const visible = defineModel<boolean>({ default: false })

const emit = defineEmits<{ (e: 'bound', accountLogin: string): void }>()

const pendingAccount = ref('')

watch(visible, (open) => {
  if (!open) {
    // 关闭即中止进行中的轮询面板：以 key 重建面板，确保下次打开是干净四态
    pendingAccount.value = ''
  }
})

function onBound(accountLogin: string): void {
  pendingAccount.value = accountLogin
  visible.value = false
  emit('bound', accountLogin)
}
</script>

<template>
  <ElDialog v-model="visible" title="绑定 GitHub 账号" width="480px" destroy-on-close data-test="github-bind-dialog">
    <GithubBindPanel :key="pendingAccount || 'fresh'" @bound="onBound" />
  </ElDialog>
</template>
