// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Meilisearch;
using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Services;

public class BlogMeilisearchService(MeilisearchClient meilisearchClient) : IBlogSearchService
{
    private readonly MeilisearchClient _meilisearchClient = meilisearchClient;
    private const string IndexName = "blogs";

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var health = await _meilisearchClient.HealthAsync();
            return health.Status == "available";
        }
        catch
        {
            return false;
        }
    }

    public async Task RebuildIndexAsync(IEnumerable<Blog> allBlogs)
    {
        //var index = _meilisearchClient.Index(IndexName);

        // 删除并重建索引
        try
        {
            await _meilisearchClient.DeleteIndexAsync(IndexName);
        }
        catch (Exception)
        {
            // 索引可能不存在，忽略错误
        }

        // 重新创建索引并配置
        await InitializeIndexAsync();

        // 批量添加文档
        foreach (var blog in allBlogs)
        {
            await AddOrUpdateDocumentAsync(blog);
        }
    }


    public async Task InitializeIndexAsync()
    {
        // 检查索引是否已存在
        var realIndex = await _meilisearchClient.GetIndexAsync(IndexName);
        if (realIndex != null)
            return;

        var index = _meilisearchClient.Index(IndexName);

        // 定义可搜索的属性
        var searchableAttributes = new[] {
            nameof(Blog.Title).ToLower(),
            nameof(Blog.Content).ToLower(),
            nameof(Blog.Author).ToLower(),
            nameof(Blog.Category).ToLower(),
            nameof(Blog.Tags).ToLower()
        };
        await index.UpdateSearchableAttributesAsync(searchableAttributes);

        // 定义可过滤的属性
        var filterableAttributes = new[] {
            nameof(Blog.Category).ToLower(),
            nameof(Blog.Author).ToLower(),
            nameof(Blog.Tags).ToLower()
        };
        await index.UpdateFilterableAttributesAsync(filterableAttributes);

        // 定义排序属性
        var sortableAttributes = new[] {
            nameof(Blog.CreatedAt).ToLower(),
            nameof(Blog.UpdatedAt).ToLower()
        };
        await index.UpdateSortableAttributesAsync(sortableAttributes);

        // 配置分面搜索属性
        var faceting = new Faceting
        {
            SortFacetValuesBy = new Dictionary<string, SortFacetValuesByType>
            {
                { nameof(Blog.Category).ToLower(), SortFacetValuesByType.Alpha },
                { nameof(Blog.Author).ToLower(), SortFacetValuesByType.Alpha },
                { nameof(Blog.Tags).ToLower(), SortFacetValuesByType.Alpha } // 按字母顺序排序
            }
        };
        await index.UpdateFacetingAsync(faceting);

        // 配置拼写容错级别 (0-4，数字越小容错度越高)
        var typoTolerance = new TypoTolerance
        {
            Enabled = true,
            MinWordSizeForTypos = new TypoTolerance.TypoSize
            {
                OneTypo = 4,    // 4
                TwoTypos = 8,  // 8个字符以上的词允许2个拼写错误

            },
            DisableOnWords = ["大数据", "云计算"] // 这些词不启用容错
        };
        await index.UpdateTypoToleranceAsync(typoTolerance);

        // 配置同义词
        var synonyms = new Dictionary<string, IEnumerable<string>>
        {
            { "aspnet", ["asp.net", "asp net", "dotnet web" ]},
            { "csharp", ["c#", "c sharp", "dotnet" ]},
            { "javascript", ["js", "jscript", "ecmascript" ]},
            { "python", ["py", "python3", "python2" ]},
            { "java", ["java8", "java11", "java17" ]},
            { "typescript", ["ts", "typescript4.0", "typescript4.1" ]}
        };
        await index.UpdateSynonymsAsync(synonyms);

        // 配置停用词
        //var stopWords = new[] { "的", "了", "是", "在", "和", "与" };
        //await index.UpdateStopWordsAsync(stopWords);
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

    public async Task<ISearchResult<Blog>> SearchBlogsAsync(
        string query,
        int page = 1,
        int pageSize = 10,
        string? filter = null,
        string? sort = null,
        bool enableHighlight = true,
        string[]? facets = null
        )
    {
        var index = _meilisearchClient.Index(IndexName);
        var searchOptions = new SearchQuery
        {
            Page = page,
            HitsPerPage = pageSize,
            MatchingStrategy = "all" // "all" 或 "last" 或 "any"
        };

        if (!string.IsNullOrEmpty(filter))
        {
            // 过滤器格式为 "属性名 = '值'"
            // 比如限定Tags "C#" 的搜索结果: filter = "tags = 'C#'"
            // 日期范围: filter = "createdAt >= '2023-01-01' AND createdAt <= '2023-12-31'"
            // category: "category = '技术' OR category = '宅男' "

            searchOptions.Filter = filter;
        }

        if (!string.IsNullOrEmpty(sort))
        {
            searchOptions.Sort = [sort];
        }

        // 启用搜索内容高亮
        if (enableHighlight)
        {
            searchOptions.AttributesToHighlight = [nameof(Blog.Title).ToLower(), nameof(Blog.Content).ToLower()];
            searchOptions.HighlightPreTag = "<mark>";
            searchOptions.HighlightPostTag = "</mark>";
        }

        // 启用分面搜索
        if (facets?.Length > 0)
        {
            // 确保所有分面属性名也是小写的
            searchOptions.Facets = [.. facets.Select(f => f.ToLower())];
        }

        // PaginatedSearchResult类型的搜索结果.
        var searchResponse = (PaginatedSearchResult<Blog>)await index.SearchAsync<Blog>(query, searchOptions);

        // 转换为通用的 SearchResult 类型
        return new Domains.SearchResult<Blog>(
            searchResponse.Hits,
            searchResponse.TotalHits,
            searchResponse.FacetDistribution,
            pageSize,
            page,
            searchResponse.ProcessingTimeMs
        );
    }

}
