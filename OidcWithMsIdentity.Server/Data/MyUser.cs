// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Identity;

namespace OidcWithMsIdentity.Server.Data;

/// <summary>
/// 扩展 IdentityUser
/// </summary>
public class MyUser : IdentityUser
{
    [PersonalData]
    public string? Qicq { get; set; }
}
