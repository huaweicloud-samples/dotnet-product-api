# 第三方运行时接口库说明

本目录下的 `HC.Serverless.Function.Common/`（4 个 C# 源文件）与
`LICENSE`（Apache-2.0 原文）来自以下开源项目，许可证为 Apache License 2.0：

- 仓库：`opentelekomcloud-community/otc-functiongraph-csharp-runtime`
- 固定提交：`b52ca97e4a0ec4c999820539460bd4ab093efa89`（2026-01-07）
- 用途：FunctionGraph C# 函数编译期必须引用 `IFunctionContext` /
  `IFunctionLogger` 等接口；部署 ZIP 中必须携带运行时上下文实现所引用的
  接口程序集。该库未发布到 nuget.org（GitHub Packages 源需要认证），
  因此按 Apache-2.0 许可 vendored 源码本地构建。

## 为什么是 HC.* 而不是 OpenTelekomCloud.*

源码用预处理指令按目标框架选择命名空间与程序集名：
`net6.0+` 输出 `OpenTelekomCloud.Serverless.Function.Common`（面向 OTC 运行时），
更早目标输出 `HC.Serverless.Function.Common`。

华为云 cn-north-4 的 `C#(.NET Core 8.0)` 运行时实测引用的是
`HC.Serverless.Function.Common, Version=1.0.0.0, PublicKeyToken=null`
（部署 OpenTelekomCloud.* 程序集时函数报
"Could not load file or assembly 'HC.Serverless.Function.Common, ...'"）。

因此本工程：

- 目标 `netstandard2.0`，使代码走 `#else` 分支得到 `HC.*` 命名空间，
  输出的 `HC.Serverless.Function.Common.dll` 可被 net8.0 函数直接引用；
- 程序集**不强命名**（运行时期望 `PublicKeyToken=null`，故不使用强名称密钥）；
- `AssemblyVersion` 固定 `1.0.0.0`，与运行时的引用版本一致；
- `HC.Serverless.Function.Common.csproj` 为本模式编写的简化构建工程，
  确定性编译，构建可重复且不依赖任何需认证的包源。
