# 插件目录

本目录用于存放 OpenForgeSelf 系统的所有插件。

## 目录结构

```
Plugins/
├── SamplePlugin/          # 示例插件目录
│   ├── plugin.json        # 插件清单文件
│   ├── *.dll              # 插件程序集
│   └── ...                # 其他插件文件
└── ...
```

## plugin.json 清单文件格式

```json
{
  "Id": "OpenForgeSelf.SamplePlugin",      // 插件唯一标识（必填）
  "Name": "示例插件",                   // 插件名称（必填）
  "Version": "1.0.0",                  // 插件版本（必填）
  "Author": "OpenForgeSelf Team",          // 插件作者
  "Description": "插件描述",             // 插件描述
  "IconUrl": "",                       // 插件图标URL
  "EntryAssembly": "Plugin.dll",       // 入口程序集文件名（必填）
  "EntryType": "Namespace.PluginClass", // 入口类全名（必填，需实现IPlugin接口）
  "Dependencies": [],                  // 依赖的其他插件ID列表
  "Permissions": []                    // 需要的权限列表
}
```

## 权限列表

- `FileSystem` - 访问文件系统
- `Network` - 访问网络
- `Database` - 访问数据库
- `Configuration` - 访问配置
- `ExtensionPoint` - 注册扩展点
- `AIService` - 访问AI服务
- `PluginManagement` - 管理其他插件
