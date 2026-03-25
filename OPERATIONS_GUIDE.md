# FamilyTree 操作说明

## 1. 环境要求

- .NET SDK 8.0+
- macOS / Windows / Linux 均可

## 2. 启动项目

```bash
cd "/Users/admin/RiderProjects/FamilyTree"
dotnet build
dotnet run
```

启动后访问（按本机配置）：

- `http://localhost:5009`
- 或 `https://localhost:7213`

## 3. 首次运行与测试数据

系统有两层测试数据保障：

1. EF Core `HasData` 固定样例族谱
2. 若数据库为空，启动时自动插入兜底演示数据

如果你希望“重置到干净测试状态”，可删除数据库文件后重启：

```bash
cd "/Users/admin/RiderProjects/FamilyTree"
rm -f familytree.db familytree.db-shm familytree.db-wal
dotnet run
```

首次启动会自动创建默认管理员：`admin / 123456`。

## 4. 页面功能操作

### 4.1 树图浏览

- 鼠标拖拽：平移
- 工具栏“放大/缩小/重置”：缩放视图（支持更大放大倍数）
- 点击成员卡片：打开成员详情
- 移动端：单指拖拽平移，双指手势缩放，仅作用于族谱显示区域，不会放大整个网页

### 4.1.1 系统设置中的显示方式

管理员可在“系统设置”中配置：

- 家谱名称
- 浏览器标题
- 主题颜色
- 公告跑马灯
- 默认缩放比例（50% ~ 500%）
- 族谱显示方式：
  - 垂直方向（自上而下）
  - 水平方向（自左向右，默认）
- 卡片显示“行几”
- 卡片显示“辈分”
- 卡片显示“第几代”

系统默认缩放为 `180%`，以保证首次打开页面时成员卡片文字更清晰。

### 4.2 成员管理

- 管理员：可“添加/编辑/删除/添加子女/添加配偶”
- 普通用户：只读查看，不显示管理按钮
- 成员支持维护职业字段
- 若姓名中包含“农民”，系统会自动将职业识别为“农民”

### 4.3 统计、搜索、导出、分享

- “统计”：显示人数与辈分统计
- 搜索框：按姓名模糊查询
- “导出”：将当前树图导出为图片
- “分享”：生成分享链接（默认 30 天有效）
- “帮助”：查看操作说明弹窗
- “全部成员”：点击配偶名字可直接打开配偶详情
- “全部成员/个人详情/成员卡片”会显示职业信息（如有）
- “打印”：系统会先生成完整族谱图片，再打开专用打印窗口，避免直接打印当前浏览区导致 PDF 内容不完整
- “导入XMind”：管理员可导入 `.xmind` 文件，导入后会替换当前成员数据
- “导出XMind”：管理员可将当前族谱导出为 `.xmind` 文件

## 5. 账号管理

管理员登录后可在导航栏进入“账号管理”：

- 创建 `Admin` 账号
- 创建 `Viewer` 账号

普通账号只能浏览族谱和查看统计，无法新增/删除成员。

## 6. 腾讯云 CDN 部署（详细步骤）

说明：CDN 只负责静态资源加速，ASP.NET Core 动态请求仍需源站（如腾讯云 CVM）承载。

### 6.1 准备云主机（CVM）

1. 创建 Linux CVM（建议 Ubuntu 22.04）。
2. 安全组放行：`22`、`80`、`443`、应用端口（如 `5000`）。
3. 安装 .NET 8 Runtime / SDK、Nginx。

### 6.2 发布应用到服务器

在本地发布：

```bash
cd "/Users/admin/RiderProjects/FamilyTree"
dotnet publish -c Release -o ./publish
```

上传 `publish` 目录到服务器（例如 `/var/www/familytree`），并在服务器创建 systemd 服务：

```bash
sudo tee /etc/systemd/system/familytree.service >/dev/null <<'EOF'
[Unit]
Description=FamilyTree ASP.NET Core App
After=network.target

[Service]
WorkingDirectory=/var/www/familytree
ExecStart=/usr/bin/dotnet /var/www/familytree/FamilyTree.dll --urls http://127.0.0.1:5000
Restart=always
RestartSec=5
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable familytree
sudo systemctl start familytree
sudo systemctl status familytree
```

### 6.3 配置 Nginx 反向代理

```bash
sudo tee /etc/nginx/conf.d/familytree.conf >/dev/null <<'EOF'
server {
	listen 80;
	server_name your-domain.com;

	location / {
		proxy_pass http://127.0.0.1:5000;
		proxy_http_version 1.1;
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		proxy_set_header X-Forwarded-Proto $scheme;
	}

	# 静态资源缓存示例（也可完全交给 CDN）
	location ~* \.(css|js|png|jpg|jpeg|svg|ico|woff2?)$ {
		proxy_pass http://127.0.0.1:5000;
		expires 7d;
		add_header Cache-Control "public, max-age=604800";
	}
}
EOF

sudo nginx -t
sudo systemctl reload nginx
```

### 6.4 接入腾讯云 CDN

1. 打开腾讯云 CDN 控制台，新增域名（如 `static.your-domain.com`）。
2. 回源地址填 Nginx 公网域名或 CVM IP。
3. 缓存策略建议：
   - `*.css`, `*.js`, `*.svg`, `*.png`：缓存 7 天
   - HTML、`/api/*`：不缓存或缓存 0
4. 回源 Host 使用业务域名，保证应用识别正确。
5. 完成后在 DNS 中将 CDN 子域名 CNAME 到腾讯云分配地址。

### 6.5 应用侧建议

- 静态资源继续使用 `asp-append-version="true"` 防止旧缓存。
- 登录态 Cookie 建议开启 `Secure`（HTTPS）并设置合理过期。
- 生产环境请修改默认管理员密码，避免弱口令。

### 6.6 发布与回滚建议

- 发布前：备份 `familytree.db`。
- 发布后：CDN 执行“目录刷新”或“URL 刷新”。
- 回滚时：恢复上一个 `publish` 包 + 数据库备份。

## 7. 常见问题排查

### 问题 1：页面一直加载

按顺序检查：

1. 浏览器控制台是否有 `familytree.js` 404 或 JS 报错
2. 接口是否可访问：`/api/person/tree`
3. 数据是否异常（父子环路等）

可用命令：

```bash
curl -sS http://localhost:5009/api/person/tree
curl -sS http://localhost:5009/api/person/stats
```

### 问题 2：按钮点击没反应

- 检查 `Views/Home/Index.cshtml` 是否引用 `~/js/familytree.js`
- 检查 `wwwroot/js/familytree.js` 文件是否存在
- 检查 Bootstrap/D3 脚本是否加载成功
- 检查浏览器是否缓存了旧版 JS/CSS，必要时强制刷新（`Cmd + Shift + R`）

### 问题 5：打印到 PDF 只有当前屏幕的一部分

- 请使用页面右上角“打印”按钮，而不是浏览器默认菜单直接打印当前页面
- 新版打印流程会先生成整棵树图片，再进入专用打印页
- 若仍异常，请确认浏览器允许打开弹窗

### 问题 3：看不到任何成员

- 检查数据库是否为空
- 删除本地 DB 后重启，让系统重新初始化测试数据

### 问题 4：升级后启动报 Users 表不存在

- 该版本已在启动时自动补建 `Users` 表。
- 如仍异常，先备份数据库后重启应用，确认日志中无 SQL 报错。

## 8. 开发调试建议

- 后端：关注 `PersonController` 返回状态与异常信息
- 前端：在 `familytree.js` 的 `apiGet/apiSend` 处观察请求失败原因
- 数据：修改亲属关系后，优先验证 `/api/person/tree` 响应是否正常
- 设置：修改显示方向、主题色、默认缩放后，优先验证 `/api/person/site-settings` 返回值是否已更新

