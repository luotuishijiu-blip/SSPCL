# 舰船装配工坊

入口：选择本机游戏版本后，进入「其他 → 百宝箱 → 舰船装配工坊」。旧 AI 装配实现移入 `Compatibility/Uncompiled/LegacyLoadouts`，仅作参考，不参与编译。

## 使用

1. 搜索舰船名称、ID 或来源，选择船体。目录只读取原版与当前 `enabled_mods.json` 中启用的 MOD；皮肤继承船体并应用槽位、内置武器和 OP 的改动。
2. 点击舰船上的槽位圆点，或右侧槽位列表。右侧只列出类型、尺寸兼容的武器；双击或点击「安装武器」应用。内置武器只读。
3. 手工安装或清空会锁定槽位，后续生成保留该选择；取消勾选可以让生成器重新选择。锁定的空槽也会保留。
4. 选择均衡、远程、突击或防御风格，点击「智能规则装配」即可离线生成，并在剩余 OP 内按武器幅能补充通风。配置名、通风、电容可编辑；支持撤销和重做。滚轮缩放，中键拖动，「适应画布」复位。
5. 「AI 设置」填写完整 `chat/completions` URL、模型名与密钥。只有点击「AI 建议」才发送当前船体、兼容武器与装配数据；不发送本地文件路径或存档。密钥可选择由当前 Windows 用户加密保存。取消、超时、非法返回或生成期间修改装配都会保留当前方案。
6. 「导出 .variant」生成游戏配置；「导入 .variant」可载入当前船体的武器配置。导入的武器默认锁定。

`.variant` 不是存档文件，也不是可一键安装的 MOD。可将导出文件放入自建 MOD 的 `data/variants`，文件名与 `variantId` 一致，再由该 MOD 的舰队、市场或生成逻辑引用。游戏重新加载 MOD 后才能使用。工坊不直接修改游戏目录或存档。

当前范围：武器、武器组、通风和电容；内置武器保留在导出武器组中。额外船插、S 插、舰载机尚未编辑，含这些内容的配置会拒绝导入，防止丢失内容或漏算 OP。含 `STATION_MODULE` 的舰船可预览，无法导出缺少子模块的完整配置。武器显示静态贴图，不模拟开火、后坐力或舰船脚本；幅能为长期射击估算值，考虑已知弹药再生上限，包括已知内置武器，不含导弹，不模拟船插与脚本。实际战斗效果需要在游戏中检验。

## 模块边界

- `src/Sspcl.Core/Loadouts`：游戏文件读取、目录快照、皮肤继承、槽位坐标、兼容性与 OP 校验、离线生成、AI 建议校验及 variant 读写。无 WPF、无全局启动器状态。
- `src/Sspcl.App/Features/Starsector/Loadouts`：WPF 工作区、选中与安装操作、撤销重做、共用的画布变换、贴图加载、文件对话框和 AI 设置。
- 两种生成方式返回相同的 `LoadoutPlan`。UI 仅在结果有效且当前装配未变化时应用；原始槽位 ID 始终用于安装、显示和导出。

船体像素坐标：`x = center[0] - slotY`，`y = height - center[1] - slotX`。武器旋转为 `-angle`；炮塔锚点为贴图中心，固定武器为宽度一半、高度 3/4。船体、武器、标记使用同一画布缩放，不裁剪槽位坐标。`numFrames` 可能代表独立帧文件，因此不把首帧贴图按该数值盲目切分。

坐标和锚点依据本机 `.ship` / `.wpn` 数据，并核对了 [Ship-Editor 的船体坐标转换](https://github.com/Ontheheavens/Ship-Editor/blob/master/src/main/java/oth/shipeditor/components/viewer/layers/ship/ShipPainterInitialization.java) 与 [武器贴图锚点](https://github.com/Ontheheavens/Ship-Editor/blob/master/src/main/java/oth/shipeditor/components/viewer/layers/weapon/WeaponSprites.java)。未引入该项目代码或依赖。

## 验证

`eng/build.ps1 -Configuration Release -Target Legacy` 包括离线检查：带引号的 CSV、宽松 JSON、启用来源覆盖、皮肤、内置武器、镜像坐标与锚点、类型/尺寸、OP 上限、锁定空槽、variant 往返，以及 AI 非法结果和取消。

`dotnet run --project tests/Sspcl.Core.Forum.Tests -c Release -- --live-loadout` 只读本机 `D:/Starsector`，检查攻势级实际坐标、XIV 皮肤及 100 艘舰船的有效生成结果。其他位置可通过工坊重载查看解析提示。

`powershell.exe -NoProfile -STA -File eng/smoke-loadout.ps1 -Executable src/Sspcl.App/bin/Sspcl.exe -GamePath D:/Starsector` 检查内嵌依赖、WPF 控件、槽位点击、实际贴图层坐标与角度、手工安装、锁定、撤销重做与导出，并离屏生成预览到 `release/loadout-preview.png`。这些检查不启动游戏，不等同于游戏内战斗测试；外部 AI 用模拟服务验证协议，不使用玩家密钥消费额度。
