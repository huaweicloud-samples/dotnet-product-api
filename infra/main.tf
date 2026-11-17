locals {
  shared_env_configured       = var.shared_apig_env_id != ""
  function_network_configured = var.shared_network_id != "" && var.shared_subnet_id != ""
  gemini_region               = var.gemini_db_region != "" ? var.gemini_db_region : var.region

  # 解析后的发布环境：共享环境优先，否则使用本目录自建环境。
  apig_env_id = local.shared_env_configured ? var.shared_apig_env_id : huaweicloud_apig_environment.products[0].id

  # 注入函数的环境变量（值必须为字符串）。
  function_env = {
    PRODUCT_TABLE_NAME = var.products_table
    GEMINI_ENDPOINT    = var.gemini_db_endpoint
    GEMINI_PORT        = tostring(var.gemini_db_port)
    GEMINI_PROTOCOL    = var.gemini_db_protocol
    GEMINI_REGION      = local.gemini_region
    GEMINI_ACCESS_KEY  = var.gemini_access_key
    GEMINI_SECRET_KEY  = var.gemini_secret_key
  }

  # 五个函数共用同一份构建产物，仅入口不同。
  # C# handler 形态：程序集::命名空间.类::方法。
  functions = {
    create-product = {
      handler = "ProductApi::ProductApi.Functions.CreateProduct::Handler"
    }
    get-product = {
      handler = "ProductApi::ProductApi.Functions.GetProduct::Handler"
    }
    get-products = {
      handler = "ProductApi::ProductApi.Functions.GetProducts::Handler"
    }
    update-product = {
      handler = "ProductApi::ProductApi.Functions.UpdateProduct::Handler"
    }
    delete-product = {
      handler = "ProductApi::ProductApi.Functions.DeleteProduct::Handler"
    }
  }

  api_routes = {
    create_product = {
      method   = "POST"
      path     = "/products"
      function = "create-product"
    }
    get_product = {
      method   = "GET"
      path     = "/products/{product_id}"
      function = "get-product"
    }
    get_products = {
      method   = "GET"
      path     = "/products"
      function = "get-products"
    }
    update_product = {
      method   = "PUT"
      path     = "/products/{product_id}"
      function = "update-product"
    }
    delete_product = {
      method   = "DELETE"
      path     = "/products/{product_id}"
      function = "delete-product"
    }
  }
}

# ---------------------------------------------------------------------------
# 日志：显式创建 LTS 组与每函数一个日志流，并在函数上绑定四参数，
# 否则平台不会自动建组，日志会丢失。
# ---------------------------------------------------------------------------

resource "huaweicloud_lts_group" "products" {
  group_name  = "${var.app_name}-logs"
  ttl_in_days = 7
}

resource "huaweicloud_lts_stream" "products" {
  for_each = local.functions

  group_id    = huaweicloud_lts_group.products.id
  stream_name = "${var.app_name}-${each.key}"
}

# ---------------------------------------------------------------------------
# 委托：函数数据面凭据经环境变量注入，委托仅覆盖 LTS 日志与可选的网络挂接。
# ---------------------------------------------------------------------------

resource "huaweicloud_identity_agency" "products_function" {
  name                  = "${var.app_name}-func"
  description           = "Allow the product API functions to write LTS logs"
  delegated_domain_name = "op_svc_cff"
  duration              = "FOREVER"

  project_role {
    project = var.region
    roles = concat(
      ["LTS FullAccess"],
      local.function_network_configured ? ["VPC FullAccess"] : [],
    )
  }

  lifecycle {
    ignore_changes = [delegated_service_name]
  }
}

# ---------------------------------------------------------------------------
# 函数：5 个 C#(.NET Core 8.0) 函数，共用同一份确定性 ZIP（根目录平铺），
# 各自配置不同入口。构建：python scripts/build.py --dotnet <dotnet8>
# ---------------------------------------------------------------------------

resource "huaweicloud_fgs_function" "products" {
  for_each = local.functions

  name           = "${var.app_name}-${each.key}"
  app            = "default"
  handler        = each.value.handler
  runtime        = "C#(.NET Core 8.0)"
  memory_size    = 128
  timeout        = 15
  code_type      = "zip"
  code_filename  = "function.zip"
  func_code      = filebase64("${path.module}/build/function.zip")
  user_data      = jsonencode(local.function_env)
  agency         = huaweicloud_identity_agency.products_function.name
  enable_lts_log = true
  vpc_id         = local.function_network_configured ? var.shared_network_id : null
  network_id     = local.function_network_configured ? var.shared_subnet_id : null

  log_group_id    = huaweicloud_lts_group.products.id
  log_group_name  = huaweicloud_lts_group.products.group_name
  log_stream_id   = huaweicloud_lts_stream.products[each.key].id
  log_stream_name = huaweicloud_lts_stream.products[each.key].stream_name

  depends_on = [huaweicloud_identity_agency.products_function]
}

# ---------------------------------------------------------------------------
# 数据表：经 GeminiDB DynamoDB 兼容接口创建产品表（apply 阶段执行）。
# ---------------------------------------------------------------------------

resource "terraform_data" "products_table" {
  # destroy provisioner 只能引用 self，故所需参数全部经 triggers_replace 传递。
  triggers_replace = {
    table     = var.products_table
    endpoint  = var.gemini_db_endpoint
    port      = tostring(var.gemini_db_port)
    protocol  = var.gemini_db_protocol
    region    = local.gemini_region
    ak        = var.gemini_access_key
    sk        = var.gemini_secret_key
    proxy_url = var.gemini_proxy_url
    token     = var.gemini_proxy_token
  }

  provisioner "local-exec" {
    environment = {
      TABLE_NAME         = var.products_table
      GEMINI_ENDPOINT    = var.gemini_db_endpoint
      GEMINI_PORT        = tostring(var.gemini_db_port)
      GEMINI_PROTOCOL    = var.gemini_db_protocol
      GEMINI_REGION      = local.gemini_region
      GEMINI_ACCESS_KEY  = var.gemini_access_key
      GEMINI_SECRET_KEY  = var.gemini_secret_key
      GEMINI_PROXY_URL   = var.gemini_proxy_url
      GEMINI_PROXY_TOKEN = var.gemini_proxy_token
    }
    command = "python ${abspath(path.module)}/scripts/ensure_table.py"
  }

  provisioner "local-exec" {
    when = destroy
    environment = {
      TABLE_NAME         = self.triggers_replace["table"]
      GEMINI_ENDPOINT    = self.triggers_replace["endpoint"]
      GEMINI_PORT        = self.triggers_replace["port"]
      GEMINI_PROTOCOL    = self.triggers_replace["protocol"]
      GEMINI_REGION      = self.triggers_replace["region"]
      GEMINI_ACCESS_KEY  = self.triggers_replace["ak"]
      GEMINI_SECRET_KEY  = self.triggers_replace["sk"]
      GEMINI_PROXY_URL   = self.triggers_replace["proxy_url"]
      GEMINI_PROXY_TOKEN = self.triggers_replace["token"]
    }
    command = "python ${abspath(path.module)}/scripts/ensure_table.py --delete"
  }
}

# ---------------------------------------------------------------------------
# APIG：分组 + 环境（共享优先）+ 5 个 API + 发布（实例由外部传入）。
# ---------------------------------------------------------------------------

resource "huaweicloud_apig_group" "products" {
  instance_id           = var.apig_instance_id
  name                  = "${var.app_name}-group"
  description           = "Product CRUD API group"
  domain_access_enabled = true
}

resource "huaweicloud_apig_environment" "products" {
  count = local.shared_env_configured ? 0 : 1

  instance_id = var.apig_instance_id
  name        = var.apig_env_name
  description = "Product CRUD API environment"
}

resource "huaweicloud_apig_api" "products" {
  for_each = local.api_routes

  instance_id             = var.apig_instance_id
  group_id                = huaweicloud_apig_group.products.id
  type                    = "Public"
  name                    = "${var.app_name}-${each.key}"
  request_protocol        = "HTTPS"
  request_method          = each.value.method
  request_path            = each.value.path
  security_authentication = "NONE"
  matching                = "Exact"

  func_graph {
    function_urn     = trimsuffix(huaweicloud_fgs_function.products[each.value.function].urn, ":latest")
    version          = "latest"
    network_type     = "V1"
    request_protocol = "HTTPS"
    timeout          = 15000
    invocation_type  = "sync"
  }
}

resource "huaweicloud_apig_api_publishment" "products" {
  for_each = local.api_routes

  instance_id = var.apig_instance_id
  env_id      = local.apig_env_id
  api_id      = huaweicloud_apig_api.products[each.key].id
  description = "Published by ${var.app_name}"
}

# 分组调试子域名：分组创建后即稳定，勿依赖发布资源。
data "huaweicloud_apig_groups" "products" {
  instance_id = var.apig_instance_id
  group_id    = huaweicloud_apig_group.products.id
}
