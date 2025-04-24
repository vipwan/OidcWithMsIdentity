// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OidcWithMsIdentity.ContentService.Apis;
using OidcWithMsIdentity.ContentService.Repository;
using OidcWithMsIdentity.ContentService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Aspire ServiceDefaults
builder.AddServiceDefaults();

// 添加meilisearch客户端
builder.AddMeilisearchClient("meilisearch");

// 添加mq
builder.AddRabbitMQClient(
    "mq",
    configureConnectionFactory:
        static factory => factory.ClientProvidedName = "content_service");

// 注册BlogSearchService
builder.Services.AddScoped<IBlogSearchService, BlogSearchService>();
builder.Services.AddScoped<IContentRepository, ContentRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Aspire DefaultEndpoints
app.MapDefaultEndpoints();

// 路由说说服务
app.MapGroup("api").AddBlogApi();

// 主页定向到博客列表搜索
app.MapGet("/", async ctx =>
{
    ctx.Response.Redirect("api/blog/search"); await Task.CompletedTask;
});


// 应用启动时初始化Meilisearch索引
using (var scope = app.Services.CreateScope())
{
    var searchService = scope.ServiceProvider.GetRequiredService<IBlogSearchService>();
    await searchService.InitializeIndexAsync();

    // 将现有的博客数据添加到索引,这里测试,实际项目中可以使用后台任务来处理!
    var repository = scope.ServiceProvider.GetRequiredService<IContentRepository>();
    var blogs = await repository.GetBlogsAsync(0, 1000); // 获取所有博客
    foreach (var blog in blogs)
    {
        await searchService.AddOrUpdateDocumentAsync(blog);
    }
}

app.Run();

