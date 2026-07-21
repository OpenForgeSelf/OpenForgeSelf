<script setup lang="ts">
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">关于</h2>
      </div>
    </div>

    <!-- 应用信息 -->
    <el-card shadow="never">
      <div class="flex flex-col items-center py-6 gap-3">
        <!-- Logo -->
        <svg
          width="48"
          height="48"
          viewBox="0 0 28 28"
          fill="none"
          aria-hidden="true"
        >
          <rect x="3" y="14" width="22" height="8" rx="2" fill="var(--el-color-primary)" opacity="0.9" />
          <rect x="6" y="8" width="16" height="8" rx="2" fill="var(--el-color-primary)" opacity="0.7" />
          <rect x="9" y="3" width="10" height="7" rx="2" fill="var(--el-color-primary)" />
          <rect x="12" y="22" width="4" height="3" rx="1" fill="var(--el-text-color-placeholder)" />
        </svg>

        <div class="text-center">
          <h3 class="text-lg font-semibold text-text m-0">铸己匣 OpenForgeSelf</h3>
          <code class="text-sm text-text-secondary">v0.1.0</code>
        </div>

        <p class="text-sm text-text-regular m-0">以器铸己，日积寸进</p>

        <!-- 链接 -->
        <div class="flex items-center gap-4 mt-2">
          <a
            href="https://github.com"
            target="_blank"
            rel="noopener noreferrer"
            class="flex items-center gap-1.5 text-sm text-primary no-underline hover:underline"
          >
            <svg
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z" />
            </svg>
            GitHub
          </a>
          <span class="text-sm text-text-secondary">MIT License</span>
        </div>
      </div>
    </el-card>
  </div>
</template>
