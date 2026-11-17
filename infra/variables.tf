variable "region" {
  description = "部署区域"
  type        = string
  default     = "cn-north-4"
}

variable "app_name" {
  description = "应用前缀；所有资源名称均由该前缀派生"
  type        = string
  default     = "dpa-rest"

  validation {
    condition     = can(regex("^[a-zA-Z][a-zA-Z0-9-]{2,30}$", var.app_name))
    error_message = "app_name 必须以字母开头，仅含字母、数字、连字符，长度为 3~31。"
  }
}

variable "apig_instance_id" {
  description = "APIG 专享实例 ID（由外部集中创建后传入，本目录不创建实例）"
  type        = string

  validation {
    condition     = length(var.apig_instance_id) > 0
    error_message = "apig_instance_id 不能为空。"
  }
}

variable "shared_apig_env_id" {
  description = "已存在的共享 APIG 发布环境 ID；非空时直接引用该环境，为空时本目录自建一个环境"
  type        = string
  default     = ""
}

variable "apig_env_name" {
  description = "shared_apig_env_id 为空时，本目录自建环境的名称"
  type        = string
  default     = "prod"
}

variable "products_table" {
  description = "GeminiDB（DynamoDB 兼容接口）产品表名；必须以 dpa_ 前缀开头"
  type        = string
  default     = "dpa_products"

  validation {
    condition     = can(regex("^dpa_[A-Za-z0-9_]{1,50}$", var.products_table))
    error_message = "products_table 必须以 dpa_ 前缀开头，仅含字母、数字、下划线。"
  }
}

variable "gemini_db_endpoint" {
  description = "GeminiDB 数据面访问地址（主机名或含协议的 URL）"
  type        = string

  validation {
    condition     = length(var.gemini_db_endpoint) > 0
    error_message = "gemini_db_endpoint 不能为空。"
  }
}

variable "gemini_db_port" {
  description = "GeminiDB 数据面端口"
  type        = number
  default     = 8000
}

variable "gemini_db_protocol" {
  description = "GeminiDB 数据面协议（http 或 https）"
  type        = string
  default     = "http"

  validation {
    condition     = contains(["http", "https"], lower(var.gemini_db_protocol))
    error_message = "gemini_db_protocol 必须是 http 或 https。"
  }
}

variable "gemini_db_region" {
  description = "GeminiDB 兼容接口签名所用区域；留空时使用部署区域"
  type        = string
  default     = ""
}

variable "gemini_access_key" {
  description = "GeminiDB 兼容接口访问密钥 ID"
  type        = string
  sensitive   = true
}

variable "gemini_secret_key" {
  description = "GeminiDB 兼容接口访问密钥 Secret"
  type        = string
  sensitive   = true
}

variable "gemini_proxy_url" {
  description = "共享数据面代理函数的公网 APIG 入口 URL；留空时建表脚本直连签名"
  type        = string
  default     = ""
}

variable "gemini_proxy_token" {
  description = "代理入口校验令牌（X-Proxy-Token）；与 gemini_proxy_url 同时提供"
  type        = string
  sensitive   = true
  default     = ""
}

variable "shared_network_id" {
  description = "函数挂接的共享网络（虚拟私有云）ID；与 shared_subnet_id 成对提供时函数接入共享网络"
  type        = string
  default     = ""
}

variable "shared_subnet_id" {
  description = "函数挂接的共享子网 network_id；必须与 shared_network_id 同时填写"
  type        = string
  default     = ""

  validation {
    condition     = (var.shared_network_id == "") == (var.shared_subnet_id == "")
    error_message = "shared_network_id 与 shared_subnet_id 必须同时为空或同时填写。"
  }
}
