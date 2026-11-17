# 部署与运维指南

本文说明本实践的前置依赖、构建与部署流程、验证步骤与资源清理。
架构设计见 [architecture.md](architecture.md)，功能概览见仓库根目录 `README.md`。

## 1. 前置依赖

- 华为云账号，具备 APIG、FunctionGraph、GeminiDB、LTS 与 IAM（委托）操作权限。
- 本地安装 Terraform ≥ 1.5、.NET SDK 8.0、Python 3.9+。
- 配置认证环境变量：
  ```bash
  export HW_ACCESS_KEY="<your-ak>"
  export HW_SECRET_KEY="<your-sk>"
  export HW_REGION_NAME="cn-north-4"
  ```

## 2. 构建与部署

### 2.1 构建 C# 部署包
```bash
python scripts/build.py
```
构建脚本将编译 .NET 工程并打包至 `build/function.zip`。

### 2.2 编排基础设施与函数
```bash
cd infra
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

## 3. 功能验证

执行端到端功能验证脚本：
```bash
python scripts/verify_e2e.py
```
脚本将按顺序测试商品创建（POST）、单条查询（GET）、列表获取（GET）、更新（PUT）与删除（DELETE）。

## 4. 资源清理

```bash
cd infra
terraform destroy -auto-approve
```
