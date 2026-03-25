# FamilyTree

家谱管理系统（ASP.NET Core MVC + EF Core + SQLite + D3.js）。

<img width="3484" height="1804" alt="image" src="https://github.com/user-attachments/assets/32efde77-3929-4277-bcc0-28da7a817e4c" />

## 基本功能

- 家族成员增删改查
- 族谱树可视化展示
- 成员搜索、统计、导出、分享
- 成员照片上传
- 登录鉴权和角色权限（管理员/只读用户）
- 系统设置驱动的主题色、家谱名称、公告与显示方向
- 系统设置可控制卡片显示“行几/辈分/第几代”三个标签
- 支持 XMind 文件导入与导出

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
