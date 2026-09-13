#!/usr/bin/env python3
"""本地开发/测试用静态服务器：支持 Range 断点续传（python http.server 原生不支持）。

URL 映射：/repo/<path> → server/<path>
用法: python tools/dev_server.py [端口，默认 8420]
"""
import os
import re
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "server")


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        self.handle_req(True)

    def do_HEAD(self):
        self.handle_req(False)

    def handle_req(self, send_body):
        path = self.path.split("?")[0]
        if path.startswith("/repo/"):
            rel = path[len("/repo/"):]
        else:
            rel = path.lstrip("/")
        fp = os.path.normpath(os.path.join(ROOT, rel))
        if not fp.startswith(ROOT) or not os.path.isfile(fp):
            self.send_error(404)
            return
        size = os.path.getsize(fp)
        rng = self.headers.get("Range")
        start, end = 0, size - 1
        partial = False
        if rng:
            m = re.match(r"bytes=(\d*)-(\d*)$", rng.strip())
            if m and (m.group(1) or m.group(2)):
                if m.group(1):
                    start = int(m.group(1))
                    if m.group(2): end = min(int(m.group(2)), size - 1)
                else:  # bytes=-N 后缀
                    start = max(0, size - int(m.group(2)))
                if start >= size:
                    self.send_response(416)
                    self.send_header("Content-Range", f"bytes */{size}")
                    self.end_headers()
                    return
                partial = True
        length = end - start + 1
        self.send_response(206 if partial else 200)
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Content-Length", str(length))
        self.send_header("Accept-Ranges", "bytes")
        if partial:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.end_headers()
        if not send_body:
            return
        with open(fp, "rb") as f:
            f.seek(start)
            remaining = length
            while remaining > 0:
                chunk = f.read(min(1 << 18, remaining))
                if not chunk:
                    break
                try:
                    self.wfile.write(chunk)
                except (BrokenPipeError, ConnectionResetError):
                    return
                remaining -= len(chunk)


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8420
    print(f"serving {ROOT} at http://127.0.0.1:{port}/repo/ (Range-capable)")
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
