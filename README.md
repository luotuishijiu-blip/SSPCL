# sspcl · 远行星号启动器兼 MOD 管理器

基于 [PCL2（Plain Craft Launcher 2）](https://github.com/Hex-Dragon/PCL2) 移植的**远行星号（Starsector）**专用启动器与 MOD 管理器，沿用 PCL 原版界面风格。

- 目标版本：Starsector **0.98a-RC8**
- 运行环境：.NET Framework 4.8（Windows）

---

## 功能

- **版本隔离**：多游戏目录、多实例并存，各自独立配置。
- **MOD 管理**：读取 `enabled_mods.json` 动态启用/禁用，支持 mod 池、直链安装。
- **内存分配**：仿照 PCL 原版的实时内存跟踪与分档分配，支持「跟随全局设置」。
- **存档管理**：扫描、备份、隔离存档。
- **AI 配装**：可视化舰船武器槽位编辑器，支持启发式配装与 OpenAI 兼容 API 配装。
- **启动动画**：启动时显示头像舰船图（72×72），淡出效果仿照 PCL 原版。
- **热启动优化**：AppCDS 类数据共享（`starsector.jsa`），加速二次启动。
- **隐私**：不采集、不记录、不上传激活码或任何玩家身份信息（默认关闭遥测）。

---

## 构建

依赖：Windows、Visual Studio 2022+（含 .NET Framework 4.8 目标包）、.NET 8 SDK 和 PowerShell 7。

```powershell
pwsh -File eng\check-structure.ps1
pwsh -File eng\build.ps1 -Configuration Release
```

也可以使用 `-Target Legacy` 或 `-Target Desktop` 只构建一个桌面入口。旧版主程序会自动从核心库源码生成并嵌入 `netstandard2.0` DLL，不需要手工复制。主要产物为 `src\Sspcl.App\bin\Sspcl.exe` 与 `src\Sspcl.Desktop\bin\Release\net8.0-windows\Sspcl.Desktop.exe`。

---

## 目录结构

```
├─ src\Sspcl.App\                 主程序（VB.NET / WPF）
│   ├─ Framework\               旧版通用控件与服务
│   ├─ Features\Starsector\     游戏页面与适配层
│   ├─ Features\Settings\       设置模块
│   ├─ Shell\                   主窗口与导航
│   ├─ Infrastructure\          宿主集成
│   └─ Compatibility\           PCL2 兼容代码
├─ src\Sspcl.Foundation\          基础库（.NET Standard 2.0）
├─ src\Sspcl.Foundation.Wpf\      WPF 基础库（.NET Framework 4.8）
├─ src\Sspcl.Java\                Java 启动封装库（.NET Framework 4.8）
├─ src\Sspcl.Core\                核心库（Install/Launch/Mods/Saves/…）
├─ src\Sspcl.Cli\                 命令行工具
├─ src\Sspcl.Desktop\             演示桌面应用（.NET 8 WPF）
├─ eng\                           构建与结构检查脚本
├─ docs\ARCHITECTURE.md          分层与依赖规则
└─ Sspcl.sln
```

---

## 说明

- 本项目移植自 PCL2，保留了其界面风格与交互；远行星号相关功能为新增。
- 游戏激活码由游戏本体管理，本启动器不触碰、不记录。
- AI 配装需要自行配置 OpenAI 兼容 API 地址与密钥，请求仅发送到你指定的端点。
