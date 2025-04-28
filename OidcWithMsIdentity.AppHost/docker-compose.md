```cmd
aspire publish --publisher docker-compose
```

> 运行docker-compose
```cmd
// 运行docker-compose
docker-compose up -d
// 查看运行状态
docker-compose ps
// 查看特定服务的日志
docker-compose logs -f <service_name>
// 停止服务
docker-compose down
// 删除服务
docker-compose down --volumes --remove-orphans

```