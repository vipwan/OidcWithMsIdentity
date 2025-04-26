// Licensed to the OidcWithMsIdentity.Client under one or more agreements.
// The OidcWithMsIdentity.Client licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OidcWithMsIdentity.ServiceDefaults;

/// <summary>
/// 服务URI提供器
/// </summary>
public interface IServiceUriProvider
{
    /// <summary>
    /// 获取服务URI
    /// </summary>
    /// <param name="service"></param>
    /// <returns></returns>
    string GetServiceUri(string service);
}

public class ServiceUriProvider(ILogger<ServiceUriProvider> logger, IConfiguration configuration) :
    IServiceUriProvider
{
    public string GetServiceUri(string service)
    {
        // 从环境变量获取服务URI
        //var envVars = Environment.GetEnvironmentVariables();

        // 首先尝试HTTP
        string? serviceUrl = configuration[$"services:{service}:http:0"];

        // 然后尝试HTTPS
        if (string.IsNullOrEmpty(serviceUrl))
        {
            serviceUrl = configuration[$"services:{service}:https:0"];
        }

        // 如果仍然没有找到，使用默认值
        var serviceUri = serviceUrl ?? $"http://{service}";

        logger.LogInformation("Content service URI: {Uri}", serviceUri);

        return serviceUri;
    }
}