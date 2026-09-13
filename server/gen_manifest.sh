#!/usr/bin/env bash
# 生成 /srv/repo/dlss5/manifest.json + manifest.sig（ECDSA P-256 签名）
# 用法: ./gen_manifest.sh <版本号如 1.1.0> <exe文件名如 DLSS5Patcher-v1.1.0.exe> [更新说明]
# 前提: 本目录下有签名私钥 manifest.key（chmod 600，勿入 git/勿外传）
set -euo pipefail
cd "$(dirname "$0")"

VER="${1:?用法: gen_manifest.sh <版本号> <exe文件名> [说明]}"
EXE_NAME="${2:?缺少 exe 文件名}"
NOTES="${3:-}"
EXE="app/$EXE_NAME"

[ -f "$EXE" ] || { echo "缺少 $EXE" >&2; exit 1; }
[ -f "manifest.key" ] || { echo "缺少签名私钥 manifest.key（上传到本目录并 chmod 600）" >&2; exit 1; }

python3 - "$VER" "$EXE" "$NOTES" <<'EOF' > manifest.json
import hashlib, json, os, sys
ver, exe, notes = sys.argv[1], sys.argv[2], sys.argv[3]
data = open(exe, "rb").read()
print(json.dumps({
    "version": ver,
    "exe": {"file": exe, "size": os.path.getsize(exe), "sha256": hashlib.sha256(data).hexdigest()},
    "notes": notes,
}, ensure_ascii=False, indent=2))
EOF

openssl dgst -sha256 -sign manifest.key -out manifest.sig manifest.json
chmod 644 manifest.json manifest.sig
echo "已生成 manifest.json + manifest.sig（版本 $VER）"
echo "客户端验签公钥指纹: $(openssl dgst -sha256 manifest.pub.der 2>/dev/null | cut -d' ' -f2 || echo '参考本地记录')"
