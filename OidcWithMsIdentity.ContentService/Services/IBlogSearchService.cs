// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Services;

public interface IBlogSearchService
{
    /// <summary>
    /// 健康检测
    /// </summary>
    /// <returns></returns>
    Task<bool> HealthCheckAsync();

    Task InitializeIndexAsync();

    /// <summary>
    /// 重新构建索引
    /// </summary>
    /// <param name="allBlogs"></param>
    /// <returns></returns>
    Task RebuildIndexAsync(IEnumerable<Blog> allBlogs);

    Task AddOrUpdateDocumentAsync(Blog blog);

    Task DeleteDocumentAsync(int id);

    Task<ISearchResult<Blog>> SearchBlogsAsync(
        string query,
        int page = 1,
        int pageSize = 10,
        string? filter = null,
        string? sort = null,
        bool enableHighlight = true,
        string[]? facets = null
        );
}
