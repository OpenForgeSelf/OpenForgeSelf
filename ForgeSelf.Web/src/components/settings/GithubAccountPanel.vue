<script setup lang="ts">
/**
 * 设置页 GitHub 账号面板（PILOT-083 + 绑定 UX 二轮）。
 * 三态：未绑定（绑定入口）/ 已绑定（账号信息 + 解绑 + 自动附凭据开关）/ 加载中。
 * 解绑二次确认（不可逆动作）；开关即改即存，失败回滚并提示。
 */
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import GithubBindPanel from '@/components/github/GithubBindPanel.vue'
import { githubAccountApi } from '@/services/githubAccountApi'
import type { GitHubBindingStatus } from '@/types/githubAccount'

const status = ref<GitHubBindingStatus | null>(null)
const loading = ref(false)
const toggling = ref(false)

async function loadStatus(): Promise<void> {
  loading.value = true
  try {
    const res = await githubAccountApi.status()
    if (res.success && res.data) {
      status.value = res.data
    } else {
      ElMessage.error(res.error || '获取绑定状态失败')
    }
  } catch {
    // 服务不可达/无令牌环境：回落未绑定态，不让挂载钩子产生未处理拒绝
    status.value = null
  } finally {
    loading.value = false
  }
}

onMounted(loadStatus)

function onBound(accountLogin: string): void {
  if (status.value) {
    status.value = { ...status.value, bound: true, accountLogin }
  } else {
    void loadStatus()
  }
}

/** 解绑：二次确认（铁律：不可逆动作必确认），成功后回未绑定态 */
async function onUnbind(): Promise<void> {
  try {
    await ElMessageBox.confirm('解绑后，插件市场安装与 GitHub 同步将不再携带你的凭据。确定解绑？', '解绑 GitHub 账号', {
      confirmButtonText: '解绑',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  loading.value = true
  const res = await githubAccountApi.unbind()
  loading.value = false
  if (res.success && res.data) {
    status.value = res.data
    ElMessage.success('已解绑 GitHub 账号')
  } else {
    ElMessage.error(res.error || '解绑失败')
  }
}

/** 自动附凭据开关：即改即存，失败回滚 */
async function onAutoAttachChange(enabled: boolean): Promise<void> {
  if (!status.value || toggling.value) return
  const previous = status.value.autoAttachToken
  toggling.value = true
  const res = await githubAccountApi.setAutoAttach(enabled)
  toggling.value = false
  if (res.success && res.data) {
    status.value = res.data
    ElMessage.success(enabled ? '已开启：插件 GitHub 拉取自动附凭据' : '已关闭：插件 GitHub 拉取不再附凭据')
  } else {
    status.value = { ...status.value, autoAttachToken: previous }
    ElMessage.error(res.error || '设置失败')
  }
}
</script>

<template>
  <div v-loading="loading" class="github-account-panel" data-test="github-account-panel">
    <h3 class="panel-title">GitHub 账号</h3>

    <!-- 未绑定 -->
    <template v-if="!status || !status.bound">
      <p class="panel-desc" data-test="unbound-desc">尚未绑定 GitHub 账号。绑定后可用你自己的凭据安装私有仓库插件。</p>
      <GithubBindPanel @bound="onBound" />
    </template>

    <!-- 已绑定 -->
    <template v-else>
      <div class="bound-row" data-test="bound-row">
        <ElTag type="success" data-test="bound-tag">已绑定</ElTag>
        <span class="account-login" data-test="account-login">{{ status.accountLogin }}</span>
        <span v-if="status.scopes" class="account-scopes" data-test="account-scopes">授权范围：{{ status.scopes }}</span>
        <ElButton size="small" type="danger" plain data-test="unbind-button" @click="onUnbind">解绑</ElButton>
      </div>
      <div class="attach-row" data-test="auto-attach-row">
        <span>插件 GitHub 拉取自动附凭据</span>
        <ElSwitch :model-value="status.autoAttachToken" :loading="toggling" data-test="auto-attach-switch"
          @change="onAutoAttachChange" />
      </div>
    </template>
  </div>
</template>

<style scoped>
.github-account-panel {
  padding: var(--el-padding, 16px) 0;
}

.panel-title {
  margin: 0 0 var(--el-margin, 8px);
  font-size: var(--el-font-size-medium);
  color: var(--el-text-color-primary);
}

.panel-desc {
  font-size: var(--el-font-size-small);
  color: var(--el-text-color-secondary);
  margin-bottom: var(--el-margin, 12px);
}

.bound-row {
  display: flex;
  align-items: center;
  gap: var(--el-margin, 12px);
  margin-bottom: var(--el-margin, 12px);
}

.account-login {
  font-weight: var(--el-font-weight-bold, 600);
  color: var(--el-text-color-primary);
}

.account-scopes {
  font-size: var(--el-font-size-small);
  color: var(--el-text-color-secondary);
}

.attach-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: var(--el-font-size-small);
  color: var(--el-text-color-regular);
}
</style>
