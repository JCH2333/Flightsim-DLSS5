#!/usr/bin/env python3
"""DLSS5 站点 API 服务（127.0.0.1:8430，nginx 8420 反代 /repo/dlss5/api/ 与 /repo/dlss5/feedback）。

公开接口（免登录）:
  POST /repo/dlss5/feedback                      反馈提交（限流同旧版，v1.2.0 客户端兼容；新增可选 username，
                                                 返回新增 feedbackCode 短码，旧客户端仍读 id 不受影响）
  GET  /repo/dlss5/api/feedback/query/<code>     凭反馈码查进度（每 IP 每天 60 次）
                                                 {code:200,data:{statusCode:PENDING|PROCESSED|EXPIRED|NOT_FOUND,
                                                 createdAt,username,adminReply}}（同 GSX：已完成的反馈保留 30 天后查为 EXPIRED）
  GET  /repo/dlss5/api/announcements?page&size   已发布公告 {code:200,data:{content:[...]}}
  GET  /repo/dlss5/api/announcements/popup       弹窗公告   {code:200,data:[...]}
  GET  /repo/dlss5/api/assets/sponsor-qr         加密赞助码信封（nginx 直接回静态文件，见 nginx-repo.conf）

管理接口（Authorization: Bearer <token>，token 由 /api/auth/login 获得）:
  POST   /repo/dlss5/api/auth/login                       {username,password}
  GET    /repo/dlss5/api/admin/announcements?status&keyword
  POST   /repo/dlss5/api/admin/announcements              新建（DRAFT）
  PUT    /repo/dlss5/api/admin/announcements/<id>         编辑
  DELETE /repo/dlss5/api/admin/announcements/<id>
  POST   /repo/dlss5/api/admin/announcements/<id>/publish 发布
  POST   /repo/dlss5/api/admin/announcements/upload-image multipart file → imageUrl
  POST   /repo/dlss5/api/admin/assets/sponsor-qr          multipart file，替换收款码并重新加密
  GET    /repo/dlss5/api/admin/feedback?page&size&status
  GET    /repo/dlss5/api/admin/feedback/unread-count
  GET    /repo/dlss5/api/admin/feedback/<id>              详情（自动标记已读）
  PUT    /repo/dlss5/api/admin/feedback/<id>/status       {status, remark}

存储:
  /var/lib/dlss5-feedback/<FB-ID>/...  反馈（旧有）
  /var/lib/dlss5-cms/                  secret.json(qr_key+管理员口令, 600) / announcements.json /
                                       state.json(tokens+seq) / sponsor-qr.json(信封缓存) / sponsor-qr-raw.*

部署: systemd (dlss5-api.service)；本机测试:
    FEEDBACK_DIR=/tmp/fb CMS_DIR=/tmp/cms python3 dlss5_api.py 8430
"""
import base64
import hashlib
import hmac
import json
import os
import re
import secrets
import socketserver
import sys
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs, unquote

try:  # 可选：仅服务端赞助码上传加密需要（pip3 install pycryptodome）；缺失时回退离线工具方案
    from Crypto.Cipher import AES as _AES
except Exception:  # noqa: BLE001
    _AES = None


class ThreadingHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    """兼容 Python 3.6（老系统无 http.server.ThreadingHTTPServer）。"""
    daemon_threads = True
    allow_reuse_address = True


STORE_DIR = os.environ.get("FEEDBACK_DIR", "/var/lib/dlss5-feedback")
CMS_DIR = os.environ.get("CMS_DIR", "/var/lib/dlss5-cms")
STATE_PATH = os.path.join(CMS_DIR, "state.json")
ANN_PATH = os.path.join(CMS_DIR, "announcements.json")
SECRET_PATH = os.path.join(CMS_DIR, "secret.json")
SPONSOR_ENV_PATH = os.path.join(CMS_DIR, "sponsor-qr.json")
ASSET_WEB_DIR = os.environ.get("ASSET_WEB_DIR", "/srv/repo/dlss5/assets/announcements")

MAX_BODY = 48 * 1024 * 1024
MAX_IMAGE = 2 * 1024 * 1024           # 赞助码/公告图片上限（与 GSX 一致：赞助码 ≤2MB）
TOKEN_TTL = 30 * 24 * 3600   # 管理端登录有效期 30 天（旧值 48h 过期后前端曾卡在加载中）
FB_PATH = "/repo/dlss5/feedback"
DAILY_LIMIT = 10
INTERVAL_SECONDS = 600
MAX_DESC = 8000
MAX_LOGS = 20
MAX_LOG_CHARS = 400_000
MAX_SHOTS = 8
MAX_USERNAME = 50
QUERY_DAILY_LIMIT = 60          # 反馈码查询：每 IP 每天 60 次（与 GSX 一致）
FB_RETAIN_DAYS = 30             # 已完成反馈的保留期，超期后查询返回 EXPIRED（与 GSX 一致）
FB_CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"   # 去掉易混的 I/O/0/1

API = "/repo/dlss5/api"


# ───────────────────────────── 存取 ─────────────────────────────

def load_json(path, default):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return default


def save_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)
    os.replace(tmp, path)


def load_state():
    return load_json(STATE_PATH, {"tokens": {}, "seq": 0})


def save_state(state):
    save_json(STATE_PATH, state)


def load_secret():
    sec = load_json(SECRET_PATH, None)
    if sec is None:
        sec = {
            "qr_key": base64.b64encode(os.urandom(32)).decode(),
            "admin_user": os.environ.get("ADMIN_USER", "admin"),
            "admin_pass": os.environ.get("ADMIN_PASS", "dlss5-admin"),
        }
        save_json(SECRET_PATH, sec)
        try:
            os.chmod(SECRET_PATH, 0o600)
        except Exception:
            pass
    return sec


def now_iso():
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")


# ───────────────────────────── 反馈（旧有逻辑） ─────────────────────────────

def check_rate(ip, state):
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    now = time.time()
    rec = state.get("ips", {}).get(ip)
    if rec:
        if rec.get("day") != today:
            rec["day"], rec["count"] = today, 0
        if now - rec.get("last", 0) < INTERVAL_SECONDS:
            wait = int((INTERVAL_SECONDS - (now - rec.get("last", 0))) / 60) + 1
            return "提交太频繁，请约 %d 分钟后再试（每 10 分钟限 1 条）。" % wait
        if rec.get("count", 0) >= DAILY_LIMIT:
            return "今日反馈已达上限（每天 10 条），请明天再试或在粉丝群直接反馈。"
    return None


def record_submit(ip, state):
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    rec = state.setdefault("ips", {}).setdefault(ip, {})
    if rec.get("day") != today:
        rec["day"], rec["count"] = today, 0
    rec["count"] = rec.get("count", 0) + 1
    rec["last"] = time.time()
    save_state(state)


def gen_fb_code(state):
    """生成 FB-XXXXXX 短反馈码（去易混字符，查重后登记到 state.codes）。"""
    codes = state.setdefault("codes", {})
    for _ in range(50):
        code = "FB-" + "".join(secrets.choice(FB_CODE_ALPHABET) for _ in range(6))
        if code not in codes:
            return code
    raise RuntimeError("feedback code space exhausted")


def fb_query_status(meta, report):
    """GSX 语义：未处理=PENDING；已处理=PROCESSED；处理后超过保留期=EXPIRED。"""
    if meta.get("status") != "completed":
        return "PENDING"
    done = meta.get("processedAt") or report.get("receivedAt") or ""
    try:
        done_ts = datetime.fromisoformat(done).timestamp() if done else 0
    except Exception:
        done_ts = 0
    if done_ts and time.time() - done_ts > FB_RETAIN_DAYS * 86400:
        return "EXPIRED"
    return "PROCESSED"


def check_query_rate(ip, state):
    today = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    rec = state.setdefault("qips", {}).setdefault(ip, {})
    if rec.get("day") != today:
        rec["day"], rec["count"] = today, 0
    if rec.get("count", 0) >= QUERY_DAILY_LIMIT:
        return "今日查询次数已达上限（每天 %d 次），请明天再试。" % QUERY_DAILY_LIMIT
    rec["count"] = rec.get("count", 0) + 1
    save_state(state)
    return None


# ───────────────────────────── 公告 ─────────────────────────────

def public_announcement(a):
    """对客户端只输出已发布字段；展示时间用发布时间。"""
    return {
        "id": a["id"],
        "title": a["title"],
        "content": a["content"],
        "imageUrl": a.get("imageUrl") or None,
        "popup": bool(a.get("popup")),
        "pinned": bool(a.get("pinned")),
        "category": a.get("category", "global"),
        "createdByUsername": a.get("createdByUsername", ""),
        "createdAt": a.get("publishedAt") or a.get("createdAt") or "",
    }


def published_sorted():
    rows = [a for a in load_json(ANN_PATH, []) if a.get("status") == "PUBLISHED"]
    rows.sort(key=lambda a: a.get("publishedAt") or a.get("createdAt") or "", reverse=True)   # 时间倒序
    rows.sort(key=lambda a: not a.get("pinned", False), reverse=False)                        # 置顶优先（稳定排序）
    return rows


def admin_sorted(rows, status=None, keyword=None):
    out = []
    for a in rows:
        if status and a.get("status") != status:
            continue
        if keyword and keyword.lower() not in str(a.get("title", "")).lower():
            continue
        out.append(a)
    out.sort(key=lambda a: (a.get("createdAt") or "", a.get("id", 0)), reverse=True)
    return out


def envelope_ok(handler, payload, code=200):
    handler._reply(200, {"code": code, "message": "ok", "data": payload})


# ───────────────────────────── 赞助码加密（与服务端 qr-key 对称） ─────────────────────────────

def sponsor_envelope(png_or_jpg, qr_key_b64, marker):
    if _AES is None:
        return None
    key = base64.b64decode(qr_key_b64)
    nonce = os.urandom(12)
    cipher = _AES.new(key, _AES.MODE_GCM, nonce=nonce, mac_len=16)
    ct, tag = cipher.encrypt_and_digest(png_or_jpg)
    return {
        "scheme": "aes-256-gcm",
        "nonce": base64.b64encode(nonce).decode(),
        "payload": base64.b64encode(ct + tag).decode(),
        "sha256": hashlib.sha256(png_or_jpg).hexdigest(),
        "marker": marker,
    }


def detect_image(raw):
    if len(raw) >= 4 and raw[:2] == b"\xff\xd8":
        return "jpg"
    if len(raw) >= 4 and raw[:4] == b"\x89PNG":
        return "png"
    return None


# ───────────────────────────── multipart 解析（py3.6 stdlib） ─────────────────────────────

def parse_multipart(handler):
    """返回 {name: bytes}；只取第一个文件段，字段名不限。"""
    import io
    import cgi
    if not hasattr(handler, "_raw_body"):
        handler._read_body()   # 上传端点先读原始 body 再解析
    if not hasattr(handler, "_raw_body"):
        handler._raw_body = b""
    env = {
        "REQUEST_METHOD": "POST",
        "CONTENT_TYPE": handler.headers.get("Content-Type", ""),
        "CONTENT_LENGTH": handler.headers.get("Content-Length", "0"),
    }
    fs = cgi.FieldStorage(fp=io.BytesIO(handler._raw_body), environ=env, keep_blank_values=True)
    out = {}
    for key in fs.keys():
        item = fs[key]
        if isinstance(item, list):
            item = item[0] if item else None
        if item is not None:
            out[key] = item.file.read() if item.file else item.value.encode("utf-8")
    return out


# ───────────────────────────── HTTP ─────────────────────────────

class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):  # 静默访问日志
        pass

    def _reply(self, code, payload):
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)
        self.close_connection = True

    def _read_body(self):
        length = int(self.headers.get("Content-Length", "0") or 0)
        if length <= 0 or length > MAX_BODY:
            return None
        self._raw_body = self.rfile.read(length)
        try:
            return json.loads(self._raw_body.decode("utf-8"))
        except Exception:
            return None

    def _auth_user(self):
        auth = self.headers.get("Authorization", "")
        if not auth.startswith("Bearer "):
            return None
        token = auth[7:].strip()
        state = load_state()
        rec = state.get("tokens", {}).get(token)
        if not rec or rec.get("expires", 0) < time.time():
            return None
        return rec.get("username", "admin")

    def do_GET(self):
        try:
            self._handle_get()
        except Exception as e:  # noqa: BLE001
            self._reply(500, {"code": 500, "message": "internal error: %s" % e})

    def do_POST(self):
        try:
            self._handle_post()
        except Exception as e:  # noqa: BLE001
            try:
                self._reply(500, {"code": 500, "message": "internal error: %s" % e})
            except Exception:
                pass

    def do_PUT(self):
        self.do_POST()

    def do_DELETE(self):
        try:
            self._handle_delete()
        except Exception as e:  # noqa: BLE001
            self._reply(500, {"code": 500, "message": "internal error: %s" % e})

    # ── GET ──

    def _handle_get(self):
        path = unquote(urlparse(self.path).path)
        qs = parse_qs(urlparse(self.path).query)

        if path == FB_PATH:
            return self._reply(405, {"ok": False, "error": "method not allowed"})

        # 凭反馈码查询处理进度（公开；每 IP 每天 60 次）。兼容长编号（旧版已发给用户）与短码
        m = re.match(r"^%s/feedback/query/([A-Za-z0-9\-]+)$" % re.escape(API), path)
        if m:
            return self._feedback_query(m.group(1))

        if path == API + "/announcements":
            page = int((qs.get("page") or ["0"])[0] or 0)
            size = min(100, int((qs.get("size") or ["50"])[0] or 50))
            rows = published_sorted()
            start = max(0, page * size)
            return envelope_ok(self, {
                "content": [public_announcement(a) for a in rows[start:start + size]],
                "totalItems": len(rows), "page": page, "size": size,
            })

        if path == API + "/announcements/popup":
            rows = [a for a in published_sorted() if a.get("popup")]
            return envelope_ok(self, [public_announcement(a) for a in rows])

        # 生产环境由 nginx 精确匹配直接回静态信封文件；此处兜底便于本机联调
        if path == API + "/assets/sponsor-qr":
            envl = load_json(SPONSOR_ENV_PATH, None)
            if envl is None:
                return self._reply(404, {"code": 404, "message": "sponsor qr not configured"})
            body = json.dumps(envl, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-cache")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(body)
            self.close_connection = True
            return

        if path == API + "/admin/announcements":
            if not self._auth_user():
                return self._reply(401, {"code": 401, "message": "unauthorized"})
            status = (qs.get("status") or [""])[0]
            keyword = (qs.get("keyword") or [""])[0]
            rows = admin_sorted(load_json(ANN_PATH, []), status or None, keyword or None)
            return envelope_ok(self, {"content": rows, "totalItems": len(rows)})

        if path == API + "/admin/feedback/unread-count":
            if not self._auth_user():
                return self._reply(401, {"code": 401, "message": "unauthorized"})
            return envelope_ok(self, {"count": _fb_scan()[1]})

        m = re.match(r"^%s/admin/feedback/([^/]+)$" % re.escape(API), path)
        if m:
            if not self._auth_user():
                return self._reply(401, {"code": 401, "message": "unauthorized"})
            return self._fb_detail(m.group(1))

        if path == API + "/admin/feedback":
            if not self._auth_user():
                return self._reply(401, {"code": 401, "message": "unauthorized"})
            page = int((qs.get("page") or ["0"])[0] or 0)
            size = min(100, int((qs.get("size") or ["20"])[0] or 20))
            status = (qs.get("status") or [""])[0]
            summaries, _unread = _fb_scan()
            if status:
                summaries = [s for s in summaries if s.get("status") == status]
            start = max(0, page * size)
            return envelope_ok(self, {
                "content": summaries[start:start + size],
                "totalItems": len(summaries), "unread": _unread, "page": page, "size": size,
            })

        return self._reply(404, {"code": 404, "message": "not found"})

    # ── POST / PUT ──

    def _handle_post(self):
        path = unquote(urlparse(self.path).path)
        ctype = self.headers.get("Content-Type", "")

        if path == FB_PATH:
            return self._feedback_submit()

        if path == API + "/auth/login":
            body = self._read_body() or {}
            sec = load_secret()
            user = str(body.get("username", ""))
            pw = str(body.get("password", ""))
            ok = hmac.compare_digest(user, sec.get("admin_user", "")) and \
                hmac.compare_digest(hashlib.sha256(pw.encode()).hexdigest(), hashlib.sha256(sec.get("admin_pass", "").encode()).hexdigest())
            if not ok:
                return self._reply(401, {"code": 401, "message": "用户名或密码错误"})
            token = secrets.token_hex(32)
            state = load_state()
            now = time.time()
            state.setdefault("tokens", {})
            for t in list(state["tokens"].keys()):     # 清理过期 token
                if state["tokens"][t].get("expires", 0) < now:
                    del state["tokens"][t]
            state["tokens"][token] = {"username": user, "expires": now + TOKEN_TTL}
            save_state(state)
            return envelope_ok(self, {"token": token, "username": user})

        # ── 以下全部需要管理员 ──
        user = self._auth_user()
        if not user:
            return self._reply(401, {"code": 401, "message": "unauthorized"})

        if path == API + "/admin/announcements":
            body = self._read_body() or {}
            title = str(body.get("title", "")).strip()
            content = str(body.get("content", "")).strip()
            category = str(body.get("category", "global")).strip()
            if not title or not content:
                return self._reply(400, {"code": 400, "message": "标题与内容不能为空"})
            if not (category == "global" or category == "software" or category.startswith("patch:")):
                return self._reply(400, {"code": 400, "message": "公告类别非法"})
            state = load_state()
            ann = {
                "id": int(state.get("seq", 0)) + 1,
                "title": title[:200],
                "content": content[:10000],
                "imageUrl": str(body.get("imageUrl", "") or "")[:500],
                "popup": body.get("popup") is True,
                "pinned": body.get("pinned") is True,
                "category": category,
                "status": "DRAFT",
                "createdByUsername": user,
                "createdAt": now_iso(),
                "updatedAt": now_iso(),
                "publishedAt": None,
            }
            state["seq"] = ann["id"]
            save_state(state)
            rows = load_json(ANN_PATH, [])
            rows.append(ann)
            save_json(ANN_PATH, rows)
            return envelope_ok(self, ann)

        if path == API + "/admin/announcements/upload-image":
            raw = self._admin_upload_image()
            return raw

        if path == API + "/admin/assets/sponsor-qr":
            return self._sponsor_upload()

        m = re.match(r"^%s/admin/announcements/(\d+)/publish$" % re.escape(API), path)
        if m:
            rows = load_json(ANN_PATH, [])
            for a in rows:
                if a.get("id") == int(m.group(1)):
                    a["status"] = "PUBLISHED"
                    a["publishedAt"] = a.get("publishedAt") or now_iso()
                    a["updatedAt"] = now_iso()
                    save_json(ANN_PATH, rows)
                    return envelope_ok(self, a)
            return self._reply(404, {"code": 404, "message": "公告不存在"})

        m = re.match(r"^%s/admin/announcements/(\d+)$" % re.escape(API), path)
        if m:
            aid = int(m.group(1))
            body = self._read_body() or {}
            rows = load_json(ANN_PATH, [])
            for a in rows:
                if a.get("id") == aid:
                    if "title" in body:
                        a["title"] = str(body.get("title", "")).strip()[:200] or a["title"]
                    if "content" in body:
                        a["content"] = str(body.get("content", "")).strip()[:10000] or a["content"]
                    if "imageUrl" in body:
                        a["imageUrl"] = str(body.get("imageUrl", "") or "")[:500]
                    if "popup" in body:
                        a["popup"] = body.get("popup") is True
                    if "pinned" in body:
                        a["pinned"] = body.get("pinned") is True
                    if "category" in body:
                        c = str(body.get("category", "")).strip()
                        if c == "global" or c == "software" or c.startswith("patch:"):
                            a["category"] = c
                    a["updatedAt"] = now_iso()
                    save_json(ANN_PATH, rows)
                    return envelope_ok(self, a)
            return self._reply(404, {"code": 404, "message": "公告不存在"})

        m = re.match(r"^%s/admin/feedback/([^/]+)/status$" % re.escape(API), path)
        if m:
            fid = m.group(1)
            fdir = os.path.join(STORE_DIR, fid)
            if not os.path.isdir(fdir):
                return self._reply(404, {"code": 404, "message": "反馈不存在"})
            body = self._read_body() or {}
            meta = load_json(os.path.join(fdir, "meta.json"), {})
            status = str(body.get("status", ""))
            if status not in ("pending", "processing", "completed", ""):
                return self._reply(400, {"code": 400, "message": "状态非法"})
            if status:
                meta["status"] = status
                if status == "completed":
                    meta["processedAt"] = now_iso()   # 反馈码查询的 EXPIRED 保留期从此刻起算
                else:
                    meta["processedAt"] = ""
            meta["remark"] = str(body.get("remark", meta.get("remark", "")))[:2000]
            save_json(os.path.join(fdir, "meta.json"), meta)
            return envelope_ok(self, meta)

        return self._reply(404, {"code": 404, "message": "not found"})

    def _admin_upload_image(self):
        parts = parse_multipart(self)
        raw = None
        for v in parts.values():
            if v:
                raw = v
                break
        if not raw:
            return self._reply(400, {"code": 400, "message": "缺少文件"})
        if len(raw) > MAX_IMAGE:
            return self._reply(400, {"code": 400, "message": "图片需 ≤ 2MB"})
        ext = detect_image(raw)
        if not ext:
            return self._reply(400, {"code": 400, "message": "仅支持 PNG/JPEG"})
        os.makedirs(ASSET_WEB_DIR, exist_ok=True)
        name = "ann-%s-%s.%s" % (datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S"), secrets.token_hex(3), ext)
        with open(os.path.join(ASSET_WEB_DIR, name), "wb") as f:
            f.write(raw)
        return envelope_ok(self, {"imageUrl": "/repo/dlss5/assets/announcements/" + name})

    def _sponsor_upload(self):
        sec = load_secret()
        parts = parse_multipart(self)
        raw = None
        for v in parts.values():
            if v:
                raw = v
                break
        if not raw:
            return self._reply(400, {"code": 400, "message": "缺少文件"})
        if len(raw) > MAX_IMAGE:
            return self._reply(400, {"code": 400, "message": "赞助码需 ≤ 2MB"})
        if not detect_image(raw):
            return self._reply(400, {"code": 400, "message": "仅支持 PNG/JPEG"})
        with open(os.path.join(CMS_DIR, "sponsor-qr-raw." + detect_image(raw)), "wb") as f:
            f.write(raw)
        envl = sponsor_envelope(raw, sec["qr_key"], {
            "project": "DLSS5Patcher", "type": "wechat-sponsor-qr",
            "owner": "JCH", "note": "", "uploadedAt": now_iso(),
        })
        if envl is None:
            return self._reply(500, {"code": 500, "message": "服务器缺少 pycryptodome，无法加密（pip3 install pycryptodome 或用离线工具生成信封）"})
        save_json(SPONSOR_ENV_PATH, envl)
        return envelope_ok(self, {"ok": True})

    # ── DELETE ──

    def _handle_delete(self):
        path = unquote(urlparse(self.path).path)
        if not self._auth_user():
            return self._reply(401, {"code": 401, "message": "unauthorized"})
        m = re.match(r"^%s/admin/announcements/(\d+)$" % re.escape(API), path)
        if m:
            aid = int(m.group(1))
            rows = [a for a in load_json(ANN_PATH, []) if a.get("id") != aid]
            save_json(ANN_PATH, rows)
            return envelope_ok(self, {"deleted": aid})
        return self._reply(404, {"code": 404, "message": "not found"})

    # ── 反馈 ──

    def _feedback_submit(self):
        ip = self.headers.get("X-Real-IP") or self.client_address[0]
        state = load_state()
        reason = check_rate(ip, state)     # 读大 body 前廉价拒绝
        if reason:
            return self._reply(429, {"ok": False, "error": reason})

        body = self._read_body()
        if body is None:
            return self._reply(413, {"ok": False, "error": "payload too large"})
        try:
            if not isinstance(body, dict):
                raise ValueError()
        except Exception:
            return self._reply(400, {"ok": False, "error": "invalid JSON"})

        desc = str(body.get("description", "") or "").strip()
        username = str(body.get("username", "") or "").strip()[:MAX_USERNAME]
        logs = body.get("logs", [])
        shots = body.get("screenshots", [])
        if not desc and not logs:
            return self._reply(400, {"ok": False, "error": "请填写问题描述后再提交。"})
        if len(desc) > MAX_DESC:
            return self._reply(400, {"ok": False, "error": "问题描述过长。"})
        if not isinstance(logs, list) or len(logs) > MAX_LOGS:
            return self._reply(400, {"ok": False, "error": "日志条目非法。"})
        if not isinstance(shots, list) or len(shots) > MAX_SHOTS:
            return self._reply(400, {"ok": False, "error": "截图数量超限。"})

        fid = "FB-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + "-" + secrets.token_hex(2)
        code = gen_fb_code(state)   # 短反馈码，供用户查询进度
        fdir = os.path.join(STORE_DIR, fid)
        os.makedirs(os.path.join(fdir, "logs"), exist_ok=True)
        os.makedirs(os.path.join(fdir, "shots"), exist_ok=True)

        def safe_name(name, default):
            name = re.sub(r"[^0-9A-Za-z._\- \u4e00-\u9fff（）()]", "_", str(name or default))[:120]
            return name or default

        log_files = []
        for i, log in enumerate(logs):
            name = safe_name(log.get("name"), "log-%d.txt" % i)
            content = str(log.get("content", "") or "")[:MAX_LOG_CHARS]
            fn = "log-%02d-%s" % (i, name)
            if not fn.endswith(".txt"):
                fn += ".txt"
            with open(os.path.join(fdir, "logs", fn), "w", encoding="utf-8", errors="replace") as f:
                f.write(content)
            log_files.append({"name": name, "size": log.get("size"), "truncated": bool(log.get("truncated"))})

        shot_files = []
        for i, shot in enumerate(shots):
            name = safe_name(shot.get("name"), "shot-%d.png" % i)
            try:
                raw = base64.b64decode(shot.get("data", "") or "")
            except Exception:
                continue
            if not raw or len(raw) > 16 * 1024 * 1024:
                continue
            fn = "shot-%02d-%s" % (i, name)
            with open(os.path.join(fdir, "shots", fn), "wb") as f:
                f.write(raw)
            shot_files.append({"name": name, "size": len(raw)})

        report = {
            "id": fid,
            "code": code,
            "username": username,
            "receivedAt": datetime.now(timezone.utc).isoformat(),
            "ip": ip,
            "app": body.get("app"),
            "version": body.get("version"),
            "os": body.get("os"),
            "runtime": body.get("runtime"),
            "gpu": body.get("gpu"),
            "games": body.get("games", []),
            "description": desc,
            "logs": log_files,
            "screenshots": shot_files,
        }
        save_json(os.path.join(fdir, "report.json"), report)
        save_json(os.path.join(fdir, "meta.json"), {"read": False, "status": "pending", "remark": ""})
        state["codes"][code] = fid
        save_state(state)

        record_submit(ip, state)
        with open(os.path.join(STORE_DIR, "submissions.log"), "a", encoding="utf-8") as f:
            f.write("%s %s code=%s ip=%s ver=%s user=%r desc=%r\n" % (
                datetime.now(timezone.utc).isoformat(), fid, code, ip, body.get("version"), username, desc[:80]))

        self._reply(200, {"ok": True, "id": fid, "feedbackCode": code})

    def _feedback_query(self, raw):
        """凭反馈码查进度：statusCode=PENDING|PROCESSED|EXPIRED|NOT_FOUND（NOT_FOUND 也是 200，与 GSX 一致）。"""
        ip = self.headers.get("X-Real-IP") or self.client_address[0]
        state = load_state()
        reason = check_query_rate(ip, state)
        if reason:
            return self._reply(429, {"code": 429, "message": reason})

        code = raw.strip().upper()
        fid = state.get("codes", {}).get(code)
        if fid is None and re.match(r"^FB-[0-9]{8}-[0-9]{6}-[0-9A-F]{4}$", code):
            fid = code.lower() if os.path.isdir(os.path.join(STORE_DIR, code.lower())) else code
            if not os.path.isdir(os.path.join(STORE_DIR, fid)):
                fid = None
        fdir = os.path.join(STORE_DIR, fid) if fid else None
        if not fdir or not os.path.isdir(fdir):
            return envelope_ok(self, {"statusCode": "NOT_FOUND"})

        report = load_json(os.path.join(fdir, "report.json"), {})
        meta = load_json(os.path.join(fdir, "meta.json"), {})
        return envelope_ok(self, {
            "statusCode": fb_query_status(meta, report),
            "createdAt": report.get("receivedAt", ""),
            "username": str(report.get("username", "") or ""),
            "adminReply": str(meta.get("remark", "") or ""),
        })

    def _fb_detail(self, fid):
        if not re.match(r"^FB-[0-9A-Za-z\-]+$", fid):
            return self._reply(404, {"code": 404, "message": "反馈不存在"})
        fdir = os.path.join(STORE_DIR, fid)
        if not os.path.isdir(fdir):
            return self._reply(404, {"code": 404, "message": "反馈不存在"})
        report = load_json(os.path.join(fdir, "report.json"), {})
        meta = load_json(os.path.join(fdir, "meta.json"), {})

        logs = []
        for lf in sorted(os.listdir(os.path.join(fdir, "logs")))[-20:]:
            try:
                with open(os.path.join(fdir, "logs", lf), "r", encoding="utf-8", errors="replace") as f:
                    logs.append({"name": lf, "content": f.read()[:400_000]})
            except Exception:
                pass

        shots = []
        sdir = os.path.join(fdir, "shots")
        for sf in sorted(os.listdir(sdir))[-8:]:
            try:
                with open(os.path.join(sdir, sf), "rb") as f:
                    raw = f.read()
                mime = "image/png" if raw[:4] == b"\x89PNG" else "image/jpeg"
                shots.append({"name": sf, "dataUrl": "data:%s;base64,%s" % (mime, base64.b64encode(raw).decode())})
            except Exception:
                pass

        meta["read"] = True
        save_json(os.path.join(fdir, "meta.json"), meta)

        report["logsFull"] = logs
        report["shotsData"] = shots
        report["status"] = meta.get("status", "pending")
        report["remark"] = meta.get("remark", "")
        return envelope_ok(self, report)


def _fb_scan():
    """返回 (摘要列表(新→旧), 未读数)。"""
    out = []
    unread = 0
    if not os.path.isdir(STORE_DIR):
        return out, unread
    for fid in os.listdir(STORE_DIR):
        fdir = os.path.join(STORE_DIR, fid)
        if not fid.startswith("FB-") or not os.path.isdir(fdir):
            continue
        report = load_json(os.path.join(fdir, "report.json"), {})
        meta = load_json(os.path.join(fdir, "meta.json"), {})
        if not meta.get("read"):
            unread += 1
        desc = str(report.get("description", "") or "")
        gpu = report.get("gpu") or {}
        out.append({
            "id": fid,
            "code": report.get("code", ""),
            "username": str(report.get("username", "") or ""),
            "receivedAt": report.get("receivedAt", ""),
            "ip": report.get("ip", ""),
            "version": report.get("version", ""),
            "os": report.get("os", ""),
            "gpu": gpu.get("name", "") if isinstance(gpu, dict) else "",
            "description": desc[:200],
            "status": meta.get("status", "pending"),
            "remark": meta.get("remark", ""),
            "read": bool(meta.get("read")),
            "logCount": len(report.get("logs", [])),
            "shotCount": len(report.get("screenshots", [])),
        })
    out.sort(key=lambda s: s["id"], reverse=True)
    return out, unread


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8430
    os.makedirs(STORE_DIR, exist_ok=True)
    os.makedirs(CMS_DIR, exist_ok=True)
    load_secret()
    print("dlss5 api: POST %s | GET %s/announcements[/popup] on 127.0.0.1:%d" % (FB_PATH, API, port), flush=True)
    print("  feedback=%s cms=%s pycryptodome=%s" % (STORE_DIR, CMS_DIR, _AES is not None), flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
