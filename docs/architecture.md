# 架构与技术设计

本文说明本实践的架构组成、端到端数据流与关键设计取舍。
部署命令见 [deployment-guide.md](deployment-guide.md)，功能概览见仓库根目录 `README.md`。

## 1. 分层与职责

| 层 | 职责 | 落地位置 |
|---|---|---|
| 入口层 | 对外暴露 RESTful HTTP 路由，解析请求并转发至后端函数 | `infra/main.tf` 的分组、环境、API 与发布记录 |
| 计算层 | 承载商品业务规则：创建、查询、列表、更新、删除 | `src/ProductApi/` 五个 C# 处理程序 |
| 数据层 | 商品数据的持久化存储与条件读写 | GeminiDB 兼容接口（`dpa_products` 表） |
| 观测层 | 函数运行日志采集与调用排障 | `infra/main.tf` LTS 日志组与五条日志流 |
| 权限层 | 函数访问云服务所需的委托与最小权限 | `infra/main.tf` 统一委托资源 |

## 2. 组件清单

### 2.1 函数计算（FunctionGraph）
- 5 个函数共用同一份代码包（C# / .NET Core 8.0 运行时），规格为 128 MB 内存，超时时间 15 秒。
- 函数包括：`create-product`、`get-product`、`get-products`、`update-product`、`delete-product`。

### 2.2 API 网关（APIG）
- 1 个 API 分组（开启调试子域名）、1 个运行环境（默认 `prod`）。
- 5 条 API 路由规则，后端绑定对应的 FunctionGraph 函数。

### 2.3 数据持久化（GeminiDB）
- 商品表 `dpa_products` 以 `id` 为哈希键（String 类型）。
- 函数内通过 SigV4 签名机制与 GeminiDB 兼容接口进行数据交互。
