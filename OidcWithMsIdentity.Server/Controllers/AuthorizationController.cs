// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OidcWithMsIdentity.Server.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Collections.Immutable;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcWithMsIdentity.Server.Controllers;

public class AuthorizationController : Controller
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly SignInManager<MyUser> _signInManager;
    private readonly UserManager<MyUser> _userManager;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictScopeManager scopeManager,
        SignInManager<MyUser> signInManager,
        UserManager<MyUser> userManager)
    {
        _applicationManager = applicationManager;
        _authorizationManager = authorizationManager;
        _scopeManager = scopeManager;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        try
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // 验证是否已经登录
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            // 如果未登录，重定向到登录页面
            if (!result.Succeeded)
            {
                return Challenge(
                    authenticationSchemes: IdentityConstants.ApplicationScheme,
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                            Request.HasFormContentType ? Request.Form.ToList() : [.. Request.Query])
                    });
            }

            // 从身份验证结果中获取用户信息
            string? userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("The user ID cannot be retrieved.");
            }

            // 获取用户详情 - 只获取最基本信息，其他信息通过userinfo端点获取
            var user = await _userManager.FindByIdAsync(userId) ??
                throw new InvalidOperationException("The user cannot be found.");

            // 创建仅包含必要声明的身份标识
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            // 只添加必要的OIDC声明 - Subject声明是必需的
            identity.AddClaim(new Claim(Claims.Subject, user.Id));
            // 添加最小的必要信息 - Name（通常用于显示）
            identity.AddClaim(new Claim(Claims.Name, user.UserName!));

            // 创建主体
            var principal = new ClaimsPrincipal(identity);

            // 设置作用域
            principal.SetScopes(request.GetScopes());

            // 设置资源
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            // 返回签名结果
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        catch (Exception ex)
        {
            // 记录错误并重新抛出
            Console.WriteLine($"错误详情: {ex.StackTrace}");
            throw;
        }
    }



    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // 用户授权码或刷新令牌授权类型
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (result.Principal == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid.",
                    }));
            }

            // 获取用户身份
            var user = await _userManager.FindByIdAsync(result.Principal.GetClaim(Claims.Subject)!);
            if (user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid.",
                    }));
            }

            // 创建新的身份标识，确保包含正确格式的声明
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            // 添加必要的声明
            identity.AddClaim(new Claim(Claims.Subject, user.Id));
            identity.AddClaim(new Claim(Claims.Name, user.UserName!));
            identity.AddClaim(new Claim(Claims.Email, user.Email!));

            // 如果有自定义声明
            if (!string.IsNullOrEmpty(user.Qicq))
            {
                identity.AddClaim(new Claim("qicq", user.Qicq));
            }

            // 创建新的Principal
            var principal = new ClaimsPrincipal(identity);

            // 设置作用域和资源
            principal.SetScopes(result.Principal.GetScopes());
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            // 设置声明目标
            foreach (var claim in principal.Claims)
            {
                var destinations = GetDestinations(claim, principal).ToArray();
                claim.SetDestinations(destinations);
            }

            // 返回签名结果
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        // 客户端凭据授权类型
        else if (request.IsClientCredentialsGrantType())
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidClient,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client application was not found."
                    }));
            }

            // 验证客户端身份
            var application = await _applicationManager.FindByClientIdAsync(request.ClientId);
            if (application == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidClient,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client application was not found."
                    }));
            }

            // 验证客户端凭据授权类型的权限
            var clientId = await _applicationManager.GetClientIdAsync(application);
            if (!await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.ClientCredentials))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnauthorizedClient,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client is not allowed to use the client credentials grant."
                    }));
            }

            // 为客户端创建一个新的身份验证票据
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            identity.AddClaim(
                Claims.Subject,
                (await _applicationManager.GetClientIdAsync(application))!,
                Destinations.AccessToken);

            identity.AddClaim(
                Claims.Name,
                (await _applicationManager.GetDisplayNameAsync(application))!,
                Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        // 添加 Password 授权类型的处理
        else if (request.IsPasswordGrantType())
        {
            // 验证客户端 (如果 client_id 和 client_secret 已提供)
            if (!string.IsNullOrEmpty(request.ClientId))
            {
                var application = await _applicationManager.FindByClientIdAsync(request.ClientId);
                if (application == null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidClient,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client application was not found."
                        }));
                }

                // 确保客户端被允许使用密码授权类型
                if (!await _applicationManager.HasPermissionAsync(application, Permissions.GrantTypes.Password))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnauthorizedClient,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The client is not allowed to use the password grant type."
                        }));
                }
            }

            // 验证用户凭据
            var user = await _userManager.FindByNameAsync(request.Username!);
            if (user == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The username or password is invalid."
                    }));
            }

            // 验证密码
            if (!await _userManager.CheckPasswordAsync(user, request.Password!))
            {
                if (_userManager.SupportsUserLockout)
                {
                    await _userManager.AccessFailedAsync(user);
                }

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The username or password is invalid."
                    }));
            }

            if (_userManager.SupportsUserLockout)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            // 创建新的身份标识，确保包含正确格式的声明
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            // 添加必要的声明
            identity.AddClaim(new Claim(Claims.Subject, user.Id));
            identity.AddClaim(new Claim(Claims.Name, user.UserName!));
            identity.AddClaim(new Claim(Claims.Email, user.Email!));
            // 兼容Identity的NameIdentifier
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));

            // 如果有自定义声明
            if (!string.IsNullOrEmpty(user.Qicq))
            {
                identity.AddClaim(new Claim("qicq", user.Qicq));
            }

            // 添加角色声明
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any())
            {
                identity.AddClaim(new Claim(Claims.Role, string.Join(',', roles)));
            }

            // 创建新的Principal
            var principal = new ClaimsPrincipal(identity);

            // 设置作用域和资源
            principal.SetScopes(request.GetScopes());
            principal.SetResources(await ListResourcesAsync(principal.GetScopes()));

            // 设置声明目标
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }
            // 返回签名结果
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The specified grant type is not supported.",
            }));
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo")]
    public async Task<IActionResult> Userinfo()
    {
        var user = await _userManager.FindByIdAsync(User.GetClaim(Claims.Subject)!);
        if (user == null)
        {
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The specified access token is bound to an account that no longer exists.",
                }));
        }

        var claims = new Dictionary<string, object?>(StringComparer.Ordinal);

        // 无论作用域如何, 始终包含用户Id
        claims[ClaimTypes.NameIdentifier] = claims[Claims.Subject] = await _userManager.GetUserIdAsync(user);
        // 无论作用域如何，始终包含基本用户信息
        claims[ClaimTypes.Name] = claims[Claims.Name] = await _userManager.GetUserNameAsync(user);
        claims[ClaimTypes.Email] = claims[Claims.Email] = await _userManager.GetEmailAsync(user);
        claims[Claims.EmailVerified] = await _userManager.IsEmailConfirmedAsync(user);

        // 添加自定义声明
        if (!string.IsNullOrWhiteSpace(user.Qicq))
        {
            claims["qicq"] = user.Qicq;
        }

        // 添加角色声明
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Any())
        {
            // 使用,隔开的方式解决:
            claims[Claims.Role] = string.Join(',', roles);
            // 也添加.NET框架角色声明
            claims[ClaimTypes.Role] = string.Join(',', roles);
        }

        return Ok(claims);
    }


    [HttpPost("~/connect/revoke")]
    public IActionResult Revoke()
    {
        // 获取 OpenIddict 请求
        var request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // 验证请求中必须包含 token
        if (string.IsNullOrEmpty(request.Token))
        {
            return BadRequest(new OpenIddictResponse
            {
                Error = Errors.InvalidRequest,
                ErrorDescription = "The 'token' parameter is required."
            });
        }

        // token_type_hint 参数是可选的，但如果提供了，必须是有效值
        if (!string.IsNullOrEmpty(request.TokenTypeHint) &&
            !string.Equals(request.TokenTypeHint, TokenTypeHints.AccessToken, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.TokenTypeHint, TokenTypeHints.RefreshToken, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new OpenIddictResponse
            {
                Error = Errors.UnsupportedTokenType,
                ErrorDescription = "The specified token type is not supported."
            });
        }

        // 撤销令牌的处理 - OpenIddict 会自动处理令牌的验证和撤销，我们只需回应成功即可
        // 根据 RFC 7009 规范，即使令牌无效或已过期，撤销端点也应返回成功响应
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        //return Ok();
    }


    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        // 从身份认证中登出
        await _signInManager.SignOutAsync();

        // 获取请求中的post_logout_redirect_uri参数
        var postLogoutRedirectUri = Request.Query["post_logout_redirect_uri"].ToString();

        // 如果有指定的重定向地址，则重定向到该地址
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            // 验证重定向URI是否在允许列表中 (可选但推荐)
            // 此处可以添加验证逻辑

            return Redirect(postLogoutRedirectUri);
        }

        // 默认重定向到首页
        return Redirect("~/");
    }


    private IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        // 优先处理Subject声明
        if (claim.Type == Claims.Subject)
        {
            yield return Destinations.AccessToken;
            yield return Destinations.IdentityToken;
            yield break;
        }

        // 处理其他声明类型
        switch (claim.Type)
        {
            case Claims.Name:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (principal.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;
                yield break;

            // 其他声明默认只添加到访问令牌
            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }


    private async Task<List<string>> ListResourcesAsync(ImmutableArray<string> scopes)
    {
        var resources = new List<string>();
        await foreach (var resource in _scopeManager.ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }
        return resources;
    }


}
