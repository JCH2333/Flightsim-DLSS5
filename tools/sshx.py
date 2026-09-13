#!/usr/bin/env python3
"""SSH 运维小工具（paramiko）。
用法:
  python tools/sshx.py run "<命令>"          # 执行命令，实时输出
  python tools/sshx.py put <本地> <远程>     # 上传文件
连接参数从环境变量读取: SSH_HOST / SSH_PORT / SSH_USER / SSH_PASS
"""
import os
import sys
import time

import paramiko

HOST = os.environ.get("SSH_HOST", "47.109.31.236")
PORT = int(os.environ.get("SSH_PORT", "22"))
USER = os.environ.get("SSH_USER", "root")
PASS = os.environ["SSH_PASS"]


def connect() -> paramiko.SSHClient:
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, port=PORT, username=USER, password=PASS, timeout=20, banner_timeout=20)
    return c


def run(c: paramiko.SSHClient, cmd: str) -> int:
    chan = c.get_transport().open_session()
    chan.set_combine_stderr(True)
    chan.exec_command(cmd)
    while True:
        while chan.recv_ready():
            sys.stdout.write(chan.recv(4096).decode("utf-8", "replace"))
            sys.stdout.flush()
        if chan.exit_status_ready() and not chan.recv_ready():
            break
        time.sleep(0.05)
    code = chan.recv_exit_status()
    return code


def put(c: paramiko.SSHClient, local: str, remote: str):
    sftp = c.open_sftp()

    def progress(done, total):
        pct = done * 100 // total
        sys.stdout.write(f"\r  {os.path.basename(local)} {pct}% ({done / 1048576:.0f}/{total / 1048576:.0f} MB)")
        sys.stdout.flush()
        if done >= total:
            sys.stdout.write("\n")

    sftp.put(local, remote, callback=progress)
    sftp.close()
    print(f"  uploaded: {remote}")


def main() -> int:
    cmd = sys.argv[1]
    c = connect()
    try:
        if cmd == "run":
            return run(c, sys.argv[2])
        if cmd == "put":
            put(c, sys.argv[2], sys.argv[3])
            return 0
        print(__doc__)
        return 2
    finally:
        c.close()


if __name__ == "__main__":
    sys.exit(main())
