<template>
  <!-- 合法对照样例：全部走允许路径，不产生违规（verify 断言总数恰 32，即隐含零误伤）。 -->
  <div class="legit-root">fixture only</div>
</template>

<style>
/* --el- 真实 token：放行 */
.legit-root { color: var(--el-text-color-primary); background: var(--el-bg-color); }
/* transparent / currentColor：允许关键字 */
.legit-mid { border-color: transparent; outline-color: currentColor; }
/* 白名单 token（prefix --dur- 与独立 --border-subtle）：放行 */
.legit-anim { animation: fade var(--dur-fast) var(--ease-out); border: 1px solid var(--border-subtle); }
/* var() 硬编码兜底也被查——这里用 token 兜底，不违规 */
.legit-fallback { color: var(--el-color-primary, var(--el-text-color-regular)); }
</style>
