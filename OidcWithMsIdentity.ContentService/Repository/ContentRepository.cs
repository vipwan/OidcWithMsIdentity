// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Meilisearch;
using OidcWithMsIdentity.ContentService.Domains;
using OidcWithMsIdentity.ContentService.Services;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OidcWithMsIdentity.ContentService.Repository;

public class ContentRepository(
    IConnectionFactory connectionFactory,
    ILogger<ContentRepository> logger,
    IBlogSearchService searchService) : IContentRepository
{
    //内存中模拟数据
    private static readonly List<Blog> _blogs =
    [
        new() { Id = 1, Title = "First Blog", Content = "This is the first blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="first,bolg"},
        new() { Id = 2, Title = "Second Blog", Content = "This is the second blog." , Author = "万雅虎", Category = "关于", CreatedAt =DateTime.Now, UpdatedAt=DateTime.Now,Tags = "second,bolg"},
        new() { Id = 3, Title = "Third Blog", Content = "This is the third blog." , Author = "万雅虎", Category = "关于", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="third,bolg"},
        new() { Id = 4, Title = "Fourth Blog", Content = "This is the fourth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="fourth,bolg"},
        new() { Id = 5, Title = "Fifth Blog", Content = "This is the fifth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="fifth,bolg"},
        new() { Id = 6, Title = "Sixth Blog", Content = "This is the sixth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="sixth,bolg"},
        new() { Id = 7, Title = "Seventh Blog", Content = "This is the seventh blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="seventh,bolg"},
        new() { Id = 8, Title = "Eighth Blog", Content = "This is the eighth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="eighth,bolg"},
        new() { Id = 9, Title = "Ninth Blog", Content = "This is the ninth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="ninth,bolg"},
        new() { Id = 10, Title = "Tenth Blog", Content = "This is the tenth blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="tenth,bolg"},
        new() { Id = 11, Title = "Eleventh Blog", Content = "This is the eleventh blog." , Author = "万雅虎", Category = "博客", CreatedAt=DateTime.Now,UpdatedAt= DateTime.Now , Tags="eleventh,bolg"},
        ];

    private static readonly Lock @lock = new();
    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly ILogger<ContentRepository> _logger = logger;
    private readonly IBlogSearchService _searchService = searchService;

    public async Task AddBlogAsync(Blog blog)
    {
        lock (@lock)
        {
            blog.Id = _blogs.Max(b => b.Id) + 1;
            _blogs.Add(blog);
        }

        // 发送消息到RabbitMQ来更新搜索索引
        await PublishSearchIndexingMessageAsync(new SearchIndexingMessage
        {
            Operation = IndexOperation.AddOrUpdate,
            BlogId = blog.Id,
            BlogData = blog
        });

    }

    public async Task DeleteBlogAsync(int id)
    {
        lock (@lock)
        {
            var blog = _blogs.FirstOrDefault(b => b.Id == id);
            if (blog != null)
            {
                _blogs.Remove(blog);
            }
        }

        // 发送消息到RabbitMQ来删除搜索索引
        await PublishSearchIndexingMessageAsync(new SearchIndexingMessage
        {
            Operation = IndexOperation.Delete,
            BlogId = id
        });
    }

    public Task<Blog?> GetBlogByIdAsync(int id)
    {
        Blog? blog = null;
        lock (@lock)
        {
            blog = _blogs.FirstOrDefault(b => b.Id == id);
        }
        return Task.FromResult(blog);
    }

    public Task<IList<Blog>> GetBlogsAsync(int pageIndex = 0, int pageSize = 10)
    {
        IList<Blog> blogs = [];
        lock (@lock)
        {
            blogs = _blogs.Skip(pageIndex * pageSize).Take(pageSize).ToList();
        }
        return Task.FromResult(blogs);
    }

    public async Task UpdateBlogAsync(Blog blog)
    {
        lock (@lock)
        {
            var existingBlog = _blogs.FirstOrDefault(b => b.Id == blog.Id);
            if (existingBlog != null)
            {
                existingBlog.Title = blog.Title;
                existingBlog.Content = blog.Content;
                existingBlog.Author = blog.Author;
                existingBlog.Category = blog.Category;
                existingBlog.Tags = blog.Tags;
                existingBlog.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 发送消息到RabbitMQ来更新搜索索引
        await PublishSearchIndexingMessageAsync(new SearchIndexingMessage
        {
            Operation = IndexOperation.AddOrUpdate,
            BlogId = blog.Id,
            BlogData = blog
        });
    }

    // 添加搜索方法
    public async Task<IList<Blog>> SearchBlogsAsync(string query, int pageIndex = 0, int pageSize = 10,
        string? category = null, string? sortBy = null)
    {
        string? filter = null;
        if (!string.IsNullOrEmpty(category))
        {
            filter = $"category = '{category}'";
        }

        string? sort = null;
        if (!string.IsNullOrEmpty(sortBy))
        {
            sort = sortBy;
        }

        var searchResult = await _searchService.SearchBlogsAsync(
            query,
            pageIndex + 1, // Meilisearch页码从1开始
            pageSize,
            filter,
            sort);

        return [.. searchResult.Hits];
    }

    #region 私有方法 - RabbitMQ消息发送

    private async Task PublishSearchIndexingMessageAsync(SearchIndexingMessage message)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 声明交换机和队列
            await channel.ExchangeDeclareAsync(
                  exchange: "blog_events",
                  type: ExchangeType.Direct,
                  durable: true);

            await channel.QueueDeclareAsync(
                queue: "search_indexing",
                durable: true,
                exclusive: false,
                autoDelete: false);

            await channel.QueueBindAsync(
                  queue: "search_indexing",
                  exchange: "blog_events",
                  routingKey: "search.index");

            // 序列化消息内容
            var messageBody = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageBody);

            // 发布消息
            await channel.BasicPublishAsync(
                 exchange: "blog_events",
                 routingKey: "search.index",
                 mandatory: false,
                 basicProperties: new BasicProperties(),
                 body: body);

            _logger.LogInformation("已发送索引消息: {Operation} 博客ID: {BlogId}",
                message.Operation, message.BlogId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送索引消息失败");
            // 在生产环境中可能需要重试逻辑或者将失败的消息保存到数据库以便后续处理
        }
    }

    #endregion
}