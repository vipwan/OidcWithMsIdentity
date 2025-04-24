// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Meilisearch;
using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Services;

public interface IBlogSearchService
{
    Task InitializeIndexAsync();
    Task AddOrUpdateDocumentAsync(Blog blog);
    Task DeleteDocumentAsync(int id);
    Task<ISearchable<Blog>> SearchBlogsAsync(string query, int page = 1, int pageSize = 10,
        string? filter = null, string? sort = null);
}
