// Licensed to the OidcWithMsIdentity.ContentService under one or more agreements.
// The OidcWithMsIdentity.ContentService licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

var builder = DistributedApplication.CreateBuilder(args);

// 添加SQLite存储
var sqlite = builder.AddSqlite("sqlite").WithSqliteWeb();

// 添加MySQL数据库
var mysql = builder.AddMySql("mysql")
    .WithDataVolume("mysql-data")//持久化数据卷
    .AddDatabase("oidcdb");// 创建名为oidcdb的数据库

// 添加meilisearch搜索引擎
var meilisearch = builder.AddMeilisearch("meilisearch")
    .WithDataVolume("meilisearch");//持久化索引

// 由于本人开发电脑硬件脆弱跑不起来ES.如果你有条件可以使用ES,请取消下面的注释

// 添加elasticsearch搜索引擎
//var elasticsearch = builder.AddElasticsearch("elasticsearch")
//    .WithDataVolume("es-data")//持久化索引
//                              //.WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")//设置JVM内存限制
//    ;

// 添加mq
var mq = builder.AddRabbitMQ("mq")
     .WithDataVolume("rabbitmq-data")//持久化数据卷
     .WithManagementPlugin()
     .WithEnvironment("RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS", "-rabbitmq_management http_max_header_size 64000") // 增加请求头大小限制
     ;

// 添加Redis缓存
var redis = builder.AddRedis("redis");

// 添加服务端
var server = builder.AddProject<Projects.OidcWithMsIdentity_Server>("server")
    .WithReference(mysql).WaitFor(mysql)
    .WithReference(sqlite).WaitFor(sqlite) //sqlite
    ;

// 添加ContentService
var contentSvc = builder.AddProject<Projects.OidcWithMsIdentity_ContentService>("content")
    .WithReference(meilisearch).WaitFor(meilisearch)
    //.WithReference(elasticsearch).WaitFor(elasticsearch)
    .WithReference(mq).WaitFor(mq)
    ;

// 添加客户端
var client = builder.AddProject<Projects.OidcWithMsIdentity_Client>("client")
    .WithReference(redis).WaitFor(redis) //Redis缓存演练
    .WithReference(server).WaitFor(server)
    //.WithReplicas(2)//2个副本
    .WithReference(contentSvc)//客户端检索内容服务
    .WithEnvironment("CLIENT_ENV_VAR", "value")
    ;

builder.Build().Run();
