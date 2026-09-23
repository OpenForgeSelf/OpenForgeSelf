// @ts-check
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import pluginVue from 'eslint-plugin-vue'
import vueParser from 'vue-eslint-parser'
import globals from 'globals'

export default tseslint.config(
  {
    // Global ignore patterns
    ignores: [
      'dist/',
      'node_modules/',
      'coverage/',
      '*.config.*',
      '.vite/',
      '*.cjs',
      // 插件 AIAgent 模板构建产物（web/dist 的宿主内构建目录），非源码
      '.plugin-build-aiagent/',
      // Playwright 产物目录（报告、trace、测试结果），非源码
      'playwright-report/',
      'playwright-report-published/',
      'test-results/',
    ],
  },

  // Base JS/TS recommended rules
  js.configs.recommended,
  ...tseslint.configs.recommended,

  // Element Plus 组件禁止显式导入（type 导入除外）：
  // 组件样式依赖 unplugin-vue-components 按需注入，显式 import 会绕过自动解析导致组件无样式。
  // 统一在模板中使用 <ElXxx>，由 unplugin-vue-components 自动解析并注入样式。
  // 例外：ElMessage/ElMessageBox/ElNotification/ElLoading 是 API 调用型组件（JS 中 ElMessage.success()
  // 等调用，unplugin 不解析 JS 调用），必须显式导入，列入 allowImportNames 白名单。
  {
    files: ['src/**/*.{ts,tsx,vue}'],
    rules: {
      '@typescript-eslint/no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'element-plus',
              message:
                '禁止显式导入 ElXxx 组件（会绕过按需样式注入导致组件无样式）。请在模板中使用 <ElXxx> 由 unplugin-vue-components 自动解析；type 导入（如 FormInstance/FormRules）与 API 调用组件（ElMessage/ElMessageBox/ElNotification/ElLoading）除外。',
              allowTypeImports: true,
              allowImportNames: ['ElMessage', 'ElMessageBox', 'ElNotification', 'ElLoading'],
            },
          ],
        },
      ],
    },
  },

  // Vue files
  ...pluginVue.configs['flat/recommended'],
  {
    files: ['src/**/*.vue'],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tseslint.parser,
        sourceType: 'module',
      },
      globals: {
        ...globals.browser,
      },
    },
    rules: {
      'vue/multi-word-component-names': 'off',
      'vue/max-attributes-per-line': ['warn', { singleline: 4, multiline: 1 }],
      'vue/singleline-html-element-content-newline': 'off',
      'vue/html-self-closing': ['warn', { html: { void: 'always' } }],
      // 项目中 markdown 渲染、lucide 图标 SVG、正则高亮预览需要 v-html，
      // 内容均为内部生成/可信数据，关闭 XSS 检查
      'vue/no-v-html': 'off',
    },
  },

  // TypeScript files
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: {
      globals: {
        ...globals.browser,
      },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'warn',
      '@typescript-eslint/no-unused-vars': ['warn', {
        argsIgnorePattern: '^_',
        varsIgnorePattern: '^_',
      }],
      '@typescript-eslint/no-empty-object-type': 'off',
    },
  },

  // Relax rules for test files
  {
    files: ['src/**/*.test.*', 'src/__tests__/**', 'src/stores/__tests__/**', 'src/utils/__tests__/**'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-unused-vars': 'off',
    },
  },

  // Playwright E2E 测试文件（运行于 Node 环境，globals 为 Node + 浏览器 API）
  {
    files: ['e2e/**/*.ts'],
    languageOptions: {
      globals: {
        ...globals.node,
        ...globals.browser,
      },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-unused-vars': ['warn', {
        argsIgnorePattern: '^_',
        varsIgnorePattern: '^_',
      }],
    },
  },

  // Config files
  {
    files: ['*.config.*'],
    rules: {
      '@typescript-eslint/no-require-imports': 'off',
    },
  },

  // 插件共享依赖 shim（public/shared/*.js，spec 010）：
  // 这些文件**不是应用源码**，而是作为静态资源由浏览器经 import map 直接加载的
  // ESM 转发层（从 window.__FORGE_SHARED__ 具名再导出宿主真实模块）。
  // 因此运行在浏览器环境、不使用打包器，需要 browser globals 且关闭
  // 依赖打包器的 import 解析规则，否则 no-undef(window)/import 相关规则会误报。
  {
    files: ['public/shared/**/*.js'],
    languageOptions: {
      sourceType: 'module',
      globals: {
        ...globals.browser,
      },
    },
    rules: {
      'no-undef': 'off',
    },
  },

  // Node 脚本（scripts/*.mjs 等）
  {
    files: ['scripts/**/*.mjs'],
    languageOptions: {
      globals: {
        ...globals.node,
      },
    },
  },
)
