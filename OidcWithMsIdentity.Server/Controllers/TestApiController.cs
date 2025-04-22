// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcWithMsIdentity.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TestApiController : ControllerBase
{
    #region 使用Oidc的令牌访问

    // 使用 OpenIddict 验证方案保护此端点
    [HttpGet("secure")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public IActionResult GetSecuredData()
    {
        // 返回用户信息和声明作为验证成功的证明
        return Ok(new
        {
            Message = "您已成功通过 OpenIddict 验证访问此 API!",
            // UserId 存在于sub和NameIdentifier声明中
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(Claims.Subject) ?? "无用户ID",
            UserName = User.Identity?.Name ?? "无用户名",
            UserClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }

    // 使用 API 范围保护此端点
    [HttpGet("scoped")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Policy = "ApiScope")]
    public IActionResult GetScopedData()
    {
        return Ok(new
        {
            Message = "您已通过 API 范围授权访问此端点!",
            Scopes = User.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToList()
        });
    }

    #endregion

    // 使用 Microsoft Identity 获取的令牌方式访问
    [HttpGet("default-identity")]
    [Authorize(AuthenticationSchemes = "Identity.Bearer", Policy = "user")]
    public IActionResult Get()
    {
        // 返回用户信息和声明作为验证成功的证明
        return Ok(new
        {
            Message = "您已成功通过 Identity 验证访问此 API!",
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "无用户ID",
            UserName = User.Identity?.Name ?? "无用户名",
            UserClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }
}
