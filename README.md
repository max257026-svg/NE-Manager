# NE Manager

终极 Windows 系统管理工具 —— C# 12 / .NET 8 / WPF 写的模块化 Windows 系统工具箱。

## 技术栈

- **语言**：C# 12 / .NET 8
- **UI 框架**：WPF (XAML) · One Dark Pro 深色主题
- **架构**：三层分离
  - NE.Native — Win32 API / NT Native API 原生封装（kernel32 / advapi32 / ntdll / rstrtmgr）
  - NE.Core — 核心解析引擎（PE / DEX / ARSC / 注册表 / WMI / 内存 / 注入）
  - NE.Manager — WPF 前端（26 个功能页面）
- **发布**：自包含单文件 EXE · win-x64 · 无需预装 .NET Runtime

## 功能

文件与系统（5）· 系统工具（4）· 逆向与安全（6）· 开发工具（7）= **26 个模块**

- 文件管理 / 权限与提权 / 注册表编辑器 / 启动项管理 / 一键清理
- 进程管理器 / 服务管理器 / 磁盘与卷 / WMI 控制台
- PE 文件分析 / HEX 编辑器 / 文本编辑器 / 内存修改 / DLL 注入器 / APK 解析 / 安全审计 / Diff 对比
- 实时仪表盘 / 归档浏览 / 网络文件 / 脚本引擎 / 批量重命名 / 数据格式化 / Linux FS / macOS FS / 日志与回滚

## 快速开始

1. 下载最新 Release 的 ZIP：<https://github.com/max257026-svg/NE-Manager/releases>
2. 解压后运行 NE.Manager.exe
3. 部分功能（注入器、内存修改、磁盘扇区编辑）需要**管理员权限**或 **SeDebug 特权**

## 系统要求

- Windows 10/11 x64
- 自包含单文件 EXE，无需预装 .NET Runtime
- 部分高危操作建议以管理员身份运行

## 构建

`ash
dotnet build src/NEManager.sln -c Release
dotnet publish src/NE.Manager/NE.Manager.csproj -c Release -r win-x64 /p:PublishSingleFile=true /p:SelfContained=true /p:EnableCompressionInSingleFile=true
`

## 版本历史

详见 [CHANGELOG.md](CHANGELOG.md)。

## 宣传片文案

详见 [PROMO-SCRIPT.md](PROMO-SCRIPT.md)。

## 下载

- GitHub Releases（源码 + Release）：<https://github.com/max257026-svg/NE-Manager/releases>
- 123云盘（永久直链）：（待填）
- 夸克网盘（永久直链）：（待填）

---

© NewEra Studio
