# DLSS5 分发服务器部署指南（47.109.31.236 / Alibaba Cloud Linux 3）

目标：`http://47.109.31.236:8420/repo/dlss5/` 提供包体与自更新清单。
域名 jianchihu.online 备案下发后，在本配置基础上追加 443+TLS 站点，客户端新版把域名源置顶。

## 目录结构

```
/srv/repo/
  dlss5/
    manifest.json          # 版本清单（gen_manifest.sh 生成）
    manifest.sig           # ECDSA P-256 签名（客户端内嵌公钥验签）
    manifest.key           # 签名私钥（chmod 600，绝不入 git / 不外传）
    packages/
      dlss-unlocked-standalone-DLSSNR-v0.7.6.zip   (440MB, MSFS)
      dlss5-feeder-kit-v0.13.1-beta.1-dfc-1.4.8.zip (154MB, XP12)
    app/
      DLSS5Patcher-v1.1.0.exe
  <future-app>/...         # 预留：其他软件按同样结构扩展
```

## 首次部署步骤（SSH 登录后）

```bash
# 1. nginx（AL3 官方源自带）
sudo dnf install -y nginx
sudo cp repo.conf /etc/nginx/conf.d/repo.conf     # 本目录的 nginx-repo.conf
sudo nginx -t && sudo systemctl enable --now nginx

# 2. 目录与文件（本机上传 server/dlss5/ 整个目录）
sudo mkdir -p /srv/repo
sudo rsync -av dlss5/ /srv/repo/dlss5/
sudo chcon -R -t httpd_sys_content_t /srv/repo 2>/dev/null || true   # SELinux（若启用）
sudo chmod 600 /srv/repo/dlss5/manifest.key

# 3. 生成清单（版本号与 EXE 名按实际）
cd /srv/repo/dlss5 && chmod +x gen_manifest.sh && ./gen_manifest.sh 1.1.0 DLSS5Patcher-v1.1.0.exe "首个服务器分发版"

# 4. 阿里云控制台 → 安全组 → 入方向规则：放行 TCP 8420（源 0.0.0.0/0）

# 5. 验证（本机或任意外网机器）
curl -I http://47.109.31.236:8420/repo/dlss5/manifest.json
curl -r 0-1023 -o /dev/null -w "%{http_code}\n" http://47.109.31.236:8420/repo/dlss5/packages/dlss5-feeder-kit-v0.13.1-beta.1-dfc-1.4.8.zip   # 应输出 206
```

## 日常发版

1. `dotnet publish` 出新版 EXE → 上传到 `app/DLSS5Patcher-vX.Y.Z.exe`（旧版本保留，便于回滚）
2. `./gen_manifest.sh X.Y.Z DLSS5Patcher-vX.Y.Z.exe "更新说明"`（自动计算哈希并签名）
3. GitHub Release 同步发一份（客户端备用源）；包体文件变化时同步 `packages/`
4. 客户端清单缓存 5 分钟（nginx Cache-Control），无需重启任何服务

## 客户端源顺序（v1.1.0）

1. `http://47.109.31.236:8420/repo/dlss5`（主源）
2. `https://github.com/JCH2333/Flightsim-DLSS5/releases/latest/download`（备用）
3. `https://gh-proxy.com/` 前缀镜像（最后）

域名备案下发后：第 0 源变为 `https://jianchihu.online/repo/dlss5`（新版客户端写入；旧版继续用 IP）。
应急覆盖：客户端配置文件 `%LOCALAPPDATA%\DLSS5Patcher\config.txt` 加 `repo=<基地址>`，或环境变量 `DLSS5_REPO`。
