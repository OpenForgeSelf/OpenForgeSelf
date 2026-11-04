<script setup lang="ts">
/**
 * CT 画廊宿主壳：把 DbAdmin 插件真实视图挂进宿主页面。
 * fixture 由 story props 注入，跳过网络；fixture.seedRestore 存在时先剥密码再写 localStorage（S-R 恢复后态）。
 */
import { onBeforeMount } from 'vue'
import DbAdminView from '../../../Plugins/DbAdmin/web/src/DbAdminView.vue'
import { RESTORE_KEY, saveSnapshot, stripSecrets, type RestoreSnapshot } from '../../../Plugins/DbAdmin/web/src/restore'
import type { DbAdminFixture } from '../../../Plugins/DbAdmin/web/src/types'

const props = defineProps<{ fixture: DbAdminFixture }>()

onBeforeMount(() => {
  if (typeof localStorage === 'undefined') return
  // 每个故事独立：先清掉上一个故事留下的恢复快照，避免串味
  localStorage.removeItem(RESTORE_KEY)
  const raw = props.fixture?.seedRestore
  if (!raw) return
  const ok = saveSnapshot(stripSecrets(raw) as unknown as RestoreSnapshot)
  const check = localStorage.getItem(RESTORE_KEY) ?? ''
  if (!ok || /"(password|connStr)"\s*:/i.test(check)) {
    console.error('[DbAdminStoryHost] seedRestore 落盘失败或仍含密码字段，已拒绝')
    localStorage.removeItem(RESTORE_KEY)
  }
})
</script>

<template>
  <div class="dba-story-host" data-testid="db-admin-story-host">
    <DbAdminView :fixture="fixture" />
  </div>
</template>

<style scoped>
.dba-story-host {
  width: 980px;
  height: 680px;
  background: var(--el-bg-color, #0f1419);
  overflow: hidden;
}
</style>
