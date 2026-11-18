#!/usr/bin/env python3
"""OpenAI-compatible keyword mock for /api/agent 联调.

Keeps the original ST keywords and adds:
  agent-tool-call  → list_files tool_call
  agent-approval   → write_file tool_call (high-risk, must approve)
  agent-fail       → text used with the facade fail+retry path
  agent-running    → delay a few seconds, then a short completion (F13 in-progress)
  agent-workflow / agent-design / agent-suggest / agent-project / agent-todo
                   → short text; the facade seeds the matching workspace panel
  agent-workflow-approval → workflow already started; high-risk write_file
                   must be confirmed in the main thread before it runs
After a tool result is already in the messages, returns one "工具已执行完毕。"
(and task_complete when that exit tool is advertised) so the loop ends.

Default: 127.0.0.1:18766 (do not bind 18765, which the ST host uses).
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
DEFAULT_PORT = 18766
MODEL_ID = "st-keyword-mock"

KEYWORDS = (
    ("st-run-write", "mkdir -p /tmp/st-r7-probe"),
    ("st-insert-ls", "ls"),
)
MISS_REPLY = "未命中约定关键词。请发送 st-insert-ls、st-run-write、agent-tool-call、agent-approval、agent-fail、agent-running、agent-workflow、agent-workflow-approval、agent-design、agent-suggest、agent-project 或 agent-todo。"
TOOL_DONE_REPLY = "工具已执行完毕。"

LIST_FILES_ARGS = {"cwd": "/tmp"}
WRITE_FILE_ARGS = {
    "cwd": "/tmp/agent-itest",
    "path": "/tmp/agent-itest/note.txt",
    "content": "ok",
    "start_line": 1,
    "end_line": 1,
}


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


KEYWORD_TOOLS = ("list_files", "write_file")


def _tool_call_name(tc: Any) -> str:
    if not isinstance(tc, dict):
        return ""
    fn = tc.get("function") if isinstance(tc.get("function"), dict) else {}
    return str(tc.get("name") or fn.get("name") or "")


def _message_tool_names(msg: dict[str, Any]) -> list[str]:
    names: list[str] = []
    for tc in msg.get("tool_calls") or []:
        name = _tool_call_name(tc)
        if name:
            names.append(name)
    name = str(msg.get("name") or "")
    if name:
        names.append(name)
    return names


def has_keyword_tool_result(body: dict[str, Any]) -> bool:
    """True only after list_files / write_file already returned a tool result.

    The host often prepends memory tools (get_relevant_memories). Those must not
    short-circuit the keyword tool_call / approval paths.
    """
    pending: set[str] = set()
    for msg in body.get("messages") or []:
        if not isinstance(msg, dict):
            continue
        role = str(msg.get("role") or "").lower()
        if role == "assistant":
            for name in _message_tool_names(msg):
                if name in KEYWORD_TOOLS:
                    pending.add(name)
        if role in {"tool", "function"} or msg.get("tool_call_id"):
            names = _message_tool_names(msg)
            # some clients only send tool_call_id; if a keyword tool is pending, count it
            if any(n in KEYWORD_TOOLS for n in names) or (pending and role in {"tool", "function"}):
                if any(n in KEYWORD_TOOLS for n in names) or (pending & set(KEYWORD_TOOLS)):
                    return True
    return False


def match_text(haystack: str) -> str:
    last_index = -1
    chosen: str | None = None
    for keyword, command in KEYWORDS:
        index = haystack.find(keyword)
        if index >= 0 and index >= last_index:
            last_index = index
            chosen = command
    if chosen is None:
        return MISS_REPLY
    return f"```bash\n{chosen}\n```"


TEXT_KEYWORDS = (
    ("agent-fail", "任务失败，请重试。"),
    ("agent-running", "任务仍在进行，即将完成。"),
    ("agent-workflow-approval", "工作流已启动，高风险步骤待确认。"),
    ("agent-workflow", "已关联联调工作流，可暂停或取消。"),
    ("agent-design", "已给出设计预览，默认不写库。"),
    ("agent-suggest", "已给出可执行建议。"),
    ("agent-project", "已绑定联调项目。"),
    ("agent-todo", "已添加可下发待办。"),
)
RUNNING_HOLD_SECONDS = 8


def match_seed_text(haystack: str) -> str | None:
    last_index = -1
    last_len = 0
    chosen: str | None = None
    for keyword, reply in TEXT_KEYWORDS:
        index = haystack.rfind(keyword)
        if index >= 0 and (index > last_index or (index == last_index and len(keyword) > last_len)):
            last_index = index
            last_len = len(keyword)
            chosen = reply
    return chosen


def choose_tool(haystack: str) -> tuple[str, dict[str, Any]] | None:
    # last occurrence wins if both appear
    i_tool = haystack.rfind("agent-tool-call")
    i_appr = haystack.rfind("agent-approval")
    i_wf_appr = haystack.rfind("agent-workflow-approval")
    i_write = max(i_appr, i_wf_appr)
    if i_tool < 0 and i_write < 0:
        return None
    if i_write > i_tool:
        return "write_file", WRITE_FILE_ARGS
    return "list_files", LIST_FILES_ARGS


def completion_id() -> str:
    return "chatcmpl-" + uuid.uuid4().hex[:24]


def non_stream_text(model: str, content: str) -> dict[str, Any]:
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
        "usage": {"prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2},
    }


def non_stream_tool(model: str, name: str, args: dict[str, Any]) -> dict[str, Any]:
    call_id = "call_" + uuid.uuid4().hex[:20]
    return {
        "id": completion_id(),
        "object": "chat.completion",
        "created": int(time.time()),
        "model": model or MODEL_ID,
        "choices": [
            {
                "index": 0,
                "message": {
                    "role": "assistant",
                    "content": None,
                    "tool_calls": [
                        {
                            "id": call_id,
                            "type": "function",
                            "function": {
                                "name": name,
                                "arguments": json.dumps(args, ensure_ascii=False),
                            },
                        }
                    ],
                },
                "finish_reason": "tool_calls",
            }
        ],
        "usage": {"prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2},
    }


def stream_text(model: str, content: str) -> list[str]:
    cid = completion_id()
    created = int(time.time())
    mid = model or MODEL_ID
    first = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [{"index": 0, "delta": {"role": "assistant", "content": content}, "finish_reason": None}],
    }
    last = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}],
    }
    return [f"data: {json.dumps(first, ensure_ascii=False)}", f"data: {json.dumps(last, ensure_ascii=False)}", "data: [DONE]"]


def stream_tool(model: str, name: str, args: dict[str, Any]) -> list[str]:
    cid = completion_id()
    created = int(time.time())
    mid = model or MODEL_ID
    call_id = "call_" + uuid.uuid4().hex[:20]
    arguments = json.dumps(args, ensure_ascii=False)
    first = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [
            {
                "index": 0,
                "delta": {
                    "role": "assistant",
                    "content": None,
                    "tool_calls": [
                        {
                            "index": 0,
                            "id": call_id,
                            "type": "function",
                            "function": {"name": name, "arguments": arguments},
                        }
                    ],
                },
                "finish_reason": None,
            }
        ],
    }
    last = {
        "id": cid,
        "object": "chat.completion.chunk",
        "created": created,
        "model": mid,
        "choices": [{"index": 0, "delta": {}, "finish_reason": "tool_calls"}],
    }
    return [f"data: {json.dumps(first, ensure_ascii=False)}", f"data: {json.dumps(last, ensure_ascii=False)}", "data: [DONE]"]


def normalize_path(path: str) -> str:
    parsed = urlparse(path)
    return (parsed.path or "/").rstrip("/") or "/"


def advertised_tool_names(body: dict[str, Any]) -> set[str]:
    names: set[str] = set()
    for tool in body.get("tools") or []:
        if not isinstance(tool, dict):
            continue
        fn = tool.get("function") if isinstance(tool.get("function"), dict) else {}
        name = str(tool.get("name") or fn.get("name") or "")
        if name:
            names.add(name)
    return names


def assistant_already_said_done(body: dict[str, Any]) -> bool:
    for msg in body.get("messages") or []:
        if not isinstance(msg, dict):
            continue
        if str(msg.get("role") or "").lower() != "assistant":
            continue
        if TOOL_DONE_REPLY in collect_text(msg):
            return True
        for name in _message_tool_names(msg):
            if name in {"task_complete", "aiagent.task_complete"}:
                return True
    return False


def resolve_response(body: dict[str, Any]) -> tuple[str, dict[str, Any] | None]:
    if has_keyword_tool_result(body):
        if assistant_already_said_done(body):
            return "", None
        return TOOL_DONE_REPLY, None
    haystack = extract_haystack(body)
    tool = choose_tool(haystack)
    if tool:
        return "", tool
    seeded = match_seed_text(haystack)
    if seeded:
        return seeded, None
    return match_text(haystack), None


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args: Any) -> None:
        import sys
        sys.stderr.write("[%s] %s\n" % (self.log_date_time_string(), fmt % args))

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
            self._json(200, {
                "object": "list",
                "data": [{"id": MODEL_ID, "object": "model", "created": 0, "owned_by": "local"}],
            })
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

        text, tool = resolve_response(body)
        haystack = extract_haystack(body)
        if tool is None and "agent-running" in haystack.lower() and not has_keyword_tool_result(body):
            time.sleep(RUNNING_HOLD_SECONDS)
        try:
            import sys
            summary = []
            for m in body.get("messages") or []:
                if not isinstance(m, dict):
                    continue
                role = str(m.get("role") or "?")
                names = []
                for tc in m.get("tool_calls") or []:
                    if isinstance(tc, dict):
                        fn = tc.get("function") if isinstance(tc.get("function"), dict) else {}
                        names.append(str(tc.get("name") or fn.get("name") or "?"))
                if m.get("name"):
                    names.append(str(m.get("name")))
                content = m.get("content")
                preview = (content[:80] if isinstance(content, str) else type(content).__name__)
                summary.append({"role": role, "names": names, "tool_call_id": m.get("tool_call_id"), "preview": preview})
            kind = f"tool:{tool[0]}" if tool else f"text:{(text or '')[:40]}"
            tool_names=[]
            for t in body.get("tools") or []:
                if not isinstance(t, dict):
                    continue
                fn = t.get("function") if isinstance(t.get("function"), dict) else {}
                tool_names.append(str(t.get("name") or fn.get("name") or t.get("type") or "?"))
            rec = {"stream": bool(body.get("stream")), "kind": kind, "tools": tool_names, "messages": summary}
            sys.stderr.write("[resolve] " + json.dumps(rec, ensure_ascii=False) + "\n")
            with open("/tmp/mock-reqs.jsonl", "a", encoding="utf-8") as fh:
                fh.write(json.dumps({"kind": kind, "stream": bool(body.get("stream")), "tools": tool_names, "messages": summary}, ensure_ascii=False) + "\n")
        except Exception as ex:
            import sys
            sys.stderr.write(f"[resolve] log failed: {ex}\n")
        model = str(body.get("model") or MODEL_ID)
        stream = bool(body.get("stream"))
        if not stream:
            payload = non_stream_tool(model, tool[0], tool[1]) if tool else non_stream_text(model, text)
            self._json(200, payload)
            return

        chunks = stream_tool(model, tool[0], tool[1]) if tool else stream_text(model, text)
        payload = ("\n\n".join(chunks) + "\n\n").encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)
        self.wfile.flush()


def main() -> None:
    parser = argparse.ArgumentParser(description="智能体关键词模拟上游（OpenAI 兼容）")
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    args = parser.parse_args()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(
        f"agent-keyword-mock listening on http://{args.host}:{args.port}/v1 "
        f"(keywords: st-insert-ls, st-run-write, agent-tool-call, agent-approval, agent-fail, agent-running, agent-workflow, agent-workflow-approval, agent-design, agent-suggest, agent-project, agent-todo)",
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
