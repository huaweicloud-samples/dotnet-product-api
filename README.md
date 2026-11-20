# Product REST API Sample (FunctionGraph + APIG + GeminiDB)

**English** | [简体中文](README.zh-CN.md)

A product CRUD REST API sample built on Huawei Cloud FunctionGraph, a dedicated APIG
(API Gateway) instance, and GeminiDB. Five C# (.NET Core 8.0) runtime functions handle
product creation, single-item retrieval, list retrieval, update, and deletion. Data is
stored in a single GeminiDB table (DynamoDB-compatible interface) and published through
APIG groups and environments.

## Architecture

```
Client (X-Stage: prod)
   │  HTTPS
   ▼
APIG dedicated instance (externally provisioned)
   ├── Group dpa-rest-group (debug subdomain)
   ├── Environment prod (can be switched to a shared environment)
   └── APIs ×5 ──► FunctionGraph C# (.NET Core 8.0) functions ×5
                     │  DynamoDB JSON API (SigV4 signing)
                     ▼
              GeminiDB instance (externally provisioned)
                     └── Table dpa_products (id hash key, on-demand billing)
```

| Layer | Cloud service | Description |
|---|---|---|
| Ingress | APIG dedicated instance | Group + environment + 5 APIs, FunctionGraph backend, synchronous invocation |
| Compute | FunctionGraph | 5 C# (.NET Core 8.0) functions, 128 MB / 15 s, one shared zip code package |
| Data | GeminiDB | DynamoDB-compatible interface, called directly from the functions with SigV4 signing |
| Logging | LTS | Explicit log group + one log stream per function, functions bound to all four parameters |
| Permissions | IAM agency | `op_svc_cff` trust, least-privilege system roles |

## Resource inventory

| Resource | Count | Notes |
|---|---|---|
| `huaweicloud_lts_group` | 1 | Log group, 7-day retention |
| `huaweicloud_lts_stream` | 5 | One log stream per function |
| `huaweicloud_identity_agency` | 1 | Function agency |
| `huaweicloud_fgs_function` | 5 | create/get/get-all/update/delete |
| `terraform_data` | 1 | Creates the table on apply, deletes it on destroy, via the compatible interface |
| `huaweicloud_apig_group` | 1 | Debug subdomain enabled |
| `huaweicloud_apig_environment` | 0 or 1 | Not created when a shared environment is referenced |
| `huaweicloud_apig_api` | 5 | Methods and paths below |
| `huaweicloud_apig_api_publishment` | 5 | Published to the resolved environment |

API routes:

| Method | Path | Function | Description |
|---|---|---|---|
| POST | `/products` | create-product | Create a product, returns 201; `id` is generated when omitted from the body |
| GET | `/products/{product_id}` | get-product | Retrieve a single product, returns 404 when absent |
| GET | `/products` | get-products | Product list, at most 20 items |
| PUT | `/products/{product_id}` | update-product | Update a product; `id` in the body must match the path |
| DELETE | `/products/{product_id}` | delete-product | Delete a product, returns 404 when absent |

The APIG instance and the GeminiDB instance are not created in this directory; they are
provisioned centrally by external infrastructure and passed in through variables.

## Directory structure

```
├── main.tf / variables.tf / outputs.tf / versions.tf
├── src/ProductApi/             # Function source (Functions/ entry points, Data/ layer, Models/)
│   └── Runtime/                # Vendored runtime interface assemblies (netstandard2.0)
├── tests/ProductApi.Tests/     # xunit unit tests
├── scripts/
│   ├── build.py                # Builds the deterministic deployment package build/function.zip
│   └── ensure_table.py         # Creates/deletes the table (invoked by Terraform local-exec)
├── build/function.zip          # Build artifact (flat archive root, no subdirectories)
├── dotnet-product-api.sln
└── global.json                 # Pins the .NET SDK to 8.0.4xx
```

## Prerequisites

1. An existing APIG dedicated instance (Basic edition or above, with a public entry point); note its instance ID.
2. An existing GeminiDB instance (DynamoDB-compatible) whose data plane endpoint is reachable
   from the functions, plus an access key that can access the instance's compatible interface.
3. .NET 8 SDK (8.0.4xx, see `global.json`), Python 3.8+, and Terraform 1.3+ installed locally.
4. (Optional) An existing shared APIG publishing environment (note its environment ID), or let
   this directory create its own `prod` environment.
5. (Optional) If the GeminiDB endpoint is a private address, the functions must be attached to the
   corresponding shared network; prepare the `network_id` of the network and subnet.

## Build and test

```bash
# Unit tests (13 cases: event parsing, SigV4 signed requests, 5 function entry branches)
dotnet test dotnet-product-api.sln -c Release

# Deterministic deployment package: --dotnet points to the .NET 8 SDK executable
python scripts/build.py --dotnet /path/to/dotnet
```

Build rules: `dotnet restore --locked-mode` (locked dependencies) followed by
`dotnet publish -c Release`. Artifacts are packed flat into `build/function.zip` sorted by
file name with fixed timestamps and permission bits, so two consecutive builds produce an
identical sha256.

## Deployment

```bash
terraform init -plugin-dir=<provider plugin cache directory>

export TF_VAR_apig_instance_id="<APIG dedicated instance ID>"
export TF_VAR_gemini_db_endpoint="<GeminiDB data plane address>"
export TF_VAR_gemini_access_key="<access key ID>"
export TF_VAR_gemini_secret_key="<access key secret>"

terraform plan
terraform apply
```

Optional variables:

| Variable | Default | Description |
|---|---|---|
| `region` | `cn-north-4` | Deployment region |
| `app_name` | `dpa-rest` | Resource name prefix |
| `shared_apig_env_id` | empty | When set, publish to this shared environment instead of creating one |
| `apig_env_name` | `prod` | Environment name when creating your own |
| `products_table` | `dpa_products` | Product table name (must use the `dpa_` prefix) |
| `gemini_db_port` | `8000` | Data plane port |
| `gemini_db_protocol` | `http` | `http` or `https` |
| `gemini_db_region` | empty (falls back to `region`) | Signing region for the compatible interface |
| `shared_network_id` / `shared_subnet_id` | empty | Set both together to attach the functions to a shared network |

## Verification

Manual invocation examples (the `X-Stage` header is mandatory for non-RELEASE environments):

```bash
BASE="https://<domain from terraform output api_base_url>"

# Create (with an explicit id, or omit it and let the function generate one)
curl -X POST "$BASE/products" -H "X-Stage: prod" -H "Content-Type: application/json" \
  -d '{"id":"p-1","name":"keyboard","price":99.5}'

# Single retrieval / list
curl "$BASE/products/p-1" -H "X-Stage: prod"
curl "$BASE/products" -H "X-Stage: prod"

# Update (id in the body must match the path)
curl -X PUT "$BASE/products/p-1" -H "X-Stage: prod" -H "Content-Type: application/json" \
  -d '{"id":"p-1","name":"mechanical keyboard","price":199}'

# Delete
curl -X DELETE "$BASE/products/p-1" -H "X-Stage: prod"
```

Function logs are available in the corresponding LTS log group and log stream
(`terraform output log_group_id`, `log_stream_ids`); business lines start with
`[PRODUCT-API] requestId=<id>`.

## Cleanup

```bash
terraform destroy
```

`destroy` removes the functions, the APIG group/environment (only if self-created)/APIs/publications,
the LTS group and streams, and the agency, and deletes the product table through
`scripts/ensure_table.py --delete`. The APIG dedicated instance and the GeminiDB instance are
external resources, are out of scope for cleanup here, and are handled according to their own
billing policies.

## Known limitations

- The data layer only uses four compatible-interface operations: GetItem/PutItem/DeleteItem/Scan.
  Updates overwrite the whole item with no atomic conditional protection.
- Listing performs a full table scan capped at 20 items, which is unsuitable for large-scale
  production workloads.
- The compatible-interface access key is injected into the functions as an environment variable;
  manage it through sensitive variables and avoid storing it in plaintext.
- Functions default to 128 MB / 15 s; .NET cold starts make the first invocation slow, so retry
  during verification.
- APIG dedicated instances are billed on demand, and leaving one idle incurs ongoing charges.
