# 12-faq — 常见问题

> 状态：部分（2026-08-12）
> 最后更新：2026-08-12

**Q：后端默认端口是多少？前端呢？**
A：后端 `ForgeSetting.PortNumber = 7102`（运行期可改，重启生效）；前端 dev `7002`。AGENTS.md 旧注 7300/7380 已过时。

**Q：AI Provider 的 ApiKey 会明文返回吗？**
A：不会。列表/详情只返回掩码（`sk-****3456`），明文仅在解密后用于上游请求。

**Q：为什么聊天记录里 app 聊天和代理录制能合并到一个会话列表？**
A：`ChatSession` 是统一聚合根，app 聊天（`ChatController`）与代理录制（`UnifiedAI/*`）都 upsert 到它，靠 `SessionKey`+`Source` 区分。详见 `02-features/010-chat-session-aggregation.md`。

**Q：多模态请求为什么会省 token？**
A：`MultimodalProcessor` 对同一会话内相同图片（同视觉模型）缓存识别结果到本地文件，命中复用、失败不缓存。详见 `02-features/011-multimodal-image-cache.md`。

**Q：改了 VisionModel 等配置没生效？**
A：运行实例无权限停止时，需手动重启后端 exe 才生效。
