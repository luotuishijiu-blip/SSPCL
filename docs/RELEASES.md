# 发布下载

逐版改动见 [中文更新日志](CHANGELOG.zh-CN.md)，从本地 MOD 压缩包导入开始整理。

## 最新版

[2026-10-10：装配工坊筛选、对比与 D 插](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/build-20261010-loadout-inspection)

下载 `sspcl-loadout-inspection-20261010.zip`，解压运行 `Sspcl.exe`，无需管理员权限。上方数据面板更宽、更矮，下方武器列表扩大并固定操作按钮；新增悬停详情、A/B 对比、组合筛选及 D 插导入/导出。修正基础部署点字段，废船行动技能预览按每项 D 插减免 6%、最多 5 项计算。修复中文环境部分原版武器读取失败。详见 [装配说明](LOADOUTS.md)。

## 全幅画布版

[2026-10-10：装配工坊全幅画布](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/build-20261010-loadout-canvas)

下载 `sspcl-loadout-canvas-20261010.zip`，完整解压运行 `Sspcl.exe`，无需管理员权限。画布铺满工作区并与背景融合，数据和装配操作移入右上、右下可收起面板；适应画布避开面板，编辑时保留手工视角。完整描述、参数、船插和 `.variant` 功能保留。详见 [装配说明](LOADOUTS.md)。

## 船插与完整数据版

[2026-10-09：装配工坊船插与完整数据](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/build-20261009-loadout-details)

下载 `sspcl-loadout-details-20261009.zip`，解压运行 `Sspcl.exe`，无需管理员权限。舰船目录可拖动调宽和收起；支持普通船插、不限数量的内置/S 插及 `.variant` 保存，显示完整基础舰船数据、武器描述和参数，以及排除导弹的配装总护盾/装甲/结构 DPS 与幅伤比。包括此前的 MOD 商店、整合包、更新与内存概览。基础数据和理论伤害不模拟船插或 MOD 脚本的实战修正，范围与验证方式见 [装配说明](LOADOUTS.md)。

## 装配工坊初版

[2026-10-09：舰船装配工坊](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/build-20261009-loadout-workbench)

可视化武器装配、离线规则与 AI 建议、槽位锁定、撤销重做及 `.variant` 导入/导出。

## MOD 更新与内存版

[2026-10-09：MOD 更新与内存概览](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/build-20261009-mod-update-memory)

推荐下载 `sspcl-mod-update-memory-20261009.zip`，包含 EXE、运行配置和中文说明。关闭旧启动器后解压运行，无需管理员权限。运行需要 Windows 与 .NET Framework 4.8；RAR/7z 解压需要系统已安装 7-Zip 或 WinRAR。

本版包括论坛目录缓存、三项并行下载、本地压缩包导入、清单整合包、游戏下载，以及最新的 MOD 启用修复、完整更新和内存概览。更新前关闭游戏；游戏版本不匹配时保留旧版。

## 历史运行包

- [2026-10-09：本地 MOD 压缩包导入](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261009-local-mod-import)
- [2026-10-08：游戏下载取消修复](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261008-game-download-cancel)
- [2026-10-08：清单整合包与游戏下载](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261008-modpacks)
- [2026-10-08：直下载状态提示修复](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261008-store-download-status)
- [2026-10-08：并行下载与滚轮修复](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261008-store-parallel)
- [2026-10-08：论坛商店与本地缓存](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261008-store-cache)
- [2026-10-07：本地保留的原始 EXE/RAR](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/archive-20261007-local)
- [已有 v0.98.1 发布](https://github.com/luotuishijiu-blip/SSPCL/releases/tag/v0.98.1)

历史附件保留原始内容，不覆盖既有发布。历史阶段的源码快照未分别提交，`archive-*` 标签用于附件归档索引，不表示能够从该标签精确复现历史二进制。最新版标签对应本次提交的完整源码。

构建和验证见 [README](../README.md)、[论坛 API 说明](FORUM_API.md) 与 [整合包说明](MODPACKS.md)。
