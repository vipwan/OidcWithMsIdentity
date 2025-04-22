# OidcWithMsIdentity.Server.http 使用指南

## 简介

`OidcWithMsIdentity.Server.http`是一个用于测试OidcWithMsIdentity项目中OpenID Connect和OAuth 2.0身份验证功能的HTTP请求文件。这个文件使用VS Code的REST Client扩展格式，允许您直接从编辑器发送HTTP请求，测试认证服务器的各种端点和授权流程，无需使用Postman等额外工具。

## 前置要求

1. **安装REST Client扩展 ,如使用VS2022忽略此项**
   - 在VS Code中安装REST Client扩展
   - 扩展ID: `humao.rest-client`

2. **确保服务器已启动**
   - OidcWithMsIdentity.Server服务需要在测试前启动并运行
   - 确认服务运行在配置的端口上（默认为7102）

3. **配置环境变量**
   - 查看文件顶部的环境变量设置，确保它们匹配您的实际配置

## 文件结构和请求说明

### 环境变量配置

文件顶部定义了测试所需的环境变量：

```
@baseUrl = https://localhost:7102
@clientId = client_id
@clientSecret = client_secret
@username = vipwan@sina.com
@password = 123456
```

- `baseUrl`: 身份验证服务器的基本URL
- `clientId`: 在OpenIddict中注册的客户端ID
- `clientSecret`: 客户端密钥
- `username`: 测试用户的电子邮件
- `password`: 测试用户的密码

请根据您的实际环境修改这些值。

### 1. 客户端凭据授权流程

客户端凭据授权适用于应用程序自身访问资源，而不是代表特定用户。

```
### 1. 获取访问令牌 (客户端凭据授权)
# @name getToken
POST {{baseUrl}}/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id={{clientId}}&client_secret={{clientSecret}}&scope=api
```

请求说明：
- 端点：`/connect/token`
- 授权类型：`client_credentials`
- 所需参数：
  - `grant_type`: 固定为"client_credentials"
  - `client_id`: 客户端标识符
  - `client_secret`: 客户端密钥
  - `scope`: 请求的权限范围，这里是"api"

响应内容：
- `access_token`: 访问令牌
- `token_type`: 令牌类型，通常为"Bearer"
- `expires_in`: 令牌有效期（秒）
- `scope`: 授予的权限范围

### 2. 使用客户端凭据访问令牌访问API

```
### 保存返回的访问令牌
@accessToken = {{getToken.response.body.$.access_token}}

### 2. 使用访问令牌访问受保护的 API
GET {{baseUrl}}/api/testapi/secure
Authorization: Bearer {{accessToken}}
```

请求说明：
- 端点：`/api/testapi/secure`
- 授权方式：在请求头中使用Bearer令牌
- 验证：此端点由`[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`属性保护

### 3. 访问需要特定API范围的端点

```
### 3. 访问需要 API 范围的端点
GET {{baseUrl}}/api/testapi/scoped
Authorization: Bearer {{accessToken}}
```

请求说明：
- 端点：`/api/testapi/scoped`
- 授权方式：在请求头中使用Bearer令牌
- 验证：此端点由`[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Policy = "ApiScope")]`属性保护，需要具有"api"范围权限的访问令牌

### 4. 密码授权流程

密码授权流程适用于应用程序代表用户访问资源。

```
### 6. 使用密码授权模式获取令牌
# @name getPasswordToken
POST {{baseUrl}}/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id={{clientId}}&client_secret={{clientSecret}}&username={{username}}&password={{password}}&scope=api
```

请求说明：
- 端点：`/connect/token`
- 授权类型：`password`
- 所需参数：
  - `grant_type`: 固定为"password"
  - `client_id`: 客户端标识符
  - `client_secret`: 客户端密钥
  - `username`: 用户的电子邮件
  - `password`: 用户的密码
  - `scope`: 请求的权限范围，这里是"api"

响应内容：
- `access_token`: 访问令牌
- `refresh_token`: 刷新令牌（如果启用）
- `id_token`: 身份令牌（如果请求了"openid"范围）
- `token_type`: 令牌类型，通常为"Bearer"
- `expires_in`: 令牌有效期（秒）

### 5. 使用密码授权流程获取的令牌访问API

```
### 保存返回的密码授权令牌
@passwordToken = {{getPasswordToken.response.body.$.access_token}}

### 7. 使用密码授权令牌访问受保护的 API
GET {{baseUrl}}/api/testapi/secure
Authorization: Bearer {{passwordToken}}
```

请求说明：
- 端点：`/api/testapi/secure`
- 授权方式：在请求头中使用通过密码授权流程获取的Bearer令牌

### 6. 使用内置Identity验证方式

```
### 4. 默认的内部Identity验证方式:
# @name getIdentityToken
POST {{baseUrl}}/account/login
Content-Type: application/json; charset=utf-8

{
  "email": "{{username}}",
  "password": "{{password}}"
}
```

请求说明：
- 端点：`/account/login`
- 请求格式：JSON
- 所需参数：
  - `email`: 用户的电子邮件
  - `password`: 用户的密码

响应内容：
- `accessToken`: ASP.NET Core Identity生成的访问令牌

### 7. 使用Identity令牌访问API

```
### 保存返回的访问令牌
@identityToken = {{getIdentityToken.response.body.$.accessToken}}

### 5. 使用Identity令牌访问受保护的 API
GET {{baseUrl}}/api/testapi/default-identity
Authorization: Bearer {{identityToken}}
```

请求说明：
- 端点：`/api/testapi/default-identity`
- 授权方式：在请求头中使用通过ASP.NET Core Identity生成的Bearer令牌

## 调试技巧

### 1. 查看请求和响应详情

在VS Code中使用REST Client发送请求后，将在右侧打开一个响应面板，您可以查看：

- 响应状态码
- 响应头部
- 响应正文（通常是JSON格式）
- 请求和响应的时间戳

### 2. 检查令牌内容

获取到的JWT令牌可以在工具如[jwt.io](https://jwt.io/)中解析，查看其包含的声明和元数据。

### 3. 测试不同的权限范围

您可以修改请求中的`scope`参数来测试不同的权限组合：

```
scope=api openid profile email offline_access
```

- `api`: 访问API的权限
- `openid`: 表明这是OpenID Connect请求
- `profile`: 请求用户的基本资料信息
- `email`: 请求用户的电子邮件
- `offline_access`: 请求包含刷新令牌

### 4. 测试错误情况

尝试以下场景来模拟错误情况：

- 使用错误的客户端凭据
- 使用错误的用户名/密码
- 尝试访问没有适当权限的资源
- 使用过期的令牌

## 常见问题

1. **令牌请求返回401或400错误**
   - 检查客户端ID和密钥是否正确
   - 确认用户账号存在且密码正确
   - 检查请求的范围是否允许给该客户端

2. **API请求返回401错误**
   - 确认令牌没有过期
   - 检查令牌是否包含正确的范围
   - 验证Bearer前缀是否正确添加

3. **令牌内容不包含预期的声明**
   - 检查服务器端的声明映射配置
   - 确认请求中包含了需要的范围

4. **"invalid_grant"错误**
   - 对于密码授权流程，确认用户凭据正确
   - 对于刷新令牌，确认刷新令牌有效且未过期

## 参考资源

- [OpenIddict 文档](https://documentation.openiddict.com/)
- [OAuth 2.0 授权框架](https://tools.ietf.org/html/rfc6749)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [VS Code REST Client 扩展](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
