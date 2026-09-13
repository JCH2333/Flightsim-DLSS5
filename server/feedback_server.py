#!/usr/bin/env python3
"""DLSS5 反馈接收服务（POST /repo/dlss5/feedback）。

- 存储: $FEEDBACK_DIR/<反馈编号>/report.json（脱载荷）+ logs/ + shots/
- 限流: 每 IP 每天最多 10 条，每 10 分钟最多 1 条（状态持久化到 $FEEDBACK_DIR/_state.json）
- 部署: systemd (feedback.service) 监听 127.0.0.1:8430，由 nginx 反代；本机测试:
    FEEDBACK_DIR=/tmp/fb python3 feedback_server.py 8420
"""
import json
import os
import re
import secrets
import socketserver
import sys
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse


class ThreadingHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    """兼容 Python 3.6（老系统无 http.server.ThreadingHTTPServer）。"""
    daemon_threads = True
    allow_reuse_address = True

STORE_DIR = os.environ.get("FEEDBACK_DIR", "/var/lib/dlss5-feedback")
STATE_PATH = os.path.join(STORE_DIR, "_state.json")
MAX_BODY = 48 * 1024 * 1024          # 与 nginx client_max_body_size 一致
MAX_DESC = 8000
MAX_LOGS = 20
MAX_LOG_CHARS = 400_000              # 单条日志字符上限（约 400KB 文本）
MAX_SHOTS = 8
PATH = "/repo/dlss5/feedback"
DAILY_LIMIT = 10
INTERVAL_SECONDS = 600


def load_state():
    try:
        with open(STATE_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_state(state):
    os.makedirs(STORE_DIR, exist_ok=True)
    tmp = STATE_PATH + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(state, f)
    os.replace(tmp, STATE_PATH)


def check_rate(ip, state):
    """返回 None（放行）或拒绝原因。"""
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    now = time.time()
    rec = state.get("ips", {}).get(ip)
    if rec:
        if rec.get("day") != today:           # 跨天重置
            rec["day"], rec["count"] = today, 0
        if now - rec.get("last", 0) < INTERVAL_SECONDS:
            wait = int((INTERVAL_SECONDS - (now - rec.get("last", 0))) / 60) + 1
            return f"提交太频繁，请约 {wait} 分钟后再试（每 10 分钟限 1 条）。"
        if rec.get("count", 0) >= DAILY_LIMIT:
            return "今日反馈已达上限（每天 10 条），请明天再试或在粉丝群直接反馈。"
    return None


def record_submit(ip, state):
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    rec = state.setdefault("ips", {}).setdefault(ip, {})
    if rec.get("day") != today:
        rec["day"], rec["count"] = today, 0
    rec["count"] = rec.get("count", 0) + 1
    rec["last"] = now_time()
    save_state(state)


def now_time():
    return time.time()


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):  # 静默默认访问日志（systemd journal 里只留关键事件）
        pass

    def _reply(self, code, payload):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)
        self.close_connection = True

    def do_GET(self):
        self._reply(405, {"ok": False, "error": "method not allowed"})

    def do_POST(self):
        try:
            self._handle()
        except Exception as e:  # noqa: BLE001
            try:
                self._reply(500, {"ok": False, "error": f"internal error: {e}"})
            except Exception:
                pass

    def _handle(self):
        if urlparse(self.path).path.rstrip("/") != PATH:
            return self._reply(404, {"ok": False, "error": "not found"})

        length = int(self.headers.get("Content-Length", "0") or 0)
        if length <= 0 or length > MAX_BODY:
            return self._reply(413, {"ok": False, "error": "payload too large"})

        ip = self.headers.get("X-Real-IP") or self.client_address[0]

        # 限流在读取大 body 之前做（廉价拒绝）
        state = load_state()
        reason = check_rate(ip, state)
        if reason:
            return self._reply(429, {"ok": False, "error": reason})

        body = self.rfile.read(length)
        try:
            data = json.loads(body.decode("utf-8"))
        except Exception:
            return self._reply(400, {"ok": False, "error": "invalid JSON"})

        desc = str(data.get("description", "") or "").strip()
        logs = data.get("logs", [])
        shots = data.get("screenshots", [])
        if not desc and not logs:
            return self._reply(400, {"ok": False, "error": "请填写问题描述后再提交。"})
        if not isinstance(desc, str) or len(desc) > MAX_DESC:
            return self._reply(400, {"ok": False, "error": "问题描述过长。"})
        if not isinstance(logs, list) or len(logs) > MAX_LOGS:
            return self._reply(400, {"ok": False, "error": "日志条目非法。"})
        if not isinstance(shots, list) or len(shots) > MAX_SHOTS:
            return self._reply(400, {"ok": False, "error": "截图数量超限。"})

        fid = "FB-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + "-" + secrets.token_hex(2)
        fdir = os.path.join(STORE_DIR, fid)
        os.makedirs(os.path.join(fdir, "logs"), exist_ok=True)
        os.makedirs(os.path.join(fdir, "shots"), exist_ok=True)

        def safe_name(name, default):
            name = re.sub(r"[^0-9A-Za-z._\- \u4e00-\u9fff（）()]", "_", str(name or default))[:120]
            return name or default

        log_files = []
        for i, log in enumerate(logs):
            name = safe_name(log.get("name"), f"log-{i}.txt")
            content = str(log.get("content", "") or "")[:MAX_LOG_CHARS]
            fn = f"log-{i:02d}-{name}"
            if not fn.endswith(".txt"):
                fn += ".txt"
            with open(os.path.join(fdir, "logs", fn), "w", encoding="utf-8", errors="replace") as f:
                f.write(content)
            log_files.append({"name": name, "size": log.get("size"), "truncated": bool(log.get("truncated"))})

        shot_files = []
        for i, shot in enumerate(shots):
            name = safe_name(shot.get("name"), f"shot-{i}.png")
            try:
                raw = __import__("base64").b64decode(shot.get("data", "") or "")
            except Exception:
                continue
            if not raw or len(raw) > 16 * 1024 * 1024:
                continue
            fn = f"shot-{i:02d}-{name}"
            with open(os.path.join(fdir, "shots", fn), "wb") as f:
                f.write(raw)
            shot_files.append({"name": name, "size": len(raw)})

        report = {
            "id": fid,
            "receivedAt": datetime.now(timezone.utc).isoformat(),
            "ip": ip,
            "app": data.get("app"),
            "version": data.get("version"),
            "os": data.get("os"),
            "runtime": data.get("runtime"),
            "gpu": data.get("gpu"),
            "games": data.get("games", []),
            "description": desc,
            "logs": log_files,
            "screenshots": shot_files,
        }
        with open(os.path.join(fdir, "report.json"), "w", encoding="utf-8") as f:
            json.dump(report, f, ensure_ascii=False, indent=2)

        record_submit(ip, state)
        with open(os.path.join(STORE_DIR, "submissions.log"), "a", encoding="utf-8") as f:
            f.write(f"{datetime.now(timezone.utc).isoformat()} {fid} ip={ip} ver={data.get('version')} desc={desc[:80]!r}\n")

        self._reply(200, {"ok": True, "id": fid})


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8430
    os.makedirs(STORE_DIR, exist_ok=True)
    print(f"feedback server: POST {PATH} on 127.0.0.1:{port}, store={STORE_DIR}", flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
