# OidcWithMsIdentity 使用指南

## 项目介绍

OidcWithMsIdentity 是一个基于OpenID Connect和Microsoft Identity的认证授权演示项目。该项目由以下主要部分组成：

1. **OidcWithMsIdentity.Server**: 一个OpenID Connect身份认证服务器，基于OpenIddict和ASP.NET Core Identity实现
2. **OidcWithMsIdentity.Client**: 一个客户端应用，使用OpenID Connect协议与服务器进行交互认证
3. **OidcWithMsIdentity.ContentService**: 内容管理和搜索服务，提供博客内容和全文搜索功能
4. **OidcWithMsIdentity.AppHost**: .NET Aspire应用托管项目，统一管理和编排所有服务
5. **OidcWithMsIdentity.ServiceDefaults**: 服务默认配置，包含所有项目共享的服务配置

## 项目架构

### Aspire 托管架构 (OidcWithMsIdentity.AppHost)

项目采用.NET Aspire进行分布式应用托管，具有以下特点：

- 集中式服务编排和管理
- 内置的服务发现和健康检查
- 使用MySQL作为数据存储
- 使用Redis进行分布式缓存
- 使用RabbitMq进行消息传递
- 支持容器化部署
- 提供统一的资源监控和管理仪表板

### 服务器端 (OidcWithMsIdentity.Server)

服务器端实现了一个完整的OpenID Connect认证服务器，提供以下功能：

- 用户注册、登录和身份管理（使用ASP.NET Core Identity）
- OAuth 2.0 / OpenID Connect授权服务（使用OpenIddict）
- 支持多种授权流程：授权码、客户端凭证、混合、密码、隐式和刷新令牌流程
- 提供API端点进行测试
- 使用SQLite作为数据存储(使用EFCore,可以切换到任意数据库)

### 客户端 (OidcWithMsIdentity.Client)

客户端应用演示了如何使用OpenID Connect与身份服务器进行集成：

- 使用OpenID Connect中间件进行身份验证
- 实现声明（Claims）映射和角色处理
- 使用获取的Token访问受保护的API
- 通过Yarp反向代理文档搜索服务

### 内容服务 (OidcWithMsIdentity.ContentService)

内容服务提供博客内容管理和全文搜索功能：

- 基于Meilisearch搜索引擎实现高效全文搜索
- 支持博客文章的增删改查操作
- 提供按分类、标签和关键词的内容过滤
- RESTful API设计，便于与前端应用集成
- 支持分页、排序和复杂查询功能
- 通过`RabbitMq`消息实时索引更新，确保搜索结果与内容同步

## 安装与配置

### 系统要求

- .NET 9.0 或更高版本
- Visual Studio 2022 或Visual Studio Code
- 操作系统：Windows、macOS或Linux
- Docker Desktop (用于Aspire容器化部署)

### 配置AppHost (OidcWithMsIdentity.AppHost)

1. 确保已安装Docker Desktop并运行
2. 检查`Program.cs`文件中的配置，确保数据库和Redis服务配置正确：

```csharp
var mysql = builder.AddMySql("mysql")
    .WithDataVolume("mysql-data")
    .AddDatabase("oidcdb");
    
var redis = builder.AddRedis("redis");

var server = builder.AddProject<Projects.OidcWithMsIdentity_Server>("server")
    .WithReference(mysql);
    
var client = builder.AddProject<Projects.OidcWithMsIdentity_Client>("client")
    .WithReference(redis)
    .WithReference(server);
```

### 配置服务器 (OidcWithMsIdentity.Server)

1. 打开`appsettings.json`文件，检查数据库连接字符串：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=MyData.db"
  }
}
```

2. 如果需要，可以修改OpenID Connect配置相关设置

### 配置客户端 (OidcWithMsIdentity.Client)

1. 打开`appsettings.json`文件，确保以下OIDC设置正确：

```json
{
  "Oidc": {
    "Host": "https://localhost:7001",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

2. 确保`ClientId`和`ClientSecret`与服务器上注册的客户端凭据匹配

## 启动项目

### 使用Aspire启动整个应用

1. 在Visual Studio中设置OidcWithMsIdentity.AppHost为启动项目
2. 按F5启动调试，或使用命令行：

```bash
cd OidcWithMsIdentity.AppHost
dotnet run
```

3. Aspire仪表板将自动打开，显示所有服务的状态

### 单独启动服务器

1. 在Visual Studio中设置OidcWithMsIdentity.Server为启动项目
2. 按F5启动调试，或使用命令行：

```bash
cd OidcWithMsIdentity.Server
dotnet run
```

3. 服务器默认将在 `https://localhost:7001` 上运行

### 单独启动客户端

1. 在Visual Studio中设置OidcWithMsIdentity.Client为启动项目
2. 按F5启动调试，或使用命令行：

```bash
cd OidcWithMsIdentity.Client
dotnet run
```

3. 客户端默认将在 `https://localhost:7002` 上运行

## 调试指南

### 调试Aspire托管应用

1. **Aspire仪表板**:
   - 使用Aspire仪表板监控所有服务的健康状态
   - 检查服务日志和性能指标
   - 使用Resource Explorer查看服务依赖关系

2. **容器服务调试**:
   - 使用Docker Desktop查看容器状态和日志
   - 使用MySQL客户端工具连接MySQL容器
   - 使用Redis客户端工具连接Redis容器

### 调试服务器端

1. **身份验证流程调试**:
   - 在`AuthorizationController.cs`中设置断点，特别是在`ExchangeAsync`方法中，以跟踪令牌交换过程
   - 在`Microsoft.AspNetCore.Identity`相关代码中设置断点来调试用户认证过程

2. **数据库调试**:
   - 使用SQLite浏览器工具检查`MyData.db`文件
   - 在`ApplicationDbContext.cs`中设置断点来监控数据库操作

3. **OpenIddict配置调试**:
   - 在`Program.cs`中添加日志记录来监控OpenIddict配置

```csharp
builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        // 添加更详细的日志
        options.SetIssuer(new Uri("https://localhost:7001/"));
        options.DisableAccessTokenEncryption();
        // 其他配置...
    });
```

4. **常见问题排查**:
   - 如果遇到JWT验证问题，检查签名验证设置
   - 如果遇到CORS问题，确认已正确配置CORS策略

### 调试客户端

1. **身份验证流程**:
   - 在`Program.cs`的OpenID Connect中间件配置部分设置断点
   - 监控`OnTicketReceived`事件处理程序以检查声明处理

2. **令牌处理**:
   - 在控制器中使用调试输出来显示当前用户的声明：

```csharp
foreach (var claim in User.Claims)
{
    Debug.WriteLine($"Claim: {claim.Type} = {claim.Value}");
}
```

3. **API调用**:
   - 使用浏览器开发者工具检查API请求头中的授权令牌
   - 在API控制器的调用前后设置断点

## 高级功能

### 添加自定义声明

1. 在服务器端的`AuthorizationController.cs`中添加自定义声明：

```csharp
var identity = new ClaimsIdentity(
    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
    nameType: Claims.Name,
    roleType: Claims.Role);

// 添加自定义声明
identity.AddClaim(new Claim("custom_claim", "custom_value"));
```

2. 在客户端配置中映射该声明：

```csharp
options.ClaimActions.MapUniqueJsonKey("custom_claim", "custom_claim");
```

### 配置额外的授权策略

在服务器或客户端的`Program.cs`中添加自定义授权策略：

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));
        
    options.AddPolicy("CustomPolicy", policy =>
        policy.RequireClaim("custom_claim", "custom_value"));
});
```

### 配置Aspire服务注册和发现

在AppHost项目中自定义服务注册和发现：

```csharp
// 添加自定义服务配置
var customService = builder.AddProject<Projects.Custom_Service>("custom-service")
    .WithReference(mysql)
    .WithReference(redis);
    
// 添加外部服务引用
builder.AddConnectionString("external-service", 
    builder.Configuration.GetConnectionString("ExternalServiceConnection"));
```

## 故障排除

### 常见问题

1. **无法登录/身份验证失败**
   - 检查客户端设置中的ClientId和ClientSecret是否与服务器端注册的匹配
   - 确认启动顺序：先启动服务器，再启动客户端
   - 检查服务器上是否有相应的用户账户

2. **CORS错误**
   - 确认CORS策略正确配置并包含客户端的源
   - 检查请求头中是否包含正确的CORS头部

3. **数据库连接问题**
   - 确认数据库连接字符串正确
   - 检查MySQL容器是否正常运行
   - 验证数据库用户权限是否正确

4. **Redis连接问题**
   - 检查Redis容器是否正常运行
   - 验证Redis连接字符串是否正确

5. **令牌验证失败**
   - 检查令牌有效期设置
   - 确保客户端和服务器时间同步
   - 验证签名密钥是否正确配置

## 参考资源

- [.NET Aspire 官方文档](https://learn.microsoft.com/zh-cn/dotnet/aspire/get-started/aspire-overview)
- [OpenIddict 官方文档](https://documentation.openiddict.com/)
- [ASP.NET Core Identity 文档](https://docs.microsoft.com/zh-cn/aspnet/core/security/authentication/identity)
- [OpenID Connect 规范](https://openid.net/specs/openid-connect-core-1_0.html)
- [OAuth 2.0 规范](https://oauth.net/2/)
