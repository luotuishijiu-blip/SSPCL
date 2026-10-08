# 项目结构与依赖边界

## 入口与模块

| 层 | 项目或目录 | 职责 |
| --- | --- | --- |
| 应用入口 | `Sspcl.App` | .NET Framework 4.8 的现行 VB/WPF 主程序；`Shell` 只负责窗口与导航 |
| 应用入口 | `Sspcl.Desktop`、`Sspcl.Cli` | .NET 8 桌面入口与命令行入口 |
| 业务模块 | `Sspcl.Core` | 安装检测、启动、MOD、存档、商店、舰船/武器数据；不引用任何 UI 项目 |
| 业务模块 | `Sspcl.Java` | Java 启动相关的兼容模块 |
| 通用框架 | `Sspcl.Foundation`、`Sspcl.Foundation.Wpf` | 与游戏业务无关的基础工具和 WPF 通用能力 |

旧版主程序内部按用途排列：`Framework` 放通用控件和服务；`Features/Starsector` 放游戏功能及其页面；`Features/Settings` 放设置；`Shell` 放主窗口和导航；`Infrastructure` 放宿主集成；`Compatibility` 放从 PCL2 保留的兼容占位代码。`Compatibility/Uncompiled` 中的旧滑块控件原本没有列入项目文件，也没有调用方，因此明确作为未编译的历史源码保存。这里保留了原有命名空间与 XAML 类名，减少迁移对运行行为的影响。

## 依赖方向

```text
Sspcl.Foundation ← Sspcl.Foundation.Wpf ← Sspcl.Java
       ↑                    ↑                    ↑
       └────────────────────┴──────────── Sspcl.App

Sspcl.Core ← Sspcl.Desktop / Sspcl.Cli
     ↑
Sspcl.App（.NET Standard 2.0 目标程序集）
```

新增游戏规则和文件解析优先放进 `Sspcl.Core`，供不同入口复用。UI 项目只处理交互和呈现。通用框架不得反向引用游戏模块或应用入口。旧版 `ModStarsector` 是连接 `Sspcl.Core` 与现有 VB 页面的一层适配代码，已按舰船目录、资源解析、启发式配装和配装规划拆成多个 Partial Module 文件。后续新增功能应直接进入核心库，再在适配层提供薄包装。

旧版应用使用 `Resources.resx` 把核心 DLL 嵌入单文件程序。`Sspcl.App.vbproj` 会在编译前从 `Sspcl.Core` 的源码构建 `netstandard2.0` 目标并生成该资源文件；不再提交或手动复制生成的 DLL。其他随项目保存的第三方二进制资源仍按原有方式引用。

## 开发流程

1. 执行 `pwsh -File eng/check-structure.ps1` 检查解决方案路径、旧版项目条目和项目依赖边界。
2. 执行 `pwsh -File eng/build.ps1` 还原依赖并串行构建两个桌面入口和 CLI。可以用 `-Target Desktop` 或 `-Target Legacy` 只构建一个入口。
3. 新增功能时先确定属于 `Core` 的哪一模块，再在入口项目中添加页面或命令；新增项目引用需同步审查依赖边界。

构建需 Windows、.NET 8 SDK、Visual Studio 的 MSBuild 与 .NET Framework 4.8 目标包。项目目前仍有旧版 VB 大文件和内置第三方 DLL，这部分后续可按功能继续迁移，不应在纯目录重排时同时改动其运行逻辑。
