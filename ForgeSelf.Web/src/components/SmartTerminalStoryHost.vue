<script setup lang="ts">
/**
 * CT 画廊宿主壳：把 SmartTerminal 插件真实视图挂进宿主页面。
 * 插件目录不引入 @playwright/test；fixture 由 story props 注入，跳过网络。
 * 若 fixture.seedSessionRestore 存在：落盘前 strip password，写入 sessionStorage。
 */
import { onBeforeMount, watch } from 'vue'
import SmartTerminalView from '../../../Plugins/SmartTerminal/web/src/SmartTerminalView.vue'
import {
  SESSION_RESTORE_KEY,
  saveSnapshot,
  type SessionRestoreSnapshot,
} from '../../../Plugins/SmartTerminal/web/src/sessionRestore'
import type { SmartTerminalFixture } from '../../../Plugins/SmartTerminal/web/src/types'

const props = defineProps<{
  fixture: SmartTerminalFixture
}>()

function stripPasswordDeep(obj: unknown): unknown {
  if (obj == null || typeof obj !== 'object') return obj
  if (Array.isArray(obj)) return obj.map(stripPasswordDeep)
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
    const lower = k.toLowerCase()
    if (
      lower === 'password'
      || lower === 'pwd'
      || lower === 'passwd'
      || lower === 'ssh_password'
      || lower === 'interactivepassword'
    ) {
      continue
    }
    out[k] = stripPasswordDeep(v)
  }
  return out
}

function seedRestore(raw: Record<string, unknown> | undefined) {
  if (!raw || typeof sessionStorage === 'undefined') return
  try {
    const cleaned = stripPasswordDeep(raw) as SessionRestoreSnapshot
    // 走正式 saveSnapshot，再双检一次 keys
    if (cleaned && cleaned.version === 1 && Array.isArray(cleaned.tabs)) {
      saveSnapshot(cleaned)
    } else {
      sessionStorage.setItem(SESSION_RESTORE_KEY, JSON.stringify(cleaned))
    }
    const check = sessionStorage.getItem(SESSION_RESTORE_KEY) || ''
    if (/"password"\s*:/i.test(check) || check.includes('SuperSecretCT')) {
      console.error('[SmartTerminalStoryHost] seedSessionRestore 仍含密码字段，已拒绝落盘')
      sessionStorage.removeItem(SESSION_RESTORE_KEY)
    }
  } catch (e) {
    console.warn('[SmartTerminalStoryHost] seedSessionRestore 失败', e)
  }
}

onBeforeMount(() => {
  seedRestore(props.fixture?.seedSessionRestore)
})

watch(
  () => props.fixture?.seedSessionRestore,
  (v) => seedRestore(v),
  { deep: true },
)
</script>

<template>
  <div class="st-story-host" data-testid="smart-terminal-story-host">
    <SmartTerminalView :fixture="fixture" />
  </div>
</template>

<style scoped>
.st-story-host {
  width: 960px;
  height: 640px;
  background: var(--el-bg-color, #0f1419);
  overflow: hidden;
}
</style>
