// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OidcWithMsIdentity.Server.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 添加 CORS 支持,用于允许跨域请求测试
builder.Services.AddCors(options =>
{
    options.AddPolicy("all", policy =>
    {
        policy.AllowAnyOrigin() //默认允许所有来源
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


builder.Services.AddAuthentication()
    .AddCookie()
    .AddBearerToken(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(IdentityConstants.BearerScheme);

// add razor pages
builder.Services.AddRazorPages();

// add mvc
builder.Services.AddControllersWithViews();


// policy
builder.Services.Configure<AuthorizationOptions>(options =>
{
    options.AddPolicy("user", configurePolicy: policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});


var configuration = builder.Configuration;

// dbcontext
builder.Services.AddDbContext<ApplicationDbContext>((sp, builder) =>
{
    // 配置SQLite
    builder.UseSqlite(configuration.GetConnectionString("DefaultConnection"));
});

// identity 
builder.Services.AddIdentity<MyUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredUniqueChars = 0;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireDigit = false;
    o.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();
// identity api endpoints
//builder.Services.AddIdentityApiEndpoints<MyUser>();

// 添加 OpenIddict
builder.Services.AddOpenIddict()
    // 注册 OpenIddict 核心组件
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    // 注册 OpenIddict 服务器组件
    .AddServer(options =>
    {
        // 启用授权和令牌端点
        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowHybridFlow()
               .AllowPasswordFlow()
               .AllowImplicitFlow()
               .AllowRefreshTokenFlow();

        //oidc6的升级信息:
        //https://documentation.openiddict.com/guides/migration/50-to-60.html

        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserInfoEndpointUris("/connect/userinfo")
               .SetEndSessionEndpointUris("/connect/logout")
               .SetRevocationEndpointUris("/connect/revoke");

        // 使用 ASP.NET Core 端点路由
        options.UseAspNetCore()

#if DEBUG
               .DisableTransportSecurityRequirement() // 开发环境中禁用HTTPS要求
#endif
               .EnableAuthorizationEndpointPassthrough() // 启用授权端点
               .EnableTokenEndpointPassthrough()      // 启用令牌端点
               .EnableUserInfoEndpointPassthrough()   // 启用用户信息端点
               .EnableEndSessionEndpointPassthrough() // 启用登出端点
               .EnableStatusCodePagesIntegration();   // 启用状态码页面集成,非必须

        // 注册签名和加密凭据
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // 注册作用域
        options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, "api");

    })
    .AddValidation(options =>
    {
        options.UseSystemNetHttp();
        options.UseLocalServer();
        options.UseAspNetCore();
    });


// 添加授权策略
builder.Services.AddAuthorizationBuilder()
// 添加授权策略
.AddPolicy("ApiScope", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireClaim("scope", "api");
});


// open api
builder.Services.AddOpenApi("v1");


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        context.Database.EnsureCreated();

    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "数据库初始化时发生错误");
        throw; // 在开发环境中抛出异常以便查看详细错误信息
    }

    // 初始化默认用户
    await IdentitySeed.SeedDefaultUserAsync(scope.ServiceProvider);
    // 初始化 OpenIddict
    await IdentitySeed.SeedOpenIddictAsync(scope.ServiceProvider);

}


// 使用 CORS 策略
app.UseCors("all");

// oidc 强制要求https
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();// oidc 适配MVC

// add identity
app.MapGroup("account").MapIdentityApi<MyUser>();

// open api
app.MapGroup("openapi")
    .MapOpenApi("{documentName}.json");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // 显示详细异常信息
}


// Configure the HTTP request pipeline.

app.MapGet("/", async context =>
{
    // redirect to swagger
    //context.Response.Redirect("openapi/v1.json");


    context.Response.Redirect("identity/account/login");

    await Task.CompletedTask;
});

app.Run();