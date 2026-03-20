# FastCli V1 说明

## 技术栈

- `C#`
- `.NET 8`
- `WPF`
- `SQLite`

## 当前实现

- 分组管理：新建、重命名、删除、拖动排序
- 命令管理：新建、复制、删除、编辑、组内拖动排序
- 跨组移动：把命令拖到左侧分组列表
- 执行模式：
  - `Embedded`：应用内执行并实时显示输出
  - `ExternalTerminal`：在外部终端启动交互式命令
- 历史记录：保存最近执行结果和输出文本
- 数据存储：本地 `SQLite`

## 目录结构

- `FastCli.Desktop`：WPF 界面与交互
- `FastCli.Application`：应用服务与用例编排
- `FastCli.Domain`：领域模型
- `FastCli.Infrastructure`：SQLite 与命令执行实现
- `sql/001_init.sql`：数据库初始化脚本

## 说明

- 应用启动时会自动执行 `sql/001_init.sql` 初始化本地 SQLite。
- 当前正式交付物包含：
  - 单文件可执行程序 `FastCli.exe`
  - 安装包 `FastCli-Setup.exe`
- V1 不支持“应用内管理员权限 + 捕获输出”的组合，这类命令请改用外部终端模式。
- `claude code` 这类交互命令建议使用 `ExternalTerminal`。
