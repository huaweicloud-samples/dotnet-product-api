output "region" {
  description = "部署区域"
  value       = var.region
}

output "apig_instance_id" {
  description = "APIG 专享实例 ID"
  value       = var.apig_instance_id
}

output "apig_env_id" {
  description = "API 发布环境 ID（共享环境或本目录自建环境）"
  value       = local.apig_env_id
}

output "apig_env_name" {
  description = "自建环境名称；引用共享环境时为空，调用时请按共享环境名携带 X-Stage 请求头"
  value       = local.shared_env_configured ? "" : huaweicloud_apig_environment.products[0].name
}

output "api_base_url" {
  description = "产品 API 基地址"
  value       = "https://${data.huaweicloud_apig_groups.products.groups[0].sl_domain}"
}

output "api_paths" {
  description = "API 路由清单"
  value = {
    for key, route in local.api_routes : key => "${route.method} ${route.path}"
  }
}

output "products_table" {
  description = "产品表名"
  value       = var.products_table
}

output "function_urns" {
  description = "FunctionGraph 函数 URN"
  value = {
    for key, fn in huaweicloud_fgs_function.products : key => fn.urn
  }
}

output "log_group_id" {
  description = "LTS 日志组 ID"
  value       = huaweicloud_lts_group.products.id
}

output "log_stream_ids" {
  description = "各函数 LTS 日志流 ID"
  value = {
    for key, stream in huaweicloud_lts_stream.products : key => stream.id
  }
}

output "data_plane_status" {
  description = "数据面配置提示；端点由变量注入，不回显凭据"
  value = {
    geminidb = var.gemini_db_endpoint != "" ? "configured" : "not-configured"
  }
}
