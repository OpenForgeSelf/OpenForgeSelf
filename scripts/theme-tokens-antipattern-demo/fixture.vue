<template>
  <!-- 反模式 fixture：check:theme:demo 的 32 处违规样本（AC2：4 未知 el + 16 自定义 + 12 硬编码）。
       本文件仅供检查器回归，不参与构建；禁止在业务代码中模仿。 -->
  <div class="antipattern-root">fixture only</div>
</template>

<script setup lang="ts">
// 未知 Element token（4）：--el-color-fantasy / --el-bg-mystic / --el-space-giant / --el-radius-huge
// 未授权自定义 token（16）：--bad-token-01..16（定义行即违规）
// 硬编码颜色（12，仅 <style> 块内查）：hex×4 + rgb(a)×4 + 命名色×4
</script>

<style>
/* ── 未知 Element token（4） ── */
.a1 { color: var(--el-color-fantasy); }
.a2 { background: var(--el-bg-mystic); }
.a3 { gap: var(--el-space-giant); }
.a4 { border-radius: var(--el-radius-huge); }

/* ── 未授权自定义 token（16） ── */
.b01 { --bad-token-01: 2px; }
.b02 { --bad-token-02: 4px; }
.b03 { --bad-token-03: 8px; }
.b04 { --bad-token-04: center; }
.b05 { --bad-token-05: 12px; }
.b06 { --bad-token-06: pointer; }
.b07 { --bad-token-07: hidden; }
.b08 { --bad-token-08: solid; }
.b09 { --bad-token-09: 90%; }
.b10 { --bad-token-10: 600; }
.b11 { --bad-token-11: uppercase; }
.b12 { --bad-token-12: 16px; }
.b13 { --bad-token-13: nowrap; }
.b14 { --bad-token-14: 100vh; }
.b15 { --bad-token-15: flex; }
.b16 { --bad-token-16: absolute; }

/* ── 硬编码颜色（12） ── */
.c01 { color: #FF0000; }
.c02 { border-color: #00ff0080; }
.c03 { outline-color: #00f; }
.c04 { background: #abcdEf; }
.c05 { box-shadow: 0 0 0 rgb(1, 2, 3); }
.c06 { color: rgba(4, 5, 6, 0.5); }
.c07 { border: 1px solid rgb( 9, 8, 7 ); }
.c08 { background: rgba( 10, 11, 12, 0.25 ); }
.c09 { color: red; }
.c10 { border-color: blue; }
.c11 { background: teal; }
.c12 { outline: 1px solid gold; }
</style>
