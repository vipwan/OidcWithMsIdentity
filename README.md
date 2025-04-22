# OidcWithMsIdentity 使用指南

## 项目介绍

OidcWithMsIdentity 是一个基于OpenID Connect和Microsoft Identity的认证授权演示项目。该项目由两个主要部分组成：

1. **OidcWithMsIdentity.Server**: 一个OpenID Connect身份认证服务器，基于OpenIddict和ASP.NET Core Identity实现
2. **OidcWithMsIdentity.Client**: 一个客户端应用，使用OpenID Connect协议与服务器进行交互认证

## 项目架构

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

## 安装与配置

### 系统要求

- .NET 9.0 或更高版本
- Visual Studio 2022 或Visual Studio Code
- 操作系统：Windows、macOS或Linux

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

### 启动服务器

1. 在Visual Studio中设置OidcWithMsIdentity.Server为启动项目
2. 按F5启动调试，或使用命令行：
   ```
   cd OidcWithMsIdentity.Server
   dotnet run
   ```
3. 服务器默认将在 https://localhost:7001 上运行

### 启动客户端

1. 在Visual Studio中设置OidcWithMsIdentity.Client为启动项目
2. 按F5启动调试，或使用命令行：
   ```
   cd OidcWithMsIdentity.Client
   dotnet run
   ```
3. 客户端默认将在 https://localhost:7002 上运行

## 调试指南

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
   - 确认SQLite文件路径正确
   - 检查应用是否有对数据库文件的写入权限

4. **令牌验证失败**
   - 检查令牌有效期设置
   - 确保客户端和服务器时间同步
   - 验证签名密钥是否正确配置

## 参考资源

- [OpenIddict 官方文档](https://documentation.openiddict.com/)
- [ASP.NET Core Identity 文档](https://docs.microsoft.com/zh-cn/aspnet/core/security/authentication/identity)
- [OpenID Connect 规范](https://openid.net/specs/openid-connect-core-1_0.html)
- [OAuth 2.0 规范](https://oauth.net/2/)
