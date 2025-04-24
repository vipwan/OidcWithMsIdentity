// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OidcWithMsIdentity.ContentService.Domains;

namespace OidcWithMsIdentity.ContentService.Repository;

public interface IContentRepository
{
    Task<Blog?> GetBlogByIdAsync(int id);

    Task<IList<Blog>> GetBlogsAsync(int pageIndex = 0, int pageSize = 10);

    Task AddBlogAsync(Blog blog);

    Task UpdateBlogAsync(Blog blog);

    Task DeleteBlogAsync(int id);

    Task<IList<Blog>> SearchBlogsAsync(string query, int pageIndex = 0, int pageSize = 10,
    string? category = null, string? sortBy = null);

}
