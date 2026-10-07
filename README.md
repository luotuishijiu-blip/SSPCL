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

依赖：Visual Studio 2022+（含 .NET Framework 4.8 目标包）与 .NET 8 SDK。

```powershell
# 1) 构建 C# 核心库（Sspcl.Core，netstandard2.0），并复制 DLL 到主程序 Resources 目录
dotnet build src\Sspcl.Core\Sspcl.Core.csproj -c Release
Copy-Item src\Sspcl.Core\bin\Release\netstandard2.0\Sspcl.Core.dll `
  "PCL-main\Plain Craft Launcher 2\Resources\Sspcl.Core.dll" -Force

# 2) 构建主程序（单文件 exe）
$msb = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
& $msb "PCL-main\Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj" -t:Build -p:Configuration=Debug
```

产物：`PCL-main\Plain Craft Launcher 2\bin\Sspcl.exe`（DLL 已内嵌，单文件可分发）。

---

## 目录结构

```
├─ PCL-main\Plain Craft Launcher 2\   主程序（VB.NET / WPF）
│   ├─ Pages\PageStarsector\          远行星号各页面（启动/设置/实例/百宝箱）
│   ├─ ModStarsector.vb               游戏封装（扫描、配装、启动）
│   └─ Modules\ModMain.vb             启动器入口逻辑
├─ PCL-main\MeloongCore\              PCL 底层库（.NET）
├─ PCL-main\PCLCS\                    PCL 辅助库（Java 启动封装等）
├─ src\Sspcl.Core\                    独立核心库（Install/Launch/Mods/Saves/…）
├─ src\Sspcl.Cli\、src\Sspcl.App\     辅助命令行与演示应用
└─ Sspcl.sln
```

---

## 说明

- 本项目移植自 PCL2，保留了其界面风格与交互；远行星号相关功能为新增。
- 游戏激活码由游戏本体管理，本启动器不触碰、不记录。
- AI 配装需要自行配置 OpenAI 兼容 API 地址与密钥，请求仅发送到你指定的端点。
