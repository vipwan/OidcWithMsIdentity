// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Services;

/// <summary>
/// 使用 Elasticsearch 实现的博客搜索服务
/// </summary>
public class BlogElasticsearchService : IBlogSearchService
{
    // Elasticsearch客户端实例，用于与ES服务器进行通信
    private readonly ElasticsearchClient _elasticsearchClient;
    // 日志记录器，用于记录操作日志和错误信息
    private readonly ILogger<BlogElasticsearchService> _logger;
    // 博客索引名称常量
    private const string IndexName = "blogs";

    /// <summary>
    /// 构造函数，通过依赖注入获取ElasticsearchClient和Logger
    /// </summary>
    /// <param name="elasticsearchClient">Elasticsearch客户端</param>
    /// <param name="logger">日志记录器</param>
    public BlogElasticsearchService(
        ElasticsearchClient elasticsearchClient,
        ILogger<BlogElasticsearchService> logger)
    {
        _elasticsearchClient = elasticsearchClient;
        _logger = logger;
    }

    /// <summary>
    /// 检查Elasticsearch服务的健康状态
    /// </summary>
    /// <returns>服务是否正常运行</returns>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            // 调用ES集群健康API检查服务状态
            var response = await _elasticsearchClient.Cluster.HealthAsync();
            // 返回响应是否有效，表示ES服务是否可用
            return response.IsValidResponse;
        }
        catch (Exception ex)
        {
            // 记录健康检查失败的异常信息
            _logger.LogError(ex, "Elasticsearch 健康检查失败");
            // 发生异常时返回false，表示服务不可用
            return false;
        }
    }

    /// <summary>
    /// 重建博客索引，先删除旧索引再创建新索引并添加所有博客数据
    /// </summary>
    /// <param name="allBlogs">所有需要索引的博客集合</param>
    public async Task RebuildIndexAsync(IEnumerable<Blog> allBlogs)
    {
        // 删除并重建索引
        try
        {
            // 检查索引是否存在
            var existsResponse = await _elasticsearchClient.Indices.ExistsAsync(IndexName);
            if (existsResponse.Exists)
            {
                // 如果索引存在，则删除它
                await _elasticsearchClient.Indices.DeleteAsync(IndexName);
            }
        }
        catch (Exception ex)
        {
            // 记录删除索引过程中的错误
            _logger.LogError(ex, "删除索引失败");
            // 索引可能不存在，忽略错误继续执行
        }

        // 重新创建索引并配置索引结构
        await InitializeIndexAsync();

        // 批量添加博客文档到索引
        if (allBlogs.Any())
        {
            // 创建批量操作请求对象
            var bulkRequest = new BulkRequest();

            // 遍历所有博客，将每篇博客添加到批量请求中
            foreach (var blog in allBlogs)
            {
                // 为每篇博客创建索引操作
                var operation = new BulkIndexOperation<Blog>(blog)
                {
                    Index = IndexName,  // 指定索引名称
                    Id = blog.Id.ToString()  // 使用博客ID作为文档ID
                };
                // 将操作添加到批量请求中
                bulkRequest.Operations?.Add(operation);
            }

            // 执行批量索引请求
            var bulkResponse = await _elasticsearchClient.BulkAsync(bulkRequest);

            // 检查批量操作是否成功
            if (!bulkResponse.IsValidResponse)
            {
                // 记录批量索引失败的错误信息
                _logger.LogError("批量索引创建失败: {ErrorReason}",
                    bulkResponse.DebugInformation);
            }
        }
    }

    /// <summary>
    /// 初始化Elasticsearch索引，包括设置分片、副本、分析器和字段映射
    /// </summary>
    public async Task InitializeIndexAsync()
    {
        // 首先检查索引是否已存在
        var indexExists = await _elasticsearchClient.Indices.ExistsAsync(IndexName);

        if (indexExists.Exists)
        {
            return;//已存在,直接返回
        }

        // 创建索引请求对象，指定索引名称和配置
        var createIndexRequest = new CreateIndexRequest(IndexName)
        {
            // 配置索引设置
            Settings = new IndexSettings
            {
                NumberOfShards = 1,  // 设置1个分片
                NumberOfReplicas = 0,  // 设置0个副本（适用于开发环境）
                // 配置文本分析器
                Analysis = new IndexSettingsAnalysis
                {
                    // 定义自定义分析器
                    Analyzers = new Analyzers(new Dictionary<string, IAnalyzer>
                    {
                        // 创建名为content_analyzer的自定义分析器
                        ["content_analyzer"] = new CustomAnalyzer
                        {
                            Tokenizer = "standard",  // 使用标准分词器
                            Filter = ["lowercase", "asciifolding", "stop"]  // 应用小写、ASCII转换和停用词过滤
                        }
                    })
                }
            },
            // 配置字段映射
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    // 配置Title字段为文本类型，使用自定义分析器和关键字字段
                    { nameof(Blog.Title).ToLower(), new TextProperty
                        {
                            Analyzer = "content_analyzer",  // 使用自定义内容分析器
                            Fields = new Properties
                            {
                                // 添加keyword子字段用于精确匹配和排序
                                { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                            }
                        }
                    },
                    // 配置Content字段为文本类型，使用自定义分析器
                    { nameof(Blog.Content).ToLower(), new TextProperty
                        {
                            Analyzer = "content_analyzer"  // 使用自定义内容分析器
                        }
                    },
                    // 配置Author字段为关键字类型
                    { nameof(Blog.Author).ToLower(), new KeywordProperty() },
                    // 配置Category字段为关键字类型
                    { nameof(Blog.Category).ToLower(), new KeywordProperty() },
                    // 配置Tags字段为关键字类型
                    { nameof(Blog.Tags).ToLower(), new KeywordProperty() },
                    // 配置CreatedAt字段为日期类型
                    { nameof(Blog.CreatedAt).ToLower(), new DateProperty() },
                    // 配置UpdatedAt字段为日期类型
                    { nameof(Blog.UpdatedAt).ToLower(), new DateProperty() }
                }
            }
        };

        // 执行创建索引请求
        var response = await _elasticsearchClient.Indices.CreateAsync(createIndexRequest);

        // 检查索引创建是否成功
        if (!response.IsValidResponse)
        {
            // 记录索引创建失败的错误
            _logger.LogError("创建索引失败: {ErrorReason}", response.DebugInformation);
            // 抛出异常，终止程序执行
            throw new InvalidOperationException($"创建索引失败: {response.DebugInformation}");
        }
    }

    /// <summary>
    /// 添加或更新单个博客文档到索引
    /// </summary>
    /// <param name="blog">要添加或更新的博客</param>
    public async Task AddOrUpdateDocumentAsync(Blog blog)
    {
        // 执行索引文档请求，如果文档存在则更新，不存在则添加
        var response = await _elasticsearchClient.IndexAsync(blog, idx =>
            idx.Index(IndexName).Id(blog.Id.ToString()));  // 指定索引名称和文档ID

        // 检查操作是否成功
        if (!response.IsValidResponse)
        {
            // 记录添加或更新文档失败的错误
            _logger.LogError("添加或更新文档失败: {ErrorReason}", response.DebugInformation);
        }
    }

    /// <summary>
    /// 从索引中删除指定ID的博客文档
    /// </summary>
    /// <param name="id">要删除的博客ID</param>
    public async Task DeleteDocumentAsync(int id)
    {
        var deleteRequest = new DeleteRequest(IndexName, id.ToString());
        // 执行删除文档请求
        var response = await _elasticsearchClient.DeleteAsync(deleteRequest);

        // 检查操作是否成功，忽略文档不存在的情况
        if (!response.IsValidResponse && response.Result != Result.NotFound)
        {
            // 记录删除文档失败的错误
            _logger.LogError("删除文档失败: {ErrorReason}", response.DebugInformation);
        }
    }

    /// <summary>
    /// 执行博客搜索，支持分页、过滤、排序、高亮和分面搜索
    /// </summary>
    /// <param name="query">搜索查询字符串</param>
    /// <param name="page">页码，从1开始</param>
    /// <param name="pageSize">每页记录数</param>
    /// <param name="filter">过滤条件</param>
    /// <param name="sort">排序规则</param>
    /// <param name="enableHighlight">是否启用高亮显示</param>
    /// <param name="facets">分面搜索的字段数组</param>
    /// <returns>搜索结果</returns>
    public async Task<ISearchResult<Blog>> SearchBlogsAsync(
        string query,
        int page = 1,
        int pageSize = 10,
        string? filter = null,
        string? sort = null,
        bool enableHighlight = true,
        string[]? facets = null)
    {
        // 计算分页起始位置
        var from = (page - 1) * pageSize;

        // 创建搜索请求对象
        var searchRequest = new SearchRequest<Blog>(IndexName)
        {
            From = from,  // 设置起始位置
            Size = pageSize,  // 设置返回文档数量
            Query = BuildQuery(query, filter)  // 构建查询条件
        };

        // 处理排序选项
        var sortOptions = BuildSort(sort);
        if (sortOptions != null && sortOptions.Count > 0)
        {
            // 如果有排序选项，设置搜索请求的排序规则
            searchRequest.Sort = sortOptions;
        }

        // 处理高亮显示
        if (enableHighlight)
        {
            // 配置高亮显示选项
            searchRequest.Highlight = new Highlight
            {
                // 指定需要高亮显示的字段及其配置
                Fields = new Dictionary<Field, HighlightField>
                {
                    // 配置标题字段的高亮显示
                    [nameof(Blog.Title).ToLower()!] = new HighlightField
                    {
                        PreTags = ["<mark>"],  // 高亮前缀标签
                        PostTags = ["</mark>"]  // 高亮后缀标签
                    },
                    // 配置内容字段的高亮显示
                    [nameof(Blog.Content).ToLower()!] = new HighlightField
                    {
                        PreTags = ["<mark>"],  // 高亮前缀标签
                        PostTags = ["</mark>"],  // 高亮后缀标签
                        FragmentSize = 150,  // 每个高亮片段的最大长度
                        NumberOfFragments = 3  // 返回最多3个高亮片段
                    }
                }
            };
        }

        // 处理分面搜索
        if (facets != null && facets.Length > 0)
        {
            // 初始化聚合字典
            searchRequest.Aggregations = new Dictionary<string, Aggregation>();

            // 遍历所有请求的分面字段
            foreach (var facet in facets)
            {
                // 根据字段名称添加不同的聚合配置
                if (facet.Equals(nameof(Blog.Category), StringComparison.OrdinalIgnoreCase))
                {
                    // 添加Category分面聚合
                    searchRequest.Aggregations["categories"] = new TermsAggregation
                    {
                        Field = nameof(Blog.Category).ToLower(),  // 指定聚合字段
                        Size = 20  // 返回最多20个分面值
                    };
                }
                else if (facet.Equals(nameof(Blog.Author), StringComparison.OrdinalIgnoreCase))
                {
                    // 添加Author分面聚合
                    searchRequest.Aggregations["authors"] = new TermsAggregation
                    {
                        Field = nameof(Blog.Author).ToLower(),  // 指定聚合字段
                        Size = 20  // 返回最多20个分面值
                    };
                }
                else if (facet.Equals(nameof(Blog.Tags), StringComparison.OrdinalIgnoreCase))
                {
                    // 添加Tags分面聚合
                    searchRequest.Aggregations["tags"] = new TermsAggregation
                    {
                        Field = nameof(Blog.Tags).ToLower(),  // 指定聚合字段
                        Size = 30  // 返回最多30个分面值
                    };
                }
            }
        }

        // 执行搜索请求
        var searchResponse = await _elasticsearchClient.SearchAsync<Blog>(searchRequest);

        // 检查搜索是否成功
        if (!searchResponse.IsValidResponse)
        {
            // 记录搜索失败的错误
            _logger.LogError("搜索失败: {ErrorReason}", searchResponse.DebugInformation);
            // 返回空的搜索结果
            return new SearchResult<Blog>(
                new List<Blog>(),
                0,
                new Dictionary<string, IReadOnlyDictionary<string, int>>(),
                pageSize,
                page);
        }

        // 获取搜索命中的文档列表
        var hits = searchResponse.Documents.ToList();

        // 处理高亮显示结果
        if (enableHighlight && searchResponse.Hits?.Count > 0)
        {
            // 遍历所有命中的文档
            foreach (var hit in searchResponse.Hits)
            {
                // 查找对应的博客对象并处理高亮内容
                if (hit.Highlight != null && hits.FirstOrDefault(b => b.Id == int.Parse(hit.Id)) is Blog blog)
                {
                    // 处理标题高亮
                    if (hit.Highlight.TryGetValue(nameof(Blog.Title).ToLower(), out var titleHighlights) && titleHighlights.Any())
                    {
                        // 将原始标题替换为高亮标题
                        blog.Title = titleHighlights.First();
                    }
                    // 处理内容高亮
                    if (hit.Highlight.TryGetValue(nameof(Blog.Content).ToLower(), out var contentHighlights) && contentHighlights.Any())
                    {
                        // 将原始内容替换为高亮内容片段拼接结果
                        blog.Content = string.Join("...", contentHighlights);
                    }
                }
            }
        }

        // 处理分面聚合结果
        var facetResults = new Dictionary<string, IReadOnlyDictionary<string, int>>();
        if (facets != null && facets.Length > 0 && searchResponse.Aggregations != null)
        {
            // 遍历所有请求的分面字段
            foreach (var facet in facets)
            {
                // 确定聚合名称
                var aggName = facet.Equals(nameof(Blog.Category), StringComparison.OrdinalIgnoreCase) ? "categories" :
                              facet.Equals(nameof(Blog.Author), StringComparison.OrdinalIgnoreCase) ? "authors" :
                              facet.Equals(nameof(Blog.Tags), StringComparison.OrdinalIgnoreCase) ? "tags" : null;

                // 获取对应的聚合结果
                if (aggName != null && searchResponse.Aggregations.TryGetValue(aggName, out var aggregation))
                {
                    // 处理术语聚合结果
                    if (aggregation is MultiTermsAggregate termsAgg)
                    {
                        // 构建分面值和计数的字典
                        var facetDict = new Dictionary<string, int>();
                        foreach (var bucket in termsAgg.Buckets)
                        {
                            // 将每个桶的键和文档计数添加到字典中
                            facetDict[bucket.Key.ToString()!] = (int)bucket.DocCount;
                        }
                        // 将分面结果添加到结果集合
                        facetResults[facet.ToLower()] = facetDict;
                    }
                }
            }
        }

        // 获取总命中数
        var totalHits = searchResponse.Total;

        // 返回搜索结果对象
        return new SearchResult<Blog>(
            hits,
            totalHits,
            facetResults,
            pageSize,
            page
        );
    }

    #region 私有辅助方法

    /// <summary>
    /// 根据查询文本和过滤条件构建Elasticsearch查询
    /// </summary>
    /// <param name="queryText">搜索查询文本</param>
    /// <param name="filter">过滤条件字符串</param>
    /// <returns>构建的查询对象</returns>
    private Query BuildQuery(string queryText, string? filter)
    {
        // 如果查询文本为空，返回匹配所有文档的查询
        if (string.IsNullOrWhiteSpace(queryText))
            return new MatchAllQuery();

        // 创建多字段匹配查询
        var multiMatchQuery = new MultiMatchQuery
        {
            // 指定搜索字段及权重
            Fields = new[] {
                $"{nameof(Blog.Title).ToLower()}^2",  // 标题字段权重为2
                nameof(Blog.Content).ToLower(),       // 内容字段
                nameof(Blog.Author).ToLower(),        // 作者字段
                nameof(Blog.Category).ToLower(),      // 分类字段
                nameof(Blog.Tags).ToLower()           // 标签字段
            },
            Query = queryText,                        // 查询文本
            Type = TextQueryType.BestFields,          // 查询类型为最佳字段
            Operator = Operator.And,                  // 使用AND操作符，要求所有词都匹配
            Fuzziness = new Fuzziness("AUTO")   // 启用自动模糊匹配，容忍拼写错误
        };

        // 如果有过滤条件，构建布尔查询
        if (!string.IsNullOrWhiteSpace(filter))
        {
            // 创建布尔查询对象
            var boolQuery = new BoolQuery();
            // 设置必须匹配的查询为多字段匹配查询
            boolQuery.Must = [multiMatchQuery];

            // 解析过滤条件
            if (filter.Contains("category = "))
            {
                // 提取分类值，去除引号
                var value = filter.Replace("category = ", "").Trim('\'');
                // 添加分类过滤条件
                boolQuery.Filter = [new TermQuery(nameof(Blog.Category).ToLower()!)
                {
                    Value = value
                }];
            }
            else if (filter.Contains("author = "))
            {
                // 提取作者值，去除引号
                var value = filter.Replace("author = ", "").Trim('\'');
                // 添加作者过滤条件
                boolQuery.Filter = [new TermQuery(nameof(Blog.Author).ToLower()!)
                {
                    Value = value
                }];
            }
            // 返回布尔查询
            return boolQuery;
        }

        // 如果没有过滤条件，直接返回多字段匹配查询
        return multiMatchQuery;
    }

    /// <summary>
    /// 根据排序字符串构建排序选项
    /// </summary>
    /// <param name="sort">排序字符串</param>
    /// <returns>排序选项列表</returns>
    private List<SortOptions>? BuildSort(string? sort)
    {
        // 如果排序字符串为空，返回null
        if (string.IsNullOrWhiteSpace(sort)) return null;

        // 创建排序选项列表
        var sortOptions = new List<SortOptions>();

        // 根据排序字符串设置不同的排序选项
        if (sort.Contains("createdAt:asc"))
        {
            var op = SortOptions.Field(Field.FromString(nameof(Blog.CreatedAt).ToLower())!, new FieldSort { Order = SortOrder.Asc });
            // 按创建时间升序排序
            sortOptions.Add(op);
        }
        else if (sort.Contains("createdAt:desc"))
        {
            var op = SortOptions.Field(Field.FromString(nameof(Blog.CreatedAt).ToLower())!, new FieldSort { Order = SortOrder.Desc });
            // 按创建时间降序排序
            sortOptions.Add(op);
        }
        else if (sort.Contains("updatedAt:asc"))
        {
            var op = SortOptions.Field(Field.FromString(nameof(Blog.UpdatedAt).ToLower())!, new FieldSort { Order = SortOrder.Asc });
            // 按更新时间升序排序
            sortOptions.Add(op);
        }
        else if (sort.Contains("updatedAt:desc"))
        {
            var op = SortOptions.Field(Field.FromString(nameof(Blog.UpdatedAt).ToLower())!, new FieldSort { Order = SortOrder.Desc });
            // 按更新时间降序排序
            sortOptions.Add(op);
        }

        // 如果有排序选项则返回，否则返回null
        return sortOptions.Count > 0 ? sortOptions : null;
    }

    #endregion
}
