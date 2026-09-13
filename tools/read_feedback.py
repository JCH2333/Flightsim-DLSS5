#!/usr/bin/env python3
"""读取服务器上的用户反馈（SSH 直连，凭据从环境变量读取，同 sshx.py）。

用法:
  python tools/read_feedback.py list [N]        # 列出最近 N 条（默认 20）
  python tools/read_feedback.py show <ID>       # 打印某条反馈的完整内容（含日志）
  python tools/read_feedback.py fetch <ID> [目录]  # 下载整条反馈（含截图）到本地
"""
import json
import os
import sys

import paramiko

HOST = os.environ.get("SSH_HOST", "47.109.31.236")
PORT = int(os.environ.get("SSH_PORT", "22"))
USER = os.environ.get("SSH_USER", "root")
PASS = os.environ["SSH_PASS"]
REMOTE_DIR = "/var/lib/dlss5-feedback"


def connect():
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, port=PORT, username=USER, password=PASS, timeout=20)
    return c


def run(c, cmd):
    _, out, err = c.exec_command(cmd)
    data = out.read().decode("utf-8", "replace")
    data += err.read().decode("utf-8", "replace")
    return data


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    cmd = sys.argv[1]
    c = connect()
    try:
        if cmd == "list":
            n = int(sys.argv[2]) if len(sys.argv) > 2 else 20
            ids = [i for i in run(c, f"ls -1t {REMOTE_DIR} | grep '^FB-' | head -{n}").split() if i]
            if not ids:
                print("（暂无反馈）")
            for fid in ids:
                raw = run(c, f"cat {REMOTE_DIR}/{fid}/report.json 2>/dev/null")
                try:
                    r = json.loads(raw)
                    desc = (r.get("description") or "").replace("\n", " ")[:60]
                    print(f"{fid} | v{r.get('version')} | {r.get('receivedAt','')[:19]} | gpu={r.get('gpu',{}).get('name','?')[:28]} | {desc}")
                except Exception as e:
                    print(f"{fid} | (读取失败: {e})")
            return 0

        fid = sys.argv[2]
        raw = run(c, f"cat {REMOTE_DIR}/{fid}/report.json 2>/dev/null")
        if not raw.strip():
            print(f"未找到反馈 {fid}")
            return 1
        r = json.loads(raw)
        print(f"编号      : {r['id']}")
        print(f"时间      : {r.get('receivedAt')}")
        print(f"版本      : {r.get('version')}   IP: {r.get('ip')}")
        print(f"系统      : {r.get('os')}   运行时: {r.get('runtime')}")
        gpu = r.get("gpu", {})
        print(f"显卡      : {gpu.get('name')} | 驱动 {gpu.get('driver')} | 显存 {gpu.get('vram')} | {gpu.get('gen')}")
        for g in r.get("games", []):
            print(f"游戏      : {g.get('name')} | {g.get('dir')} [{g.get('source')}] | {g.get('state')}")
        print(f"日志文件  : " + ", ".join(l.get("name", "?") for l in r.get("logs", [])))
        print(f"截图      : " + ", ".join(s.get("name", "?") for s in r.get("screenshots", [])) or "（无）")
        print("─" * 60)
        print("问题描述:")
        print(r.get("description", ""))
        if len(sys.argv) > 3 and sys.argv[3] == "--logs":
            print("─" * 60)
            for name in run(c, f"ls -1 {REMOTE_DIR}/{fid}/logs").split():
                print(f"\n===== logs/{name} =====")
                print(run(c, f"head -c 8000 '{REMOTE_DIR}/{fid}/logs/{name}'"))
        return 0
    finally:
        c.close()


if __name__ == "__main__":
    sys.exit(main())
