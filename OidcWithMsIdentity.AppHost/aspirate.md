> 1.搭建本地仓储
```cmd
docker pull registry
docker run -d -p 5000:5000 --restart=always --name registry registry:2
```

> 2.aspirate指令:
```cmd


aspirate init -cr localhost:5000 -ct latest --disable-secrets true --non-interactive

// 当前是生成k8s yaml文件
aspirate generate --image-pull-policy Always --include-dashboard true --disable-secrets true --non-interactive

aspirate apply -k docker-desktop --non-interactive


```

> or 生成docker-compose文件
```cmd
aspirate generate --output-format compose --image-pull-policy Always --include-dashboard true --disable-secrets true --non-interactive
```


> 3. 运行docker-compose
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


> 3. 帮助文档:

https://github.com/devkimchi/aspir8-from-scratch
https://github.com/prom3theu5/aspirational-manifests
