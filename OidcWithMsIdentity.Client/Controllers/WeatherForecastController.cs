// Licensed to the OidcWithMsIdentity.Client under one or more agreements.
// The OidcWithMsIdentity.Client licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OidcWithMsIdentity.Client.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(
    ILogger<WeatherForecastController> logger,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory
    ) : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet]
    [Authorize(Roles = "admin")] // 需要admin角色
    public IEnumerable<WeatherForecast> Get()
    {
        logger.LogInformation("WeatherForecastController Get method called.");

        logger.LogInformation("User: {User}", User.Identity?.Name);

        // logger claims
        foreach (var claim in User.Claims)
        {
            logger.LogInformation("Claim Type: {Type}, Claim Value: {Value}", claim.Type, claim.Value);
        }

        return [.. Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })];
    }


    // 添加测试方法
    [HttpGet("test-api")]
    [Authorize]
    public async Task<IActionResult> TestApiAccess()
    {
        // 从当前用户会话获取访问令牌
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        if (string.IsNullOrEmpty(accessToken))
        {
            return BadRequest("未找到访问令牌");
        }

        var client = httpClientFactory.CreateClient();

        // 设置请求头中的授权令牌
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var oidcHost = configuration["Oidc:Host"];

        // 调用受保护的 API
        var response = await client.GetAsync($"{oidcHost}/api/testapi/secure");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<object>(content);
            return Ok(new
            {
                Status = "成功",
                ApiResponse = result
            });
        }

        return StatusCode((int)response.StatusCode,
            new { Status = "失败", Message = response.ReasonPhrase });
    }


}