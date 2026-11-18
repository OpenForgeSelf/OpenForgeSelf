/**
 * 工作台图标：交接包 project-workbench.html 的自绘内联 SVG（clean-room，无外部资源）。
 * 由 WorkbenchView 一次性挂到 .wb 根节点；组件只用 ic() 生成 <use> 引用。
 */
export const ICON_SPRITE = `<svg xmlns="http://www.w3.org/2000/svg" style="position:absolute;width:0;height:0;overflow:hidden" aria-hidden="true">
  <symbol id="i-star" viewBox="0 0 24 24"><path d="M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1-5.4-2.9-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z"/></symbol>
  <symbol id="i-folder" viewBox="0 0 24 24"><path d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2z"/></symbol>
  <symbol id="i-search" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></symbol>
  <symbol id="i-plus" viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></symbol>
  <symbol id="i-download" viewBox="0 0 24 24"><path d="M12 3v12m0 0l-4-4m4 4l4-4M4 19h16"/></symbol>
  <symbol id="i-pencil" viewBox="0 0 24 24"><path d="M4 20h4l10-10-4-4L4 16v4zM14 6l4 4"/></symbol>
  <symbol id="i-close" viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></symbol>
  <symbol id="i-home" viewBox="0 0 24 24"><path d="M3 11l9-7 9 7v9a1 1 0 01-1 1h-5v-6H9v6H4a1 1 0 01-1-1z"/></symbol>
  <symbol id="i-terminal" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3m6 0h4"/></symbol>
  <symbol id="i-help" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 113.5 2.3c-.9.5-1 1.2-1 2.2M12 17h.01"/></symbol>
  <symbol id="i-list" viewBox="0 0 24 24"><path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/></symbol>
  <symbol id="i-check" viewBox="0 0 24 24"><path d="M5 13l4 4L19 7"/></symbol>
  <symbol id="i-puzzle" viewBox="0 0 24 24"><path d="M9 3h6v3a2 2 0 104 0h2v6h-3a2 2 0 100 4h3v5H9v-3a2 2 0 10-4 0H3v-6h3a2 2 0 100-4H3V3h6z"/></symbol>
  <symbol id="i-box" viewBox="0 0 24 24"><path d="M4 8l8-4 8 4v8l-8 4-8-4zM4 8l8 4 8-4M12 12v8"/></symbol>
  <symbol id="i-sliders" viewBox="0 0 24 24"><path d="M4 7h9M17 7h3M4 17h3M11 17h9M15 4v6M8 14v6"/></symbol>
  <symbol id="i-tasks" viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="2"/><path d="M8 9h8M8 13h8M8 17h5"/></symbol>
  <symbol id="i-grid" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></symbol>
  <symbol id="i-chevron" viewBox="0 0 24 24"><path d="M6 9l6 6 6-6"/></symbol>
  <symbol id="i-back" viewBox="0 0 24 24"><path d="M15 18l-6-6 6-6"/></symbol>
  <symbol id="i-external" viewBox="0 0 24 24"><path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 01-1 1H5a1 1 0 01-1-1V7a1 1 0 011-1h5"/></symbol>
  <symbol id="i-refresh" viewBox="0 0 24 24"><path d="M20 11a8 8 0 10-2 6M20 5v6h-6"/></symbol>
  <symbol id="i-play" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M10 9l5 3-5 3z"/></symbol>
  <symbol id="i-more" viewBox="0 0 24 24"><circle cx="5" cy="12" r="1.4"/><circle cx="12" cy="12" r="1.4"/><circle cx="19" cy="12" r="1.4"/></symbol>
  <symbol id="i-moon" viewBox="0 0 24 24"><path d="M20 14A8 8 0 0110 4a8 8 0 1010 10z"/></symbol>
  <symbol id="i-bug" viewBox="0 0 24 24"><rect x="8" y="7" width="8" height="12" rx="4"/><path d="M8 11H4m16 0h-4M8 15H4m16 0h-4M9 7l-2-3m8 3l2-3"/></symbol>
  <symbol id="i-globe" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a15 15 0 010 18M12 3a15 15 0 000 18"/></symbol>
  <symbol id="i-clip" viewBox="0 0 24 24"><path d="M20 11l-8 8a5 5 0 01-7-7l9-9a3.5 3.5 0 015 5l-9 9a2 2 0 01-3-3l8-8"/></symbol>
  <symbol id="i-info" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/></symbol>
  <symbol id="i-port" viewBox="0 0 24 24"><path d="M4 8h16v8H4zM8 12h.01M12 12h.01M16 12h.01"/></symbol>
</svg>`

/** 生成图标引用标签；name 为 i-* 的后缀。 */
export function ic(name: string, cls = ''): string {
  return `<svg class="ic ${cls}" aria-hidden="true"><use href="#i-${name}"></use></svg>`
}

/** 文本转义（路径、项目名等一律转义后再进 v-html 片段）。 */
export function esc(s: unknown): string {
  return String(s ?? '').replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c] ?? c))
}
