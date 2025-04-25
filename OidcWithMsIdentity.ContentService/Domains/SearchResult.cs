// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace OidcWithMsIdentity.ContentService.Domains;

public interface ISearchResult<T>
{
    /// <summary>
    /// 搜索结果列表
    /// </summary>
    IReadOnlyList<T> Hits { get; }

    /// <summary>
    /// 总记录数
    /// </summary>
    long TotalHits { get; }

    /// <summary>
    /// 每页大小
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// 当前页码
    /// </summary>
    int Page { get; }

    /// <summary>
    /// 总页数
    /// </summary>
    int TotalPages { get; }

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    long ProcessingTimeMs { get; }

    /// <summary>
    /// 聚合结果
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; }
}

public class SearchResult<T> : ISearchResult<T>
{
    public IReadOnlyList<T> Hits { get; }
    public long TotalHits { get; }
    public int PageSize { get; }
    public int Page { get; }
    public int TotalPages { get; }
    public long ProcessingTimeMs { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; }

    public SearchResult(
        IEnumerable<T> hits,
        long totalHits,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> facets,
        int pageSize = 10,
        int page = 1,
        long processingTimeMs = 0)
    {
        Hits = hits.ToList();
        TotalHits = totalHits;
        PageSize = pageSize;
        Page = page;
        TotalPages = (int)Math.Ceiling((double)totalHits / pageSize);
        ProcessingTimeMs = processingTimeMs;
        Facets = facets;
    }
}