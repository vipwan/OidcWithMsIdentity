var builder = DistributedApplication.CreateBuilder(args);

// 添加SQLite存储
// var sqlite = builder.AddSqliteDatabase("sqlite");

// 添加MySQL数据库
var mysql = builder.AddMySql("mysql")
    .WithDataVolume("mysql-data")//持久化数据卷
    .AddDatabase("oidcdb");
// 创建名为oidcdb的数据库

// 添加Redis缓存
var redis = builder.AddRedis("redis");
// 添加服务端
var server = builder.AddProject<Projects.OidcWithMsIdentity_Server>("server")
    .WithReference(mysql)
    ;
//.WithReference(sqlite)
//.WithEndpoint(port: 8080, targetPort: 5000, scheme: "http", name: "server-http")
//.WithEndpoint(port: 8081, targetPort: 443, scheme: "https", name: "server-https");

// 添加客户端
var client = builder.AddProject<Projects.OidcWithMsIdentity_Client>("client")
    .WithReference(redis) //Redis缓存演练
    .WithReference(server);
//.WithEndpoint(port: 8082, targetPort: 5001, scheme: "http", name: "client-http")
//.WithEndpoint(port: 8083, targetPort: 1443, scheme: "https", name: "client-https");


builder.Build().Run();
