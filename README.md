# FamilyTree

周氏家谱管理系统（ASP.NET Core MVC + EF Core + SQLite + D3.js）。

## 文档

- 技术介绍：`TECHNICAL_OVERVIEW.md`
- 操作说明：`OPERATIONS_GUIDE.md`

## 快速启动

```bash
cd "/Users/admin/RiderProjects/FamilyTree"
dotnet run
```

默认地址见 `Properties/launchSettings.json`（常用 `http://localhost:5009`）。

## 默认登录

- 管理员：`admin`
- 密码：`123456`

## 权限说明

- `Admin`：可查看、添加、编辑、删除成员，可创建账号。
- `Viewer`：只读账号，仅查看树图和统计。

## 本次重点更新

- 家族名称统一为“周氏家谱”。
- 首页导航中的“首页”按钮已移除。
- 成员卡片增加“第几代 + 配偶姓名”显示。
- 全部成员弹窗中，配偶姓名支持点击打开详情。
- 详情头像支持本地默认剪影：`wwwroot/images/default-avatar.svg`。
- 增加帮助按钮和操作说明弹窗。
- 支持移动端触控拖拽与双指缩放。

