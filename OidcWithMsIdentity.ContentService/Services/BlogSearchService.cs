// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Meilisearch;
using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Services;

public class BlogSearchService(MeilisearchClient meilisearchClient) : IBlogSearchService
{
    private readonly MeilisearchClient _meilisearchClient = meilisearchClient;
    private const string IndexName = "blogs";

    public async Task InitializeIndexAsync()
    {
        var index = _meilisearchClient.Index(IndexName);

        // 定义可搜索的属性
        var searchableAttributes = new[] { "title", "content", "author", "category", "tags" };
        await index.UpdateSearchableAttributesAsync(searchableAttributes);

        // 定义可过滤的属性
        var filterableAttributes = new[] { "category", "author", "tags" };
        await index.UpdateFilterableAttributesAsync(filterableAttributes);

        // 定义排序属性
        var sortableAttributes = new[] { "createdAt", "updatedAt" };
        await index.UpdateSortableAttributesAsync(sortableAttributes);
    }

    public async Task AddOrUpdateDocumentAsync(Blog blog)
    {
        var index = _meilisearchClient.Index(IndexName);
        await index.AddDocumentsAsync([blog]);
    }

    public async Task DeleteDocumentAsync(int id)
    {
        var index = _meilisearchClient.Index(IndexName);
        await index.DeleteOneDocumentAsync(id.ToString());
    }

    public async Task<ISearchable<Blog>> SearchBlogsAsync(string query, int page = 1, int pageSize = 10,
        string? filter = null, string? sort = null)
    {
        var index = _meilisearchClient.Index(IndexName);
        var searchOptions = new SearchQuery
        {
            Page = page,
            HitsPerPage = pageSize
        };

        if (!string.IsNullOrEmpty(filter))
        {
            searchOptions.Filter = filter;
        }

        if (!string.IsNullOrEmpty(sort))
        {
            searchOptions.Sort = [sort];
        }

        return await index.SearchAsync<Blog>(query, searchOptions);
    }
}
