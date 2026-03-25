# FamilyTree 技术介绍

## 1. 项目目标

FamilyTree 是一个面向家谱管理场景的 Web 系统，支持：

- 家族成员增删改查
- 族谱树可视化展示
- 成员搜索、统计、导出、分享
- 成员照片上传
- 登录鉴权和角色权限（管理员/只读用户）
- 系统设置驱动的主题色、家谱名称、公告与显示方向
- 系统设置可控制卡片显示“行几/辈分/第几代”三个标签
- 支持 XMind 文件导入与导出

## 2. 技术栈

- 后端：ASP.NET Core 8 MVC / Web API
- 数据访问：Entity Framework Core 8
- 数据库：SQLite（`familytree.db`）
- 前端：Bootstrap 5 + D3.js + 原生 JavaScript
- 图片导出：html2canvas

## 3.1 本阶段新增能力

- 支持在系统设置中切换族谱显示方向：`垂直` / `水平`，默认 `水平`
- 成员新增 `Occupation`（职业）字段
- 当姓名中包含“农民”时，系统自动推断职业为“农民”
- 默认缩放设置上限提高到 `500%`，系统默认值提升到 `180%`
- 移动端双指缩放仅作用于族谱画布区域，不放大整个网页
- 打印改为生成完整族谱图片后进入专用打印页，避免浏览器直接打印当前视口导致内容缺失

## 3. 架构与目录

- 启动入口：`Program.cs`
- 数据上下文：`Data/FamilyTreeDbContext.cs`
- 业务 API：`Controllers/PersonController.cs`
- 页面控制器：`Controllers/HomeController.cs`
- 数据模型：`Models/Person.cs`
- 用户模型：`Models/AppUser.cs`
- 主页视图：`Views/Home/Index.cshtml`
- 登录与账号管理：`Views/Account/Login.cshtml`、`Views/Account/Users.cshtml`
- 前端逻辑：`wwwroot/js/familytree.js`
- 样式：`wwwroot/css/familytree.css`

## 4. 核心设计

### 4.1 数据模型

`Person` 是自引用实体：

- `ParentId`：父节点（上级）
- `SpouseId`：配偶关系
- `Children`：子节点集合
- `IsRoot`：是否族谱根节点
- `Occupation`：职业信息

为避免序列化循环引用，接口返回使用 `PersonDto`。

### 4.2 树形接口

- `GET /api/person/tree`：返回树结构
- `BuildTreeNode(...)`：递归组装树节点
- 加入 `visited` 与深度限制，避免异常数据导致无限递归

### 4.3 数据一致性保护

在 `PersonController` 中增加了关系校验：

- 禁止自己是自己的父亲
- 禁止自己是自己的配偶
- 禁止 `ParentId == SpouseId`
- 更新父节点时检测环路（防止出现祖先回指）

### 4.4 前端交互

`familytree.js` 在页面加载后完成：

1. 初始化 Modal 与按钮事件
2. 并行加载树和成员列表
3. 渲染 D3 树图
4. 支持编辑、删除、搜索、统计、分享、导出
5. 支持移动端手势缩放、帮助弹窗、默认头像回退

补充说明：

- 根据 `site-settings` 接口中的 `layoutOrientation`，D3 在前端切换为纵向或横向布局。
- 缩放范围提升为 `0.15x ~ 8x`，并保留系统设置中的默认缩放比例。
- 为兼容移动端 Safari，族谱区域额外拦截 `gesturestart/gesturechange/gestureend`，避免触发页面级缩放。
- 打印按钮不再直接打印当前页面，而是先导出整棵树为高分辨率 PNG，再在新窗口中按 A4 页面打印。

### 4.5 认证与权限

- 采用 Cookie Authentication。
- `HomeController` 和 `PersonController` 需要登录访问。
- `PersonController` 的写操作（新增/编辑/删除/上传/分享）限制为 `Admin`。
- 提供 `GET /api/person/permissions` 给前端控制按钮显隐。

### 4.6 视图和数据展示增强

- 顶部标题、浏览器 title、家谱名称均由系统设置驱动。
- 树卡片增加“第几代 + 配偶名字 + 职业/子女数”。
- “全部成员”列表中配偶名可点击打开配偶详情。
- 个人详情头像优先使用上传图，否则回退到本地默认剪影 `wwwroot/images/default-avatar.svg`。
- 个人详情、编辑弹窗、全部成员列表均支持显示职业信息。
- 卡片右侧标签区域使用固定槽位布局，即使隐藏“行几”或“辈分”，其余标签位置也不会漂移。

## 5. 数据初始化策略

### 5.1 固定种子数据

`FamilyTreeDbContext` 使用 `HasData` 内置了一套 4 代示例家谱（30+ 人）。

### 5.2 启动兜底数据

`Program.cs` 增加空库兜底：若 `Persons` 为空，启动时自动写入最小演示数据（始祖、配偶、子女）。

另外，启动时会对已有 SQLite 数据库做轻量升级：

- 自动补齐 `SystemSettings.LayoutOrientation`
- 自动补齐 `Persons.Occupation`
- 对姓名中包含“农民”的历史数据自动回填职业

## 6. 关键 API 列表

- `GET /api/person`：成员列表
- `GET /api/person/{id}`：成员详情
- `POST /api/person`：新增成员
- `PUT /api/person/{id}`：修改成员
- `DELETE /api/person/{id}`：删除成员
- `GET /api/person/tree`：树结构
- `GET /api/person/search?name=xxx`：搜索
- `GET /api/person/stats`：统计
- `POST /api/person/{id}/photo`：上传头像
- `POST /api/person/share`：生成分享链接
- `GET /api/person/permissions`：获取当前用户权限
- `GET /api/person/site-settings`：获取站点标题、家谱名称、主题色、默认缩放、显示方向等前端配置
- `POST /api/person/import-xmind`：导入 XMind 族谱文件（管理员）
- `GET /api/person/export-xmind`：导出当前族谱为 XMind 文件（管理员）

## 7. 本次问题修复说明

针对“页面一直 loading、按钮无响应”问题，已完成：

- 补齐并接入 `wwwroot/js/familytree.js`（页面主交互脚本）
- 完整绑定工具栏和弹窗按钮事件
- 加入前端请求异常处理与提示
- 后端树构建加防环与深度保护
- 统计接口与前端请求增加防缓存，避免辈分新增后统计显示过期

以上修改可避免脚本缺失或关系异常导致的前端卡死。

