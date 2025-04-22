// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OidcWithMsIdentity.Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<MyUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 配置 OpenIddict 实体
        builder.UseOpenIddict();
    }
}
