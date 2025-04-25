// Licensed to the OidcWithMsIdentity.Server under one or more agreements.
// The OidcWithMsIdentity.Server licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Biwen.Settings;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace OidcWithMsIdentity.Server.Settings;

[Description("GithubSetting配置")]
public class GithubSetting : ValidationSettingBase<GithubSetting>
{
    [Required]
    public string Author { get; set; } = "万雅虎";

    public string HomeUrl { get; set; } = "https://github.com/vipwan";


    public string ProjectDescription { get; set; } = "当前示例项目主要是演示Oidc以及Aspire集成,搜索引擎以及各个中间件的使用!";

}
