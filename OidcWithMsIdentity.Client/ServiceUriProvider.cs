// Licensed to the OidcWithMsIdentity.Client under one or more agreements.
// The OidcWithMsIdentity.Client licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace OidcWithMsIdentity.Client;

// 实现服务URI提供器
public interface IServiceUriProvider
{
    string GetServiceUri(string service);
}

public class ServiceUriProvider(ILogger<ServiceUriProvider> logger) : IServiceUriProvider
{
    public string GetServiceUri(string service)
    {
        // 从环境变量获取服务URI
        //var envVars = Environment.GetEnvironmentVariables();

        // 首先尝试HTTP
        string? serviceUrl = Environment.GetEnvironmentVariable($"services__{service}__http__0");

        // 然后尝试HTTPS
        if (string.IsNullOrEmpty(serviceUrl))
        {
            serviceUrl = Environment.GetEnvironmentVariable($"services__{service}__https__0");
        }

        // 如果仍然没有找到，使用默认值
        var serviceUri = serviceUrl ?? $"http://{service}";

        logger.LogInformation("Content service URI: {Uri}", serviceUri);

        return serviceUri;
    }
}