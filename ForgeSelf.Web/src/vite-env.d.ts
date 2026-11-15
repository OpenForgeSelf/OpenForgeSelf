/// <reference types="vite/client" />

// 插件构建期 vite define 注入的全局常量类型（对应各插件 web/vite.config.ts 的 define，
// 构建期文本替换）。根程序经组件画廊 story 壳编译跨包插件视图时，插件的 env.d.ts
// 不随导入图进入根程序，需在此声明（插件自身程序内另有同名声明，两程序不相交）。
declare const __PLUGIN_VERSION__: string