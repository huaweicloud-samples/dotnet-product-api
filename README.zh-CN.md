# 产品 REST API 示例（FunctionGraph + APIG + GeminiDB）

[English](README.md) | **简体中文**

基于华为云函数工作负载 FunctionGraph、API 网关 APIG 专享实例与 GeminiDB 的产品
CRUD REST API 示例。5 个 C#(.NET Core 8.0) 运行时函数分别处理产品的创建、单个
查询、列表查询、更新与删除，数据存放在 GeminiDB（DynamoDB 兼容接口）的单表中，
通过 APIG 分组与环境对外发布。

## 架构

```
客户端 (X-Stage: prod)
   │  HTTPS
   ▼
APIG 专享实例（外部提供）
   ├── 分组 dpa-rest-group（调试子域名）
   ├── 环境 prod（可改为引用共享环境）
   └── API ×5 ──► FunctionGraph C#(.NET Core 8.0) 函数 ×5
                     │  DynamoDB JSON API（SigV4 签名）
                     ▼
              GeminiDB 实例（外部提供）
                     └── 表 dpa_products（id 哈希键，按需计费）
```

| 层次 | 云服务 | 说明 |
|---|---|---|
| 接入 | APIG 专享实例 | 分组 + 环境 + 5 个 API，FunctionGraph 后端，同步调用 |
| 计算 | FunctionGraph | 5 个 C#(.NET Core 8.0) 函数，128MB/15s，同一 zip 代码包 |
| 数据 | GeminiDB | DynamoDB 兼容接口，函数内以 SigV4 签名直连 |
| 日志 | LTS | 显式日志组 + 每函数一个日志流，函数绑定四参数 |
| 权限 | IAM 委托 | `op_svc_cff` 信任，最小化系统角色 |

## 资源清单

| 资源 | 数量 | 备注 |
|---|---|---|
| `huaweicloud_lts_group` | 1 | 日志组，保留 7 天 |
| `huaweicloud_lts_stream` | 5 | 每函数一个日志流 |
| `huaweicloud_identity_agency` | 1 | 函数委托 |
| `huaweicloud_fgs_function` | 5 | create/get/get-all/update/delete |
| `terraform_data` | 1 | apply 时经兼容接口建表，destroy 时删表 |
| `huaweicloud_apig_group` | 1 | 开启调试子域名 |
| `huaweicloud_apig_environment` | 0 或 1 | 引用共享环境时不创建 |
| `huaweicloud_apig_api` | 5 | 方法与路径见下 |
| `huaweicloud_apig_api_publishment` | 5 | 发布到解析后的环境 |

API 路由：

| 方法 | 路径 | 函数 | 说明 |
|---|---|---|---|
| POST | `/products` | create-product | 创建产品，返回 201；body 缺省 `id` 时自动生成 |
| GET | `/products/{product_id}` | get-product | 查询单个产品，不存在返回 404 |
| GET | `/products` | get-products | 产品列表，最多返回 20 条 |
| PUT | `/products/{product_id}` | update-product | 更新产品，body 中 `id` 必须与路径一致 |
| DELETE | `/products/{product_id}` | delete-product | 删除产品，不存在返回 404 |

APIG 实例与 GeminiDB 实例不在本目录创建，由外部集中提供并经变量传入。

## 目录结构

```
├── main.tf / variables.tf / outputs.tf / versions.tf
├── src/ProductApi/             # 函数源码（Functions/ 入口、Data/ 数据层、Models/）
│   └── Runtime/                # vendored 运行时接口程序集（netstandard2.0）
├── tests/ProductApi.Tests/     # xunit 单元测试
├── scripts/
│   ├── build.py                # 构建确定性部署包 build/function.zip
│   └── ensure_table.py         # 建表/删表（Terraform local-exec 调用）
├── build/function.zip          # 构建产物（根目录平铺，无子目录）
├── dotnet-product-api.sln
└── global.json                 # 固定 .NET SDK 8.0.4xx
```

## 前提条件

1. 已有一个 APIG 专享实例（基础版及以上，含公网入口），记下实例 ID。
2. 已有一个 GeminiDB 实例（DynamoDB 兼容），函数网络可达其数据面端点，
   并持有可访问该实例兼容接口的访问密钥。
3. 本机具备 .NET 8 SDK（8.0.4xx，见 `global.json`）、Python 3.8+、Terraform 1.3+。
4. （可选）已有共享的 APIG 发布环境（记下环境 ID），或让本目录自建 `prod` 环境。
5. （可选）若 GeminiDB 端点为私网地址，函数需挂接相应共享网络，
   准备网络与子网的 network_id。

## 构建与测试

```bash
# 单元测试（13 个用例：事件解析、SigV4 签名报文、5 个函数入口分支）
dotnet test dotnet-product-api.sln -c Release

# 确定性部署包：--dotnet 指向 .NET 8 SDK 的可执行文件
python scripts/build.py --dotnet /path/to/dotnet
```

构建规则：`dotnet restore --locked-mode`（锁定依赖）+ `dotnet publish -c Release`，
产物按文件名排序、固定时间戳与权限位平铺打进 `build/function.zip`，
两次构建的 sha256 完全一致。

## 部署

```bash
terraform init -plugin-dir=<provider 插件缓存目录>

export TF_VAR_apig_instance_id="<APIG 专享实例 ID>"
export TF_VAR_gemini_db_endpoint="<GeminiDB 数据面地址>"
export TF_VAR_gemini_access_key="<访问密钥 ID>"
export TF_VAR_gemini_secret_key="<访问密钥 Secret>"

terraform plan
terraform apply
```

可选变量：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `region` | `cn-north-4` | 部署区域 |
| `app_name` | `dpa-rest` | 资源名前缀 |
| `shared_apig_env_id` | 空 | 非空时发布到该共享环境，不再自建环境 |
| `apig_env_name` | `prod` | 自建环境时的名称 |
| `products_table` | `dpa_products` | 产品表名（必须 `dpa_` 前缀） |
| `gemini_db_port` | `8000` | 数据面端口 |
| `gemini_db_protocol` | `http` | `http` 或 `https` |
| `gemini_db_region` | 空（取 `region`） | 兼容接口签名区域 |
| `shared_network_id` / `shared_subnet_id` | 空 | 函数挂接共享网络时成对填写 |

## 验证

手工调用示例（非 RELEASE 环境必须携带 `X-Stage` 头）：

```bash
BASE="https://<terraform output api_base_url 的域名>"

# 创建（自带 id 或省略由函数生成）
curl -X POST "$BASE/products" -H "X-Stage: prod" -H "Content-Type: application/json" \
  -d '{"id":"p-1","name":"keyboard","price":99.5}'

# 单个查询 / 列表
curl "$BASE/products/p-1" -H "X-Stage: prod"
curl "$BASE/products" -H "X-Stage: prod"

# 更新（body 中 id 必须与路径一致）
curl -X PUT "$BASE/products/p-1" -H "X-Stage: prod" -H "Content-Type: application/json" \
  -d '{"id":"p-1","name":"mechanical keyboard","price":199}'

# 删除
curl -X DELETE "$BASE/products/p-1" -H "X-Stage: prod"
```

函数日志可在 LTS 对应日志组/日志流中查看（`terraform output log_group_id`、
`log_stream_ids`），业务行以 `[PRODUCT-API] requestId=<id>` 开头。

## 清理

```bash
terraform destroy
```

destroy 会同时删除函数、APIG 分组/环境（仅自建的）/API/发布、LTS 组流、委托，
并经 `scripts/ensure_table.py --delete` 删除产品表。APIG 专享实例与 GeminiDB
实例为外部资源，不在本目录清理范围内，按其自身计费策略自行处理。

## 已知限制

- 数据层只使用兼容接口的 GetItem/PutItem/DeleteItem/Scan 四类操作；更新为
  整条覆盖写，无原子条件保护。
- 列表为全表扫描并限制最多 20 条，不适合大数据量生产场景。
- 兼容接口访问密钥以环境变量注入函数，请通过敏感变量管理，避免明文入库。
- 函数默认 128MB/15s，.NET 冷启动首次调用耗时较长，验证时建议重试。
- APIG 专享实例按需计费，长期闲置会产生费用。
