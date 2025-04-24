// Licensed to the OidcWithMsIdentity.Client under one or more agreements.
// The OidcWithMsIdentity.Client licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using OidcWithMsIdentity.Client;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// Add services to the container.

// Aspire ServiceDefaults
builder.AddServiceDefaults();
// 添加Redis分布式缓存服务
builder.AddRedisDistributedCache("redis");
// 如果您需要直接使用IConnectionMultiplexer
builder.AddRedisClient("redis");
// 添加Redis 输出缓存服务
builder.AddRedisOutputCache("redis");

builder.Services.AddSingleton<IServiceUriProvider, ServiceUriProvider>();

// 添加YARP服务
builder.Services.AddHttpForwarder();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration).AddServiceDiscoveryDestinationResolver();

// 修改OIDC配置，使用服务发现
var isDocker = !string.IsNullOrEmpty(configuration["Docker:Oidc:Host"]) &&
               Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

builder.Services.AddControllers();

// 添加 HttpClient 工厂
builder.Services.AddHttpClient();

// 添加身份验证
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie("Cookies", options =>
{
    options.Cookie.Name = "OidcClient";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // 根据环境选择合适的配置
    var configSection = isDocker ? "Docker:Oidc" : "Oidc";

    options.Authority = configuration[$"{configSection}:Host"]; // OIDC服务地址
    options.ClientId = configuration[$"{configSection}:ClientId"]; // 客户端ID
    options.ClientSecret = configuration[$"{configSection}:ClientSecret"]; // 客户端密钥
    options.ResponseType = "code";

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("api"); // OidcSite中定义的API作用域

    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    // 禁用默认的声明映射，保留原始声明
    //options.MapInboundClaims = false;

    // 添加以下配置来指定 NameClaimType , RoleClaimType
    // 这将确保在身份验证后，ClaimsIdentity 中的 Name 和 Role 声明类型与 ASP.NET Core Identity 的默认值匹配
    //options.TokenValidationParameters = new TokenValidationParameters
    //{
    //    NameClaimType = "name",
    //    RoleClaimType = "role"
    //};

    // 明确映射OIDC核心声明
    options.ClaimActions.MapJsonKey(Claims.Subject, Claims.Subject);
    options.ClaimActions.MapUniqueJsonKey(Claims.Name, Claims.Name);
    options.ClaimActions.MapUniqueJsonKey(Claims.Email, Claims.Email);
    // 映射角色声明,需要注意的是，role多个使用逗号隔开
    options.ClaimActions.MapUniqueJsonKey(Claims.Role, Claims.Role);
    // 明确映射Identity框架声明
    options.ClaimActions.MapUniqueJsonKey(ClaimTypes.NameIdentifier, ClaimTypes.NameIdentifier);
    options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, ClaimTypes.Name);
    options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Email, ClaimTypes.Email);
    // 映射角色声明,需要注意的是，role多个使用逗号隔开
    options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Role, ClaimTypes.Role);

    // qicq, 自定义的声明
    options.ClaimActions.MapUniqueJsonKey("qicq", "qicq");

    // 添加事件处理程序来拆分逗号分隔的角色
    options.Events.OnTicketReceived = context =>
    {
        if (context.Principal != null)
        {
            if (context.Principal.Identity is not ClaimsIdentity identity) return Task.CompletedTask;

            // 查找所有角色声明
            var roleClaims = identity.FindAll(claim =>
                claim.Type == "role" || claim.Type == ClaimTypes.Role).ToList();

            // 处理每个角色声明
            foreach (var roleClaim in roleClaims)
            {
                // 移除原始的逗号分隔角色声明
                identity.RemoveClaim(roleClaim);
                // 如果包含逗号，则分割并添加多个声明
                if (!string.IsNullOrEmpty(roleClaim.Value))
                {
                    var roles = roleClaim.Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var role in roles)
                    {
                        identity.AddClaim(new Claim(roleClaim.Type, role.Trim()));
                    }
                }
            }
        }
        return Task.CompletedTask;
    };

    // 设置回调地址
    options.CallbackPath = "/signin-oidc";

    // 处理登出
    options.SignedOutCallbackPath = "/signout-callback-oidc";
    options.SignedOutRedirectUri = "/";

    // 配置OIDC端点 - 确保包含end_session_endpoint
    // options.MetadataAddress = $"{configuration["Oidc:Host"]}/.well-known/openid-configuration";
});

// 添加授权策略
builder.Services.AddAuthorizationBuilder()
// 添加授权策略
.AddPolicy("ApiScope", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireClaim("scope", "api");
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// 添加输出缓存中间件
app.UseOutputCache();

// app.UseHttpsRedirection();

// 添加身份验证中间件
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Aspire DefaultEndpoints
app.MapDefaultEndpoints();

// Yarp
app.MapReverseProxy();

#region 反向代理文档搜索,重定向到内容服务

// 根据环境确定服务地址
var serviceUriProvider = app.Services.GetRequiredService<IServiceUriProvider>();
var contentHost = serviceUriProvider.GetServiceUri("content");

app.MapGroup("search")
    .RequireAuthorization() // 需要登录
    .MapForwarder("{**path}", contentHost + "/api/blog");

#endregion

app.MapGet("/", async context =>
{
    var claimsHtml = "";
    if (context.User.Identity?.IsAuthenticated == true)
    {
        claimsHtml = "<h3>用户声明：</h3><ul>";
        foreach (var claim in context.User.Claims)
        {
            claimsHtml += $"<li><strong>{claim.Type}</strong>: {claim.Value}</li>";
        }
        claimsHtml += "</ul>";
    }

    //构造一个Html页面,包含一个链接定向到weatherforecast:
    var html = @$"
        <div>
        <a href='{configuration["Oidc:Host"]}/openapi/v1.json'>服务端接口</a><br/>
        <a href='{configuration["Oidc:Host"]}/.well-known/openid-configuration'>服务端OIDC端点</a><br/>
        <a href='/account/login'>登录</a><br/>
        <a href='/account/logout'>登出</a><br/>
        <a href='/weatherforecast'>访问天气预报(需要登录)</a><br/>
        <a href='/weatherforecast/test-api'>测试获取令牌并使用该令牌访问服务端的接口</a><br/>
        <a href='/weatherforecast/redis-test'>AspireRedis服务测试(需要登录)</a><br/>
        <a href='/search?query=first'>检索文档服务(需要登录)</a><br/>
        </div>
        <hr
        <div>
        {(context.User.Identity?.IsAuthenticated is true ? context.User.Identity.Name : "未登录")}
        </div>
        {claimsHtml}";

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(html);
});


app.Run();
