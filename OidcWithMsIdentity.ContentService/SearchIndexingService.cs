// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OidcWithMsIdentity.ContentService.Domains;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OidcWithMsIdentity.ContentService.Services;

/// <summary>
/// 索引操作类型
/// </summary>
public enum IndexOperation
{
    AddOrUpdate,
    Delete
}

/// <summary>
/// 搜索索引消息
/// </summary>
public class SearchIndexingMessage
{
    public IndexOperation Operation { get; set; }
    public int BlogId { get; set; }
    public Blog? BlogData { get; set; }
}


public class SearchIndexingService : BackgroundService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<SearchIndexingService> _logger;
    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private const string QueueName = "search_indexing";

    public SearchIndexingService(
        IServiceScopeFactory serviceScopeFactory,
        IConnectionFactory connectionFactory,
        ILogger<SearchIndexingService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        _connection = await _connectionFactory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
             exchange: "blog_events",
             type: ExchangeType.Direct,
             durable: true);

        await _channel.QueueDeclareAsync(
              queue: QueueName,
              durable: true,
              exclusive: false,
              autoDelete: false);

        await _channel.QueueBindAsync(
             queue: QueueName,
             exchange: "blog_events",
             routingKey: "search.index");

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        _logger.LogInformation("搜索索引服务已启动，等待消息...");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<SearchIndexingMessage>(messageJson);

                if (message != null)
                {
                    await ProcessMessageAsync(message);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    _logger.LogInformation("消息处理成功: {Operation} 博客ID: {BlogId}",
                        message.Operation, message.BlogId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息失败: {MessageJson}", messageJson);
                // 消息处理失败，重新入队
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer);

    }

    private async Task ProcessMessageAsync(SearchIndexingMessage message)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<IBlogSearchService>();

        switch (message.Operation)
        {
            case IndexOperation.AddOrUpdate:
                if (message.BlogData != null)
                {
                    await searchService.AddOrUpdateDocumentAsync(message.BlogData);
                }
                break;
            case IndexOperation.Delete:
                await searchService.DeleteDocumentAsync(message.BlogId);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
        }
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
    }
}
