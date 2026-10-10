# sspcl · 远行星号启动器兼 MOD 管理器

基于 [PCL2（Plain Craft Launcher 2）](https://github.com/Hex-Dragon/PCL2) 移植的**远行星号（Starsector）**专用启动器与 MOD 管理器，沿用 PCL 原版界面风格。

- 目标版本：Starsector **0.98a-RC8**
- 运行环境：.NET Framework 4.8（Windows）

## 下载

- [最新版：装配工坊筛选、对比与 D 插](https://github.com/luotuishijiu-blip/SSPCL/releases/latest)，下载 ZIP 后解压运行 `Sspcl.exe`，无需管理员权限。
- [历史版本与发布说明](docs/RELEASES.md)。
- [中文更新日志](docs/CHANGELOG.zh-CN.md)：从本地 MOD 压缩包导入开始。

---

## 功能

- **版本隔离**：多游戏目录、多实例并存，各自独立配置。
- **MOD 管理**：读取 `enabled_mods.json` 动态启用/禁用，支持 mod 池、直链安装。
- **完整 MOD 更新**：支持论坛发布包与本地 ZIP/RAR/7z 更新；核对 ID、游戏版本与完整性后替换 MOD 池和当前游戏中的整个旧目录，保留启用状态。
- **本地压缩包导入**：商店支持多选导入，保留原压缩包；导入后在版本设置启用。
- **论坛 MOD 下载**：接入 fossic.org 测试 API，支持搜索、游戏版本/分类/语言筛选、发布包选择、进度与取消。完整 MOD 可装入 MOD 池，补丁与附加文件可保存压缩包；未开放直连的条目跳转发布帖。
- **缓存商店**：顶部「MOD 商店」优先显示本地目录，目录缓存 30 分钟、元数据缓存 24 小时；过期后台刷新，失败保留旧目录。支持排序、直下载筛选与更新时间提示。
- **并行下载**：最多 3 个同时下载，各项独立进度和取消；下载期间继续浏览目录，切换页面不中断任务。卡住的连接会超时，MOD 池安装按顺序进行。
- **清单整合包**：版本选择页导入/导出可选 MOD 清单、配置、游戏版本、名称和存档，禁止携带 MOD 文件。导入后从本机游戏生成独立实例，右侧显示缺失 MOD；右下角加号可下载游戏安装包。见 [整合包说明](docs/MODPACKS.md)。
- **内存分配**：仿照 PCL 原版的实时内存跟踪与分档分配，支持「跟随全局设置」。
- **内存概览**：显示当前版本 Xmx/Xms 分配值，以及运行中游戏的物理和私有内存，约每两秒刷新。
- **存档管理**：扫描、备份、隔离存档。
- **舰船装配工坊**：独立模块重写的可视化装配器，读取原版、启用 MOD 与舰船皮肤；目录可拖动调宽或收起。支持武器、普通船插和不限数量的内置/S 插编辑、锁定空槽、撤销重做、离线规则与经过本地校验的 AI 建议，导入/导出 `.variant`。显示基础舰船完整数据、武器描述及参数，以及非导弹的配装总护盾/装甲/结构 DPS 与幅伤比。见 [装配说明](docs/LOADOUTS.md)。
- **启动动画**：启动时显示头像舰船图（72×72），淡出效果仿照 PCL 原版。
- **装配筛选与对比**：组合筛选兼容武器，悬停阅读完整详情，选择 A/B 武器比较参数；支持 D 插编辑与保存，显示原始部署点和废船行动技能减免预览。
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

构建会执行离线论坛接口契约检查；真实接口联调与临时目录安装检查见 [论坛 API 接入说明](docs/FORUM_API.md)。论坛下载页面当前接入 VB/WPF 主程序，新版演示桌面入口仍使用原有索引。

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
