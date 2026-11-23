#!/usr/bin/env node
/**
 * migrate-hardcoded-colors.mjs — 硬编码色值 → 语义 token 机械迁移器
 *
 * 配套 check-theme-tokens.mjs R3（硬编码颜色）。规则真源 = 本文件 RULES 常量：
 * 每条规则 = { prop: <属性分组正则>, value: <整值精确匹配或色值字面量>, to: <替换文本>, kind: 'whole'|'color' }
 *   - whole: 整个声明值精确匹配后整体替换（box-shadow / gradient / 特殊遮罩）
 *   - color: 在声明值内查找色值字面量并原位替换（保留 `1px solid` 等结构）
 *
 * 另内置两类通用规则（不进 RULES）：
 *   - fallback-strip: var(--el-X, #hex) → var(--el-X)（themes 供给齐备，兜底即幻影）
 *   - overlay 遮罩: rgba(11, 9, 8, var(--overlay-opacity, ...)) → color-mix(var(--ink-950), …)
 *
 * 用法：
 *   node scripts/migrate-hardcoded-colors.mjs --dry-run [--scope <prefix>] [--exempt-file <path>]...
 *   node scripts/migrate-hardcoded-colors.mjs --apply   [--scope <prefix>] [--exempt-file <path>]...
 *
 * 设计决策（docs/ai/pilot/2026-11-23-theme-token-unify/02-spec.md）：
 *   语义映射到 --el-* 主题 token；不再新增自造色 token；展示型工具色板走文件级豁免
 *   （functionalColorExemptions：ColorTool/QrCodeTool/RegexTester/TimestampTool/JwtTool）。
 */
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const APPLY = args.includes('--apply');
const scopeIdx = args.indexOf('--scope');
const SCOPE = scopeIdx >= 0 ? args[scopeIdx + 1] : '';
const exemptFiles = [];
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--exempt-file') exemptFiles.push(path.resolve(args[i + 1]));
}

const ROOT = process.cwd();
const SKIP_DIRS = /(^|[\\/])(node_modules|dist|\.temp|themes|fixtures|coverage|screenshots)([\\/]|$)/;
const SKIP_FILES = /(\.spec\.|\.test\.|\.story\.|\.d\.ts$)/;
/** 展示型工具：功能数据色板，评审豁免（token-migration-map.json functionalColorExemptions 同源） */
const FILE_EXEMPT = [
  'ForgeSelf.Web/src/components/devtools/ColorTool.vue',
  'ForgeSelf.Web/src/components/devtools/QrCodeTool.vue',
  'ForgeSelf.Web/src/components/devtools/RegexTester.vue',
  'ForgeSelf.Web/src/components/devtools/TimestampTool.vue',
  'ForgeSelf.Web/src/components/devtools/JwtTool.vue',
];

/* ---------------- 规则表 ---------------- */
// 属性分组
const G_TEXT = /^(color|caret-color|accent-color|fill|stroke|text-decoration-color|column-rule-color|-webkit-text-fill-color|iconColor)$/;
const G_BG = /^(background|background-color|background-image|bgColor)$/;
const G_BORDER = /^(border|border-top|border-right|border-bottom|border-left|border-color|border-top-color|border-right-color|border-bottom-color|border-left-color|outline|outline-color|column-rule)$/;
const G_SHADOW = /^(box-shadow|text-shadow)$/;

/** (分组, 色值字面量) → 替换。key = `组|小写色值` */
const COLOR_RULES = {
  // —— 文字色（Bootstrap 灰阶 → el 文字阶）
  'text|#212529': 'var(--el-text-color-primary)',
  'text|#1f2937': 'var(--el-text-color-primary)',
  'text|#111827': 'var(--el-text-color-primary)',
  'text|#343a40': 'var(--el-text-color-primary)',
  'text|#495057': 'var(--el-text-color-regular)',
  'text|#6b7280': 'var(--el-text-color-regular)',
  'text|#4b5563': 'var(--el-text-color-regular)',
  'text|#6c757d': 'var(--el-text-color-secondary)',
  'text|#8b93a3': 'var(--el-text-color-secondary)',
  'text|#9aa1ae': 'var(--el-text-color-secondary)',
  'text|#888': 'var(--el-text-color-secondary)',
  'text|#adb5bd': 'var(--el-text-color-placeholder)',
  'text|#9ca3af': 'var(--el-text-color-placeholder)',
  'text|#ced4da': 'var(--el-text-color-disabled)',
  'text|#d4d4d4': 'var(--el-text-color-primary)',
  'text|#e0e0e0': 'var(--el-text-color-primary)',
  'text|#fff': 'var(--el-color-white)',
  'text|#ffffff': 'var(--el-color-white)',
  'text|white': 'var(--el-color-white)',
  'text|#000': 'var(--el-color-black)',
  'text|black': 'var(--el-color-black)',
  'text|#333': 'var(--el-text-color-primary)',
  'text|#b0b0b0': 'var(--el-text-color-secondary)',
  'text|#383d41': 'var(--el-text-color-regular)',
  'text|#004085': 'var(--el-color-info-dark-2)',
  'text|#ffc107': 'var(--el-color-warning)',
  'text|#d32f2f': 'var(--el-color-error)',
  'text|#a78bfa': 'var(--el-color-info)',
  'text|#2dd4bf': 'var(--el-color-success)',
  'text|#9c27b0': 'var(--el-color-info)',
  'text|#ff9800': 'var(--el-color-warning)',
  'text|#4caf50': 'var(--el-color-success)',
  'text|#f44336': 'var(--el-color-error)',
  'text|#4fc3f7': 'var(--el-color-info)',
  'text|#ce93d8': 'var(--el-color-info)',
  'text|#6a9955': 'var(--el-color-success)',
  'text|#569cd6': 'var(--el-color-info-dark-2)',
  'text|#f44747': 'var(--el-color-error)',
  'text|#22543d': 'var(--el-color-success-dark-2)',
  'text|#e2e8f0': 'var(--el-color-white)',
  'text|#6366f1': 'var(--el-color-info)',
  'text|#8b5cf6': 'var(--el-color-info)',
  'text|#ffb74d': 'var(--el-color-warning)',
  'text|#ffe0b2': 'var(--el-color-warning-light-8)',
  'text|#ef5350': 'var(--el-color-error)',
  'text|#4ec9b0': 'var(--el-color-success)',
  'text|#666': 'var(--el-text-color-secondary)',
  'text|#2196f3': 'var(--el-color-primary)',
  'text|#0369a1': 'var(--el-color-info-dark-2)',
  'text|#17a2b8': 'var(--el-color-info)',
  'text|#f1f3f5': 'var(--el-fill-color)',
  'text|#ec4899': 'var(--el-color-error)',
  'text|#14b8a6': 'var(--el-color-success)',
  'text|#4f46e5': 'var(--el-color-info)',
  'text|#ffcdd2': 'var(--el-color-error)',
  'text|#90a4ae': 'var(--el-text-color-disabled)',
  'text|#b0bec5': 'var(--el-text-color-disabled)',
  'text|#0ea5e9': 'var(--el-color-info)',
  'text|#a855f7': 'var(--el-color-info)',
  'text|#84cc16': 'var(--el-color-success)',
  'text|#64748b': 'var(--el-text-color-secondary)',
  'text|#f43f5e': 'var(--el-color-error)',
  'text|#f97316': 'var(--el-color-warning)',
  // —— 文字色（品牌/状态）
  'text|#0d6efd': 'var(--el-color-primary)',
  'text|#007bff': 'var(--el-color-primary)',
  'text|#1976d2': 'var(--el-color-primary)',
  'text|#1565c0': 'var(--el-color-primary-dark-2)',
  'text|#3b82f6': 'var(--el-color-primary)',
  'text|#2563eb': 'var(--el-color-primary)',
  'text|#f59e0b': 'var(--el-color-primary)',
  'text|#d97706': 'var(--el-color-primary-dark-2)',
  'text|#b45309': 'var(--el-color-primary)',
  'text|#7a4a00': 'var(--el-color-warning-dark-2)',
  'text|#856404': 'var(--el-color-warning-dark-2)',
  'text|#dc3545': 'var(--el-color-error)',
  'text|#ef4444': 'var(--el-color-error)',
  'text|#dc2626': 'var(--el-color-error)',
  'text|#b42318': 'var(--el-color-error)',
  'text|#c0392b': 'var(--el-color-error)',
  'text|#721c24': 'var(--el-color-error-dark-2)',
  'text|#742a2a': 'var(--el-color-error-dark-2)',
  'text|#28a745': 'var(--el-color-success)',
  'text|#218838': 'var(--el-color-success)',
  'text|#198754': 'var(--el-color-success)',
  'text|#10b981': 'var(--el-color-success)',
  'text|#059669': 'var(--el-color-success)',
  'text|#16a34a': 'var(--el-color-success)',
  'text|#155724': 'var(--el-color-success-dark-2)',
  'text|#0c5460': 'var(--el-color-info-dark-2)',
  'text|#0b7285': 'var(--el-color-info-dark-2)',
  'text|#6c5b7b': 'var(--el-text-color-secondary)',
  // —— 背景（白/纸面/灰阶）
  'bg|#fff': 'var(--el-bg-color)',
  'bg|white': 'var(--el-bg-color)',
  'bg|#ffffff': 'var(--el-bg-color)',
  'bg|#f8f9fa': 'var(--el-fill-color-light)',
  'bg|#f9fafb': 'var(--el-fill-color-light)',
  'bg|#f3f4f6': 'var(--el-fill-color)',
  'bg|#f1f3f5': 'var(--el-fill-color)',
  'bg|#e9ecef': 'var(--el-fill-color)',
  'bg|#e5e7eb': 'var(--el-fill-color)',
  'bg|#dee2e6': 'var(--el-fill-color)',
  'bg|#e2e8f0': 'var(--el-fill-color)',
  // —— 背景（状态浅底 = EP light-8/9）
  'bg|#f8d7da': 'var(--el-color-error-light-9)',
  'bg|#fdecea': 'var(--el-color-error-light-9)',
  'bg|#fef2f2': 'var(--el-color-error-light-9)',
  'bg|#fff5f5': 'var(--el-color-error-light-9)',
  'bg|#fecaca': 'var(--el-color-error-light-8)',
  'bg|#fca5a5': 'var(--el-color-error-light-7)',
  'bg|#fde8e8': 'var(--el-color-error-light-8)',
  'bg|#d4edda': 'var(--el-color-success-light-9)',
  'bg|#d1fae5': 'var(--el-color-success-light-9)',
  'bg|#dcfce7': 'var(--el-color-success-light-9)',
  'bg|#e8f5e9': 'var(--el-color-success-light-9)',
  'bg|#c8e6c9': 'var(--el-color-success-light-8)',
  'bg|#fff3cd': 'var(--el-color-warning-light-9)',
  'bg|#fef3c7': 'var(--el-color-warning-light-9)',
  'bg|#fef9c3': 'var(--el-color-warning-light-9)',
  'bg|#fff8e1': 'var(--el-color-warning-light-9)',
  'bg|#ffecb3': 'var(--el-color-warning-light-8)',
  'bg|#d1ecf1': 'var(--el-color-info-light-9)',
  'bg|#e0f2fe': 'var(--el-color-info-light-9)',
  'bg|#f0f7ff': 'var(--el-color-info-light-9)',
  'bg|#e8f4fd': 'var(--el-color-info-light-9)',
  'bg|#e3f2fd': 'var(--el-color-info-light-9)',
  'bg|#bbdefb': 'var(--el-color-info-light-8)',
  'bg|#e6f7ff': 'var(--el-color-info-light-9)',
  'bg|#e6f4ff': 'var(--el-color-info-light-9)',
  'bg|#cce5ff': 'var(--el-color-info-light-8)',
  'bg|#f0e6ff': 'var(--el-color-info-light-9)',
  // —— 背景（实色块：按钮/徽标）
  'bg|#1976d2': 'var(--el-color-primary)',
  'bg|#1565c0': 'var(--el-color-primary-dark-2)',
  'bg|#0d6efd': 'var(--el-color-primary)',
  'bg|#007bff': 'var(--el-color-primary)',
  'bg|#0b5ed7': 'var(--el-color-primary-dark-2)',
  'bg|#3b82f6': 'var(--el-color-primary)',
  'bg|#f59e0b': 'var(--el-color-primary)',
  'bg|#d97706': 'var(--el-color-primary-dark-2)',
  'bg|#b45309': 'var(--el-color-primary)',
  'bg|#92400e': 'var(--el-color-primary-dark-2)',
  'bg|#dc3545': 'var(--el-color-error)',
  'bg|#c0392b': 'var(--el-color-error)',
  'bg|#ef4444': 'var(--el-color-error)',
  'bg|#b91c1c': 'var(--el-color-error-dark-2)',
  'bg|#28a745': 'var(--el-color-success)',
  'bg|#218838': 'var(--el-color-success)',
  'bg|#16a34a': 'var(--el-color-success)',
  'bg|#059669': 'var(--el-color-success)',
  'bg|#ffc107': 'var(--el-color-warning)',
  'bg|#e0a800': 'var(--el-color-warning)',
  'bg|#6c757d': 'var(--el-fill-color)',
  // —— 背景（深色控制台/终端面 → 主题跟随）
  'bg|#1e1e1e': 'var(--el-bg-color-page)',
  'bg|#0f1419': 'var(--el-bg-color-page)',
  'bg|#111827': 'var(--el-bg-color-page)',
  'bg|#2d2d2d': 'var(--el-fill-color)',
  'bg|#252526': 'var(--el-fill-color)',
  'bg|#3c3c3c': 'var(--el-fill-color-light)',
  'bg|#333': 'var(--el-fill-color)',
  'bg|#3d3d3d': 'var(--el-fill-color)',
  'bg|#4a4a4a': 'var(--el-fill-color)',
  'bg|#555': 'var(--el-fill-color-light)',
  'bg|#666': 'var(--el-fill-color-light)',
  'bg|#fffafa': 'var(--el-color-error-light-9)',
  'bg|#ffcdd2': 'var(--el-color-error-light-8)',
  'bg|#f0fff4': 'var(--el-color-success-light-9)',
  'bg|#dbeafe': 'var(--el-color-info-light-9)',
  'bg|#e2e3e5': 'var(--el-color-info-light-9)',
  'bg|#f5f7fa': 'var(--el-fill-color-light)',
  'bg|rgba(0, 0, 0, 0.5)': 'var(--el-overlay-color-lighter)',
  'bg|rgba(52, 211, 153, 0.1)': 'var(--el-color-success-light-9)',
  'bg|rgba(245, 158, 11, 0.12)': 'var(--gold-tint-strong)',
  'bg|rgba(29, 78, 216, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(16, 185, 129, 0.12)': 'var(--el-color-success-light-9)',
  'bg|rgba(167, 139, 250, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(45, 212, 191, 0.12)': 'var(--el-color-success-light-9)',
  'bg|#f3e5f5': 'var(--el-color-info-light-9)',
  'bg|#fff3e0': 'var(--el-color-warning-light-9)',
  'bg|#ffebee': 'var(--el-color-error-light-9)',
  'bg|#fee2e2': 'var(--el-color-error-light-9)',
  'bg|#e0e7ff': 'var(--el-color-info-light-9)',
  'bg|#cbd5e0': 'var(--el-fill-color)',
  'bg|#4d4d4d': 'var(--el-fill-color)',
  'bg|#f44336': 'var(--el-color-error)',
  'bg|#d32f2f': 'var(--el-color-error)',
  'bg|#c82333': 'var(--el-color-error-dark-2)',
  'bg|#e53e3e': 'var(--el-color-error)',
  'bg|#38a169': 'var(--el-color-success)',
  'bg|#17a2b8': 'var(--el-color-info)',
  'bg|#fd7e14': 'var(--el-color-warning)',
  'bg|#ffe5d0': 'var(--el-color-warning-light-9)',
  'bg|#f0fdf4': 'var(--el-color-success-light-9)',
  'bg|#fafbfc': 'var(--el-fill-color-lighter)',
  'bg|rgba(255, 255, 255, 0.8)': 'color-mix(in srgb, var(--el-bg-color) 80%, transparent)',
  'bg|rgba(255, 255, 255, 0.015)': 'color-mix(in srgb, var(--el-color-white) 2%, transparent)',
  'bg|rgba(239, 68, 68, 0.1)': 'var(--el-color-error-light-9)',
  'bg|rgba(217, 119, 6, 0.12)': 'var(--gold-tint-strong)',
  'bg|rgba(16, 185, 129, 0.15)': 'var(--el-color-success-light-9)',
  'bg|rgba(99, 102, 241, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(139, 92, 246, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(236, 72, 153, 0.12)': 'var(--el-color-error-light-9)',
  'bg|rgba(20, 184, 166, 0.12)': 'var(--el-color-success-light-9)',
  'bg|rgba(14, 165, 233, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(168, 85, 247, 0.12)': 'var(--el-color-info-light-9)',
  'bg|rgba(132, 204, 22, 0.12)': 'var(--el-color-success-light-9)',
  'bg|rgba(100, 116, 139, 0.12)': 'var(--el-fill-color)',
  'bg|rgba(244, 63, 94, 0.12)': 'var(--el-color-error-light-9)',
  'bg|rgba(249, 115, 22, 0.12)': 'var(--el-color-warning-light-9)',
  'bg|rgba(59, 130, 246, 0.12)': 'var(--el-color-info-light-9)',
  'bg|#5d5d5d': 'var(--el-fill-color-light)',
  // —— 边框
  'border|#e9ecef': 'var(--el-border-color-lighter)',
  'border|#e5e7eb': 'var(--el-border-color-lighter)',
  'border|#efe8d8': 'var(--el-border-color-lighter)',
  'border|#dee2e6': 'var(--el-border-color-light)',
  'border|#3d3d3d': 'var(--el-border-color)',
  'border|#4d4d4d': 'var(--el-border-color)',
  'border|#555': 'var(--el-border-color)',
  'border|#f1f3f5': 'var(--el-border-color-lighter)',
  'border|#ffcdd2': 'var(--el-color-error-light-8)',
  'border|#fcd34d': 'var(--el-color-warning-light-7)',
  'border|#6ee7b7': 'var(--el-color-success-light-7)',
  'border|#93c5fd': 'var(--el-color-info-light-7)',
  'border|#c6f6d5': 'var(--el-color-success-light-8)',
  'border|#fed7d7': 'var(--el-color-error-light-8)',
  'border|rgba(52, 211, 153, 0.2)': 'var(--el-color-success-light-8)',
  'border|#4fc3f7': 'var(--el-color-info)',
  'border|#fca5a5': 'var(--el-color-error-light-7)',
  'border|#cbd5e0': 'var(--el-border-color)',
  'border|#fecaca': 'var(--el-color-error-light-8)',
  'border|#bbf7d0': 'var(--el-color-success-light-8)',
  'border|#1565c0': 'var(--el-color-primary-dark-2)',
  'border|#218838': 'var(--el-color-success)',
  'border|#c82333': 'var(--el-color-error-dark-2)',
  'border|rgba(239, 68, 68, 0.3)': 'var(--el-color-error-light-7)',
  'border|rgba(255, 255, 255, 0.3)': 'color-mix(in srgb, var(--el-color-white) 30%, transparent)',
  'border|rgba(248, 113, 113, 0.2)': 'var(--el-color-error-light-8)',
  'border|#f8f9fa': 'var(--el-border-color-lighter)',
  'border|#d1d5db': 'var(--el-border-color)',
  'border|#d8cfbc': 'var(--el-border-color)',
  'border|#ced4da': 'var(--el-border-color)',
  'border|#cfd8dc': 'var(--el-border-color)',
  'border|#adb5bd': 'var(--el-border-color-dark)',
  'border|#9ca3af': 'var(--el-border-color-dark)',
  'border|#3e3e3e': 'var(--el-border-color)',
  'border|#495057': 'var(--el-border-color-dark)',
  'border|#1976d2': 'var(--el-color-primary)',
  'border|#0d6efd': 'var(--el-color-primary)',
  'border|#007bff': 'var(--el-color-primary)',
  'border|#3b82f6': 'var(--el-color-primary)',
  'border|#dc3545': 'var(--el-color-error)',
  'border|#ef4444': 'var(--el-color-error)',
  'border|#dc2626': 'var(--el-color-error)',
  'border|#28a745': 'var(--el-color-success)',
  'border|#ffc107': 'var(--el-color-warning)',
  'border|#e0a800': 'var(--el-color-warning)',
  'border|#f59e0b': 'var(--el-color-primary)',
  'border|#d97706': 'var(--el-color-primary-dark-2)',
  'border|#6c757d': 'var(--el-border-color-dark)',
  'border|#fff': 'var(--el-border-color-lighter)',
  'border|white': 'var(--el-border-color-lighter)',
  'border|#ffffff': 'var(--el-border-color-lighter)',
};

/** 整值规则（box-shadow / 渐变 / 遮罩）。key = `组|归一化整值` */
const WHOLE_RULES = {
  // 纯黑投影 → EP 阴影阶（同形：0 1px 2px / 0 4px 12px / 0 8px 24px）
  'shadow|0 1px 2px rgba(0, 0, 0, 0.05)': 'var(--el-box-shadow-lighter)',
  'shadow|0 1px 2px rgba(0, 0, 0, 0.08)': 'var(--el-box-shadow-lighter)',
  'shadow|0 1px 2px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-lighter)',
  'shadow|0 1px 3px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-lighter)',
  'shadow|0 1px 3px rgba(0, 0, 0, 0.12)': 'var(--el-box-shadow-lighter)',
  'shadow|0 2px 4px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-lighter)',
  'shadow|0 2px 6px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-light)',
  'shadow|0 2px 8px rgba(0, 0, 0, 0.06)': 'var(--el-box-shadow-light)',
  'shadow|0 2px 8px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-light)',
  'shadow|0 4px 12px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow-light)',
  'shadow|0 4px 12px rgba(0, 0, 0, 0.15)': 'var(--el-box-shadow-light)',
  'shadow|0 6px 16px rgba(0, 0, 0, 0.12)': 'var(--el-box-shadow)',
  'shadow|0 8px 24px rgba(0, 0, 0, 0.15)': 'var(--el-box-shadow)',
  'shadow|0 12px 30px -12px rgba(0, 0, 0, 0.45)': 'var(--el-box-shadow-dark)',
  'shadow|0 6px 18px -8px rgba(0, 0, 0, 0.5)': 'var(--el-box-shadow-dark)',
  'shadow|0 6px 16px -6px rgba(0, 0, 0, 0.3)': 'var(--el-box-shadow)',
  'shadow|0 12px 32px rgba(0, 0, 0, 0.35)': 'var(--el-box-shadow-dark)',
  // 品牌蓝聚焦环 → 金色聚色环（形状不变，颜色归品牌）
  'shadow|0 0 0 3px rgba(25, 118, 210, 0.1)': '0 0 0 3px var(--gold-tint-strong)',
  'shadow|0 0 0 3px rgba(25, 118, 210, 0.2)': '0 0 0 3px var(--gold-tint-strong)',
  'shadow|0 0 0 3px rgba(13, 110, 253, 0.1)': '0 0 0 3px var(--gold-tint-strong)',
  'shadow|0 0 0 3px rgba(59, 130, 246, 0.1)': '0 0 0 3px var(--gold-tint-strong)',
  'shadow|0 0 0 3px rgba(59, 130, 246, 0.15)': '0 0 0 3px var(--gold-tint-strong)',
  'shadow|0 4px 12px rgba(25, 118, 210, 0.12)': 'var(--el-box-shadow-light)',
  // 边框 hairline（DbAdmin 网格）
  'border|rgba(120, 130, 150, 0.18)': 'var(--el-border-color-lighter)',
  'border|rgba(120, 130, 150, 0.22)': 'var(--el-border-color-lighter)',
  'border|rgba(120, 130, 150, 0.25)': 'var(--el-border-color-light)',
  // 蓝紫渐变（三禁红线家族）与装饰渐变 → el 主色组合
  'bg|linear-gradient(135deg, #667eea 0%, #764ba2 100%)': 'linear-gradient(135deg, var(--el-color-primary), var(--el-color-primary-dark-2))',
  'bg|linear-gradient(90deg, #2196f3, #9c27b0)': 'linear-gradient(90deg, var(--el-color-primary), var(--el-color-primary-dark-2))',
  'bg|linear-gradient(90deg, #4caf50, #8bc34a)': 'linear-gradient(90deg, var(--el-color-success), var(--el-color-success-dark-2))',
  'bg|linear-gradient(90deg, #4caf50, #ff9800, #f44336)': 'linear-gradient(90deg, var(--el-color-success), var(--el-color-warning), var(--el-color-error))',
  'bg|linear-gradient(135deg, #e3f2fd 0%, #bbdefb 100%)': 'linear-gradient(135deg, var(--el-color-info-light-9), var(--el-color-info-light-8))',
  'bg|linear-gradient(135deg, #e8f5e9 0%, #c8e6c9 100%)': 'linear-gradient(135deg, var(--el-color-success-light-9), var(--el-color-success-light-8))',
  'bg|linear-gradient(135deg, #f0f7ff 0%, #e8f4fd 100%)': 'linear-gradient(135deg, var(--el-color-info-light-9), var(--el-color-info-light-8))',
  // App.vue 遮罩：保留运行时旋钮 --overlay-opacity，色值锚到常暗 --ink-950
  'bg|rgba(11, 9, 8, var(--overlay-opacity, 0.55))': 'color-mix(in srgb, var(--ink-950) calc(var(--overlay-opacity, 0.55) * 100%), transparent)',
  'bg|rgba(11, 9, 8, calc(var(--overlay-opacity, 0.55) - 0.05))': 'color-mix(in srgb, var(--ink-950) calc((var(--overlay-opacity, 0.55) - 0.05) * 100%), transparent)',
  'bg|radial-gradient(ellipse at 30% 20%, rgba(245, 158, 11, 0.04) 0%, transparent 70%)': 'radial-gradient(ellipse at 30% 20%, color-mix(in srgb, var(--el-color-primary) 4%, transparent) 0%, transparent 70%)',
  // 状态脉冲动画（0 → 扩散消失）：形状保留，色锚语义 token
  'shadow|0 0 0 0 rgba(52, 211, 153, 0.5)': '0 0 0 0 color-mix(in srgb, var(--el-color-success) 50%, transparent)',
  'shadow|0 0 0 6px rgba(52, 211, 153, 0)': '0 0 0 6px transparent',
  'shadow|0 0 0 0 rgba(0, 123, 255, 0.4)': '0 0 0 0 color-mix(in srgb, var(--el-color-primary) 40%, transparent)',
  'shadow|0 0 0 6px rgba(0, 123, 255, 0)': '0 0 0 6px transparent',
  'shadow|0 20px 60px rgba(0, 0, 0, 0.3)': 'var(--el-box-shadow-dark)',
  'shadow|0 2px 8px rgba(0, 0, 0, 0.04)': 'var(--el-box-shadow-light)',
  'shadow|0 0 0 4px rgba(52, 211, 153, 0)': '0 0 0 4px transparent',
  'shadow|0 0 16px rgba(245, 158, 11, 0.06)': '0 0 16px color-mix(in srgb, var(--el-color-primary) 6%, transparent)',
  'bg|rgba(248, 113, 113, 0.1)': 'var(--el-color-error-light-9)',
  'border|rgba(248, 113, 113, 0.2)': 'var(--el-color-error-light-8)',
  'bg|linear-gradient(90deg, #ff9800, #ffc107)': 'linear-gradient(90deg, var(--el-color-warning), var(--el-color-warning-dark-2))',
  'bg|linear-gradient(90deg, #f44336, #e91e63)': 'linear-gradient(90deg, var(--el-color-error), var(--el-color-error-dark-2))',
  'shadow|0 0 0 2px rgba(56, 161, 105, 0.2)': '0 0 0 2px color-mix(in srgb, var(--el-color-success) 20%, transparent)',
  'shadow|0 0 0 3px rgba(220, 53, 69, 0.1)': '0 0 0 3px color-mix(in srgb, var(--el-color-error) 10%, transparent)',
  'shadow|0 0 10px rgba(245, 158, 11, 0.4)': '0 0 10px color-mix(in srgb, var(--el-color-primary) 40%, transparent)',
  'shadow|inset 0 0 0 1px rgba(245, 158, 11, 0.08)': 'inset 0 0 0 1px var(--gold-tint)',
  'shadow|0 10px 15px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow)',
  'shadow|0 4px 16px rgba(0, 0, 0, 0.1)': 'var(--el-box-shadow)',
  'shadow|0 1px 3px rgba(0, 0, 0, 0.2)': 'var(--el-box-shadow-lighter)',
  'shadow|0 0 0 2px rgba(25, 118, 210, 0.1)': '0 0 0 2px var(--gold-tint-strong)',
};

/* ---------------- 迁移执行 ---------------- */
const HEX_RE = /#[0-9a-fA-F]{3,8}\b/;
const RGB_RE = /\brgba?\([^)]*\)/;
const NAMED_RE = /\b(white|black)\b/;

function findColorLiteral(value) {
  let m = value.match(HEX_RE);
  if (m) return m[0];
  m = value.match(RGB_RE);
  if (m) return m[0];
  // 命名色仅在「纯字面量」值里匹配；值中已含 var( 时不再找命名色，
  // 否则会把 var(--el-color-white) 里的 white 再替换一层（2026-11-23 apply2 腐损实证）
  if (/\bvar\(/.test(value)) return null;
  m = value.match(NAMED_RE);
  if (m) return m[0];
  return null;
}

function normalizeDecl(raw) {
  return raw.replace(/\s+/g, ' ').trim().replace(/;$/, '').trim();
}

function walk(dir, out) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (!SKIP_DIRS.test(p)) walk(p, out);
      continue;
    }
    if (!/\.(vue|css|scss|ts|tsx)$/.test(e.name)) continue;
    if (SKIP_FILES.test(e.name)) continue;
    out.push(p);
  }
}

const report = { files: 0, replaced: 0, unmapped: new Map() };

function addUnmapped(file, line, decl) {
  const key = `${file}:${line}`;
  if (!report.unmapped.has(key)) report.unmapped.set(key, []);
  report.unmapped.get(key).push(decl);
}

function migrateFile(file) {
  const rel = path.relative(ROOT, file).replace(/\\/g, '/');
  const src = fs.readFileSync(file, 'utf8');
  const lines = src.split('\n');
  let changed = 0;
  for (let i = 0; i < lines.length; i++) {
    let line = lines[i];
    // ① el var 兜底剥离（themes 供给齐备，hex 兜底即幻影）
    const stripped = line.replace(/var\((--el-[\w-]+),\s*(#[0-9a-fA-F]{3,8}|rgba?\([^)]*\)|white|black)\)/g, 'var($1)');
    if (stripped !== line) { line = stripped; changed++; }
    // ② 声明扫描
    const declRe = /([-a-zA-Z]+)\s*:\s*([^;{}]+)/g;
    let m;
    let newLine = line;
    while ((m = declRe.exec(line)) !== null) {
      const prop = m[1];
      const rawVal = m[2];
      const val = normalizeDecl(rawVal);
      if (!val || /url\(/i.test(val)) continue;
      const group = G_SHADOW.test(prop) ? 'shadow' : G_BG.test(prop) ? 'bg' : G_BORDER.test(prop) ? 'border' : G_TEXT.test(prop) ? 'text' : null;
      if (!group) continue;
      // ②-a 整值规则
      const wholeKey = `${group}|${val.toLowerCase()}`;
      if (WHOLE_RULES[wholeKey]) {
        const rep = WHOLE_RULES[wholeKey];
        newLine = newLine.replace(rawVal, (s) => s.replace(val, rep));
        changed++;
        continue;
      }
      // ②-b 色值字面量原位替换
      const lit = findColorLiteral(val);
      if (!lit) continue;
      const colorKey = `${group}|${lit.toLowerCase()}`;
      if (COLOR_RULES[colorKey]) {
        const rep = COLOR_RULES[colorKey];
        newLine = newLine.replace(rawVal, (s) => s.replace(lit, rep));
        changed++;
        continue;
      }
      // ②-c 未映射（不改动，报告）
      if (!/var\(--el-/.test(val)) addUnmapped(rel, i + 1, `${prop}: ${val}`);
    }
    if (newLine !== lines[i]) lines[i] = newLine;
  }
  if (changed > 0) {
    report.files++;
    report.replaced += changed;
    if (APPLY) fs.writeFileSync(file, lines.join('\n'), 'utf8');
    console.log(`  ${rel}  repl=${changed}`);
  }
}

let files = [];
walk(path.join(ROOT, 'ForgeSelf.Web', 'src'), files);
// 插件域走后续批次；本次只做宿主
if (SCOPE) files = files.filter((f) => path.relative(ROOT, f).replace(/\\/g, '/').startsWith(SCOPE));
files = files.filter((f) => !FILE_EXEMPT.includes(path.relative(ROOT, f).replace(/\\/g, '/')));
for (const e of exemptFiles) files = files.filter((f) => path.resolve(f) !== path.resolve(e));

console.log(`[hardcoded ${APPLY ? 'APPLY' : 'DRY-RUN'}] files=${files.length}`);
for (const f of files) migrateFile(f);
console.log(`[hardcoded] files changed: ${report.files}, replacements: ${report.replaced}`);
if (report.unmapped.size > 0) {
  console.log(`[hardcoded] 未映射（保留待人工/增补规则）: ${report.unmapped.size} 行`);
  const byFile = new Map();
  for (const [key, decls] of report.unmapped) {
    const [f, ln] = key.split(':');
    if (!byFile.has(f)) byFile.set(f, []);
    byFile.get(f).push(`${ln}: ${decls[0]}`);
  }
  for (const [f, items] of [...byFile.entries()].sort((a, b) => b[1].length - a[1].length).slice(0, 25)) {
    console.log(`  ${f} (${items.length})`);
    for (const it of items.slice(0, 4)) console.log(`    ${it}`);
  }
}
