// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc;
using OidcWithMsIdentity.ContentService.Repository;

namespace OidcWithMsIdentity.ContentService.Apis;

public static class ContentApi
{
    /// <summary>
    /// 添加博客API搜索路由
    /// </summary>
    /// <param name="routeBuilder"></param>
    public static void AddBlogApi(this IEndpointRouteBuilder routeBuilder)
    {
        //添加检索内容的API
        routeBuilder.MapGet("/blog/search",
            async ([FromServices] IContentRepository repository,
            [FromQuery] string query = "",
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? category = null,
            [FromQuery] string? sortBy = null) =>
            {
                var results = await repository.SearchBlogsAsync(query, pageIndex, pageSize, category, sortBy);

                var (List, Total) = results;//返回集合和总数

                return Results.Ok(new { List, Total });
            });
    }

}
