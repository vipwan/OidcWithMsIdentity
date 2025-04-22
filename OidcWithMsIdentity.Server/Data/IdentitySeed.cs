// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcWithMsIdentity.Server.Data;

public static class IdentitySeed
{
    const string DefaultUser = "vipwan@sina.com";
    const string DefaultPassword = "123456";
    const string DefaultRole = "admin";//管理员角色
    const string DefaultRole2 = "test";//测试角色

    const string DefaultClientId = "client_id";
    const string DefaultClientSecret = "client_secret";

    /// <summary>
    /// 初始化默认用户
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>异步任务</returns>
    public static async Task SeedDefaultUserAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MyUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // 检查是否存在用户
        if (userManager.Users.Any())
        {
            logger.LogInformation("已存在用户，跳过初始化默认用户");
            return;
        }

        logger.LogInformation("开始初始化默认用户");

        // 创建默认用户
        var defaultUser = new MyUser
        {
            UserName = DefaultUser,
            Email = DefaultUser,
            EmailConfirmed = true,
            Qicq = "123456", // 可选字段
        };

        var result = await userManager.CreateAsync(defaultUser, DefaultPassword);

        if (result.Succeeded)
        {
            logger.LogInformation("默认用户创建成功");
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("创建默认用户失败: {Errors}", errors);
            throw new Exception($"创建默认用户失败: {errors}");
        }

        // 添加角色:
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(DefaultRole))
        {
            var role = new IdentityRole(DefaultRole);
            await roleManager.CreateAsync(role);
            logger.LogInformation("角色 {RoleName} 创建成功", DefaultRole);
        }

        // 将用户添加到角色
        if (!await userManager.IsInRoleAsync(defaultUser, DefaultRole))
        {
            await userManager.AddToRoleAsync(defaultUser, DefaultRole);
            logger.LogInformation("用户 {UserName} 添加到角色 {RoleName} 成功", defaultUser.UserName, DefaultRole);
        }
        else
        {
            logger.LogInformation("用户 {UserName} 已在角色 {RoleName} 中", defaultUser.UserName, DefaultRole);
        }

        // 添加第二个角色
        if (!await roleManager.RoleExistsAsync(DefaultRole2))
        {
            var role = new IdentityRole(DefaultRole2);
            await roleManager.CreateAsync(role);
            logger.LogInformation("角色 {RoleName} 创建成功", DefaultRole2);
        }
        // 将用户添加到第二个角色
        if (!await userManager.IsInRoleAsync(defaultUser, DefaultRole2))
        {
            await userManager.AddToRoleAsync(defaultUser, DefaultRole2);
            logger.LogInformation("用户 {UserName} 添加到角色 {RoleName} 成功", defaultUser.UserName, DefaultRole2);
        }
        else
        {
            logger.LogInformation("用户 {UserName} 已在角色 {RoleName} 中", defaultUser.UserName, DefaultRole2);
        }

    }

    // OpenIddict 初始化方法
    public static async Task SeedOpenIddictAsync(IServiceProvider serviceProvider)
    {
        var manager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        // 检查客户端应用是否已存在
        if (await manager.FindByClientIdAsync(DefaultClientId) is null)
        {
            // 创建新的客户端
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = DefaultClientId,// 测试的Id 
                ClientSecret = DefaultClientSecret,// 测试的密钥
                DisplayName = "OidcClient测试应用",
                PostLogoutRedirectUris = { new Uri("http://localhost:7125/signout-callback-oidc") },
                RedirectUris = { new Uri("http://localhost:7125/signin-oidc") },
                Permissions ={
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.PushedAuthorization,
                Permissions.Endpoints.Revocation,

                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.GrantTypes.RefreshToken,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.Implicit,

                Permissions.ResponseTypes.Code,

                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,

                "api",

                Permissions.Prefixes.Scope + "api"
            }
            });
        }

    }
}
