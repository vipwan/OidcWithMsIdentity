// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Claims;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OidcWithMsIdentity.Server;

public static class OpenIddictExtensions
{
    /// <summary>
    /// 获取主体中指定类型的声明值
    /// </summary>
    public static string? GetClaim(this ClaimsPrincipal principal, string type)
    {
        return principal.FindFirst(type)?.Value;
    }

    /// <summary>
    /// 检查主体是否有指定的作用域
    /// </summary>
    public static bool HasScope(this ClaimsPrincipal principal, string scope)
    {
        return principal.HasClaim(Claims.Scope, scope);
    }

    /// <summary>
    /// 设置声明的目标位置
    /// </summary>
    public static void SetDestinations(this Claim claim, params string[] destinations)
    {
        claim.Properties[Properties.Destinations] = JsonSerializer.Serialize(destinations);
    }

    /// <summary>
    /// 获取声明的目标位置
    /// </summary>
    public static IEnumerable<string> GetDestinations(this Claim claim)
    {
        if (claim.Properties.TryGetValue(Properties.Destinations, out string? destinations))
        {
            return JsonSerializer.Deserialize<string[]>(destinations) ?? [];
        }

        return [];
    }
}

