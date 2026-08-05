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
      // Playwright 产物目录（报告、trace、测试结果），非源码
      'playwright-report/',
      'playwright-report-published/',
      'test-results/',
    ],
  },

  // Base JS/TS recommended rules
  js.configs.recommended,
  ...tseslint.configs.recommended,

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
