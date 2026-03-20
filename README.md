# FastCli

[English Version](./README.en.md)

FastCli 是一个基于 `C# / .NET 8 / WPF / SQLite` 的 Windows 命令工作台，用来集中管理和执行本地 CLI 命令。

它适合把常用脚本、开发命令、运维命令和交互式命令整理成可复用的命令卡片，减少反复切换终端、手动复制命令和临时查找参数的成本。

## 特性

- 分组管理：新建、重命名、删除、拖拽排序
- 命令管理：新建、复制、删除、编辑、组内拖拽排序
- 跨组移动：直接把命令拖到目标分组
- 多种 Shell：`cmd`、`Windows PowerShell`、`pwsh`、直接启动程序
- 两种执行模式：
  - `应用内部执行`：实时显示输出，适合普通命令
  - `外部终端执行`：适合交互式命令
- 环境变量配置：可为单条命令设置独立环境变量
- 执行历史：保存最近执行记录、摘要和终端输出
- 版本更新：启动后自动检测 GitHub Releases 中是否有新版本
- 终端日志体验：
  - 执行开始 / 结束自动插入系统提示
  - 系统提示与错误输出使用不同颜色区分
  - 支持复制与清空当前日志
- 本地持久化：数据保存在本地 `SQLite`
- 主题切换：支持浅色 / 深色主题

## 适用场景

- 本地开发常用命令面板
- 前后端项目启动命令收纳
- 运维脚本和批处理脚本整理
- AI/CLI 工具命令管理
- 需要频繁重复执行的本地工具命令

## 技术栈

- `C#`
- `.NET 8`
- `WPF`
- `SQLite`

## 运行环境

- Windows 10 / 11
- `.NET 8 SDK`

## 快速开始

### 1. 克隆仓库

```powershell
git clone https://github.com/mostbean-cn/fast-cli.git
cd fast-cli
```

### 2. 使用 Visual Studio 或命令行运行

```powershell
dotnet run --project .\FastCli.Desktop\FastCli.Desktop.csproj
```

也可以直接打开 [FastCli.sln](./FastCli.sln) 进行开发。

## 数据存储

- 应用启动时会自动加载内嵌的 [sql/001_init.sql](./sql/001_init.sql) 初始化本地数据库
- 本地数据库默认路径：
  - `%LOCALAPPDATA%\FastCli\fastcli.db`
- 选择状态缓存路径：
  - `%LOCALAPPDATA%\FastCli\selection-state.json`

## 项目结构

- `FastCli.Desktop`：WPF 界面、交互逻辑、主题、ViewModel
- `FastCli.Application`：应用服务、用例编排、执行流程
- `FastCli.Domain`：领域模型与枚举
- `FastCli.Infrastructure`：SQLite 持久化、命令执行实现
- `sql`：数据库初始化脚本
- `docs`：项目文档
- `assets`：图标等静态资源

## 当前限制

- `应用内部执行 + 管理员权限` 目前不支持同时启用
- 交互式命令更适合使用 `外部终端执行`
- 当前是 Windows 桌面应用，不支持 macOS / Linux

## 开发说明

- 项目采用分层结构：
  - `Desktop -> Application -> Domain`
  - `Infrastructure` 负责提供外部能力实现
- 执行记录会持久化摘要与输出，便于查看最近命令历史
- 命令数据保存在本地，不依赖服务端

## 相关文档

- 版本说明：[docs/fastcli-v1.md](./docs/fastcli-v1.md)

## License

当前仓库未声明 License。如需开源发布，建议补充 `LICENSE` 文件。
