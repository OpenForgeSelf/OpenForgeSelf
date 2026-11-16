#!/usr/bin/env python3
"""本地 OpenAI 兼容模拟上游，供智能终端按关键词验收助手建议。

只提供 POST /v1/chat/completions 与 GET /v1/models，不注册新的提供方类型。
在宿主「设置 → AI 提供方」新增一条 Custom，接入地址指向本服务即可。

默认监听 127.0.0.1:18765。启动：
    python3 Plugins/SmartTerminal/tools/st-keyword-mock.py
    python3 Plugins/SmartTerminal/tools/st-keyword-mock.py --port 18765

匹配规则：对请求里全部消息文本做子串包含（插件会包一层会话上下文，不能整句相等）。
命中后返回带 bash 栅栏的建议，便于服务端抽出命令：
  st-insert-ls  → ls
  st-run-write  → mkdir -p /tmp/st-r7-probe
未命中返回一句不含代码块的说明。同时支持 stream=true（SSE）与非流式。
鉴权：接受任意或空的 Bearer，本地验收用占位密钥即可。
"""

from __future__ import annotations

import argparse
import json
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib.parse import urlparse

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 18765
MODEL_ID = "st-keyword-mock"

KEYWORDS = (
    ("st-run-write", "mkdir -p /tmp/st-r7-probe"),
    ("st-insert-ls", "ls"),
)

MISS_REPLY = "未命中约定关键词。请发送 st-insert-ls 或 st-run-write 以获得对应命令建议。"


def suggested_reply(command: str) -> str:
    return f"```bash\n{command}\n```"


def collect_text(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    if isinstance(value, list):
        return "\n".join(collect_text(item) for item in value)
    if isinstance(value, dict):
        parts: list[str] = []
        for key in ("content", "text", "input_text", "value"):
            if key in value:
                parts.append(collect_text(value[key]))
        if "parts" in value:
            parts.append(collect_text(value["parts"]))
        return "\n".join(p for p in parts if p)
    return str(value)


def extract_haystack(body: dict[str, Any]) -> str:
    chunks: list[str] = []
    for key in ("messages", "input"):
        if key in body:
            chunks.append(collect_text(body[key]))
    if "prompt" in body:
        chunks.append(collect_text(body["prompt"]))
    return "\n".join(c for c in chunks if c)


def match_reply(haystack: str) -> str:
    last_index = -1
    chosen: str | None = None
    for keyword, command in KEYWORDS:
        index = haystack.find(keyword)
        if index >= 0 and index >= last_index:
            last_index = index
            chosen = command
    if chosen is None:
        return MISS_REPLY
    return suggested_reply(chosen)


def completion_id() -> str:
    return "chatcmpl-" + uuid.uuid4().hex[:24]


def non_stream_payload(model: str, content: str) -> dict[str, Any]:
    return {
        "id": completion_id(),
        "object": "chat.completion",
        "created": int(time.time()),
        "model": model or MODEL_ID,
        "choices": [
            {
                "index": 0,
                "message": {"role": "assistant", "content": content},
                "finish_reason": "stop",
            }
        ],
        "usage": {
            "prompt_tokens": 1,
            "completion_tokens": 1,
            "total_tokens": 2,
        },
    }


def stream_chunks(model: str, content: str) -> list[str]:
    cid = completion_id()
    created = int(time.time())
    mid = model or MODEL_ID
    first = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [
            {
                "index": 0,
                "delta": {"role": "assistant", "content": content},
                "finish_reason": None,
            }
        ],
    }
    last = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}],
    }
    return [f"data: {json.dumps(first, ensure_ascii=False)}", f"data: {json.dumps(last, ensure_ascii=False)}", "data: [DONE]"]


def models_payload() -> dict[str, Any]:
    return {
        "object": "list",
        "data": [
            {
                "id": MODEL_ID,
                "object": "model",
                "created": 0,
                "owned_by": "local",
            }
        ],
    }


def normalize_path(path: str) -> str:
    parsed = urlparse(path)
    return (parsed.path or "/").rstrip("/") or "/"


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args: Any) -> None:
        sys_stderr = __import__("sys").stderr
        sys_stderr.write("[%s] %s\n" % (self.log_date_time_string(), fmt % args))

    def _send(self, status: int, body: bytes, content_type: str) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _json(self, status: int, payload: dict[str, Any]) -> None:
        raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self._send(status, raw, "application/json; charset=utf-8")

    def do_GET(self) -> None:  # noqa: N802
        path = normalize_path(self.path)
        if path in {"/health", "/v1/health"}:
            self._json(200, {"ok": True, "model": MODEL_ID})
            return
        if path in {"/v1/models", "/models"}:
            self._json(200, models_payload())
            return
        self._json(404, {"error": {"message": f"unknown path {path}", "type": "invalid_request_error"}})

    def do_POST(self) -> None:  # noqa: N802
        path = normalize_path(self.path)
        if path not in {"/v1/chat/completions", "/chat/completions"}:
            self._json(404, {"error": {"message": f"unknown path {path}", "type": "invalid_request_error"}})
            return
        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length) if length > 0 else b"{}"
        try:
            body = json.loads(raw.decode("utf-8") or "{}")
            if not isinstance(body, dict):
                raise ValueError("body must be an object")
        except (json.JSONDecodeError, ValueError) as exc:
            self._json(400, {"error": {"message": f"invalid json: {exc}", "type": "invalid_request_error"}})
            return

        content = match_reply(extract_haystack(body))
        model = str(body.get("model") or MODEL_ID)
        stream = bool(body.get("stream"))
        if not stream:
            self._json(200, non_stream_payload(model, content))
            return

        chunks = stream_chunks(model, content)
        payload = ("\n\n".join(chunks) + "\n\n").encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)
        self.wfile.flush()


def main() -> None:
    parser = argparse.ArgumentParser(description="智能终端关键词模拟上游（OpenAI 兼容）")
    parser.add_argument("--host", default=DEFAULT_HOST, help="监听地址，默认 127.0.0.1")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="监听端口，默认 18765")
    args = parser.parse_args()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(
        f"st-keyword-mock listening on http://{args.host}:{args.port}/v1 "
        f"(chat=/v1/chat/completions, models=/v1/models)",
        flush=True,
    )
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nstopped", flush=True)
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
