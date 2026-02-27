# DynamicCrudSample 项目上下文

## 项目概述

**DynamicCrudSample** 是一个基于 **.NET 10 MVC** 的动态 CRUD 应用程序，使用 **YAML 配置驱动** 的元数据系统来动态生成实体管理界面。项目使用 **Chinook 数据库** 作为示例数据源，支持多语言（en-US、zh-CN、ja-JP）、认证授权、审计日志和 HTMX 局部更新。

### 核心技术栈

| 类别 | 技术 |
|------|------|
| 框架 | ASP.NET Core 10 MVC |
| ORM | Dapper |
| 数据库 | SQLite (Chinook) |
| UI 框架 | DaisyUI + Bootstrap |
| 局部更新 | HTMX |
| 日志 | Serilog |
| 配置解析 | YamlDotNet |
| 认证 | Cookie Authentication |

### 项目结构

```
DynamicCrudSample/
├── Program.cs                 # 应用入口：DI、认证、本地化、日志配置
├── DynamicCrudSample.csproj   # 项目文件 (.NET 10)
├── appsettings.json           # 应用配置
├── chinook.db                 # SQLite 数据库
│
├── Controllers/
│   ├── DynamicEntityController.cs  # 动态 CRUD 核心控制器
│   ├── AccountController.cs        # 认证（登录/登出）
│   ├── UsersController.cs          # 用户管理
│   └── LocalizationController.cs   # 语言切换
│
├── Models/
│   ├── EntityMetadata.cs      # YAML 元数据模型定义
│   └── Auth/
│       ├── AppUser.cs         # 用户模型
│       └── LoginViewModel.cs  # 登录视图模型
│
├── Services/
│   ├── DynamicCrudRepository.cs   # 动态 SQL 生成与执行
│   ├── EntityMetadataProvider.cs  # YAML 元数据加载
│   ├── ValueConverter.cs          # 类型转换
│   └── Auth/
│       ├── UserAuthService.cs     # 用户认证服务
│       └── AuditLogService.cs     # 审计日志服务
│
├── Data/
│   └── DbInitializer.cs       # 数据库初始化（Chinook 下载、Auth 表创建）
│
├── Views/
│   ├── DynamicEntity/         # 动态实体视图（Index/List/Form/Filter）
│   ├── Account/               # 认证视图
│   ├── Users/                 # 用户管理视图
│   └── Shared/                # 共享布局与部分视图
│
├── config/
│   ├── entities.yml           # 实体配置（备用）
│   └── entities/              # 分文件实体配置
│       ├── customer.yml
│       ├── employee.yml
│       ├── artist.yml
│       ├── album.yml
│       ├── track.yml
│       ├── genre.yml
│       └── invoice.yml
│
├── Resources/                 # 多语言 RESX 资源
├── Localization/              # 本地化资源类
├── docs/                      # 文档（CHANGELOG、实现摘要）
└── logs/                      # Serilog 日志输出
```

## 构建与运行

### 前置条件

- .NET 10 SDK
- SQLite（内置，无需单独安装）

### 构建命令

```bash
dotnet build
```

### 运行命令

```bash
dotnet run
```

应用默认在 `http://localhost:5000` 或 `https://localhost:5001` 启动（具体端口见控制台输出）。

### 默认管理员账户

首次运行时会自动创建默认管理员：

| 字段 | 值 |
|------|-----|
| 用户名 | `admin` |
| 密码 | `Admin@123` |

**注意：** 首次登录后请立即修改密码。

### 测试命令

```bash
dotnet test
```

## 核心功能

### 1. YAML 驱动的动态 CRUD

实体配置位于 `config/entities/*.yml`，定义：

- **表结构**：表名、主键、关联（JOIN）
- **列定义**：类型、标签、可搜索/可排序标志、表达式
- **表单定义**：输入类型、验证规则、外键关联
- **筛选器**：下拉、多选、范围、日期范围
- **分页设置**：页大小、模式（numbered/keyset）
- **多语言**：`displayNameI18n`、`labelI18n`
- **布局**：表单/筛选器的列数和字段顺序

示例配置片段：

```yaml
entities:
  customer:
    table: Customer
    key: CustomerId
    displayName: Customer
    softDelete: false
    paging:
      pageSize: 10
      mode: numbered
    columns:
      CustomerId:
        type: int
        identity: true
        label: ID
        sortable: true
      FirstName:
        type: string
        required: true
        label: First Name
        searchable: true
    forms:
      FirstName:
        type: string
        required: true
        editable: true
    filters:
      Country:
        type: multi-select
        options: [USA, Canada, Brazil]
```

### 2. 认证与授权

- **Cookie 认证**：滑动过期 8 小时
- **角色策略**：`AdminOnly` 策略限制管理员访问
- **用户管理**：用户列表、创建、编辑、激活/禁用

### 3. 审计日志

所有 CRUD 操作和用户管理操作均记录到 `AuditLog` 表：

```sql
CREATE TABLE AuditLog (
    Id INTEGER PRIMARY KEY,
    UserName TEXT,
    Action TEXT,
    Entity TEXT,
    Detail TEXT,
    CreatedAt TEXT
);
```

### 4. 多语言支持

支持三种语言切换：

| 代码 | 语言 |
|------|------|
| `en-US` | 英语 |
| `zh-CN` | 简体中文 |
| `ja-JP` | 日语 |

语言切换通过 `LocalizationController` 实现，UI 文本使用 RESX 资源文件管理。

### 5. 日志系统

Serilog 配置：

- **控制台输出**：开发调试
- **文件输出**：`logs/app-YYYYMMDD.log`，保留 14 天
- **请求日志**：所有 HTTP 请求自动记录
- **SQL 日志**：动态生成的 SQL 语句记录

## 开发约定

### 代码风格

- **可空引用类型**：启用（`<Nullable>enable</Nullable>`）
- **隐式 using**：启用（`<ImplicitUsings>enable</ImplicitUsings>`）
- **注释语言**：核心文件使用日语注释（`ファイル概要`）
- **日志规范**：关键操作记录 `ILogger`，包含实体名、ID、SQL

### 事务处理

CRUD 操作与审计日志写入在同一事务中执行：

```csharp
await using var tx = conn.BeginTransaction();
try
{
    await _repo.InsertAsync(entity, values, tx);
    await _audit.LogAsync(user, "Create", entity, detail, tx);
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

### SQL 安全

动态 SQL 生成时进行严格验证：

- **标识符验证**：`^[A-Za-z_][A-Za-z0-9_]*$`
- **表达式验证**：白名单字符集，拒绝 `;`、`--`、`/* */`
- **JOIN 类型限制**：仅允许 `LEFT`、`INNER`、`RIGHT`

### 提交规范

每次 push 前必须更新 `docs/CHANGELOG.md`，记录：

1. 变更内容（Added/Fixed/Changed/Removed）
2. 影响范围
3. 验证结果（至少 1 个验证点）

## 关键接口与类

### IDynamicCrudRepository

```csharp
Task<IEnumerable<dynamic>> GetAllAsync(...);  // 分页/搜索/筛选
Task<dynamic?> GetByIdAsync(string entity, object id);
Task<int> InsertAsync(string entity, IDictionary<string, object?> values, IDbTransaction? tx);
Task<int> UpdateAsync(string entity, object id, IDictionary<string, object?> values, IDbTransaction? tx);
Task<int> DeleteAsync(string entity, object id, IDbTransaction? tx);
Task<int> CountAsync(string entity, string? search, ...);
```

### IEntityMetadataProvider

```csharp
EntityDefinition Get(string entityName);
IReadOnlyDictionary<string, EntityDefinition> GetAll();
```

### 认证服务

```csharp
// IUserAuthService
Task<AppUser?> GetUserByIdAsync(int id, IDbTransaction? tx = null);
Task<IEnumerable<AppUser>> GetAllUsersAsync();
Task<int> CreateUserAsync(AppUser user, string password, IDbTransaction? tx = null);
Task<int> UpdateUserAsync(AppUser user, IDbTransaction? tx = null);

// IAuditLogService
Task LogAsync(string userName, string action, string? entity, string? detail, IDbTransaction? tx = null);
```

## 常见问题

### 数据库文件不存在

首次运行时 `DbInitializer` 会自动从 GitHub 下载 Chinook 数据库：

```
https://github.com/lerocha/chinook-database/releases/download/v1.4.5/Chinook_Sqlite.sqlite
```

### 实体配置未生效

检查 `config/entities/` 目录下的 YAML 文件：

1. 文件扩展名必须为 `.yml`
2. 根节点必须是 `entities:`
3. 表达式中的单引号需用双引号包裹

### 认证失败

确认 `AppUser` 表已创建且存在有效用户：

```bash
sqlite3 chinook.db "SELECT * FROM AppUser;"
```

## 相关文档

- `docs/CHANGELOG.md` - 变更历史
- `docs/implementation-summary-ja.md` - 详细实现摘要（日语）
