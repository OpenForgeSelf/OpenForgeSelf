// 跨包插件视图（Plugins/<X>/web/src）经画廊 story 壳进入本 program。
// 插件构建期 vite define 注入的常量在插件自身 env.d.ts 声明，但不随导入图进入本 program，
// 故在此声明（app 程序侧对应声明在 src/vite-env.d.ts，两 program 不重复）。
declare const __PLUGIN_VERSION__: string
