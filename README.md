# TaskDock

TaskDock 是一款完全本地运行的 Windows 边缘待办工具。它可吸附在屏幕左、右、顶部或底部，鼠标触边或按下全局快捷键即可快速查看当前工作。

## 功能

- 待处理、进行中、已完成三阶段任务流
- 左/右/顶部/底部吸附、触边滑出、固定展开、普通窗口和多显示器选择
- 全局快捷键（默认 `Ctrl + Alt + T`）与系统托盘
- 中文自然语言快速创建，例如“明天下午3点提交周报，提前30分钟提醒”
- 精确到分钟的截止时间与提醒、优先级、项目、标签和重复任务
- 可勾选子任务及卡片进度、备注网页、独立链接和本地附件快捷打开
- 每日总览、任务列表、可折叠看板、自定义月历、工作记录和回收站
- 按秒累计的任务计时、按时完成率及本周统计
- Windows 通知、提前提醒与每日回顾
- SQLite 本地数据、自动/手动备份、恢复、JSON/CSV/Markdown 导入导出
- 浅色、深色和跟随系统主题

TaskDock 不需要账号，不连接云服务，不检查更新，也不发送遥测。

## 获取与运行

- 安装版：运行 `TaskDock-Setup-0.2.0-win-x64.exe`
- 便携版：解压 `TaskDock-Portable-0.2.0-win-x64.zip` 后运行 `TaskDock.exe`

安装版的数据位于 `%LOCALAPPDATA%\TaskDock`。便携版的数据位于程序目录下的 `Data` 文件夹。

## 从源码构建

需要 .NET 8 SDK：

```powershell
dotnet restore TaskDock.sln
dotnet build TaskDock.sln -c Release
dotnet test TaskDock.sln -c Release
```

发布自包含版本：

```powershell
dotnet publish src\TaskDock\TaskDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 项目结构

- `src/TaskDock`：WPF 应用、SQLite 数据层与系统集成
- `tests/TaskDock.Tests`：自然语言解析和数据持久化测试
- `installer/TaskDock.iss`：Inno Setup 安装脚本
- `tools/GenerateIcon.ps1`：应用图标生成脚本
