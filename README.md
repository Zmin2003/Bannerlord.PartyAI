# Party AI Controls Reforged - AI Commander CN

这是 `Party AI Controls Reforged` 的中文增强分支。它保留上游的家族部队、商队和驻军管理功能，并增加战场 AI 指挥官：AI 可以接管我方编队，你仍然亲自操作主角。

当前主要适配和验证目标为《Mount & Blade II: Bannerlord》`v1.5.2`。

## 主要功能

- 为家族部队、商队和驻军设置兵种模板、兵种比例、招募规则和行为限制。
- 为部队添加招募、巡逻、前往、护送、驻留、围攻和防守等命令队列。
- 自动管理招募、升级、买马、驻军和家族部队创建等大地图行为。
- 部署结束后，自动将玩家编队交给游戏原生战术 AI。
- 玩家始终保留主角控制权，可在战斗中随时收回或交还编队指挥权。
- 提供完整简体中文文本。
- 不修改单位生命、伤害、武器精度、移动速度或其他战斗数值。

## 安装

需要先安装并启用：

- [Bannerlord.Harmony](https://github.com/BUTR/Bannerlord.Harmony)
- [Bannerlord.UIExtenderEx](https://github.com/BUTR/Bannerlord.UIExtenderEx)

使用打包版本时：

1. 解压文件，将 `Modules/Bannerlord.PartyAI` 复制到游戏目录下的 `Modules` 文件夹。
2. 在启动器中依次启用 `Bannerlord.Harmony`、`Bannerlord.UIExtenderEx` 和 `Bannerlord.PartyAI`。
3. 启动单人游戏并载入存档。

不要同时安装同一模块 ID 的其他 Party AI Controls 版本。

## 使用方式

### 控制面板

载入战役存档后按 `Ctrl+P` 打开控制面板。这里可以：

- 设置默认的家族部队、商队和驻军规则。
- 编辑单独部队的兵种模板、构成比例、最高兵阶、自动招募比例和买马预算。
- 设置是否允许加入军团、围城、劫掠、招募、捐赠驻军或俘虏领主。
- 添加和调整部队命令队列。
- 修改所有相关快捷键。

如果快捷键设置错误导致面板无法打开，可在游戏控制台输入：

```text
partyai.open
```

### 大地图直接指挥

- 按 `Alt+X` 选择附近可直接指挥的部队。
- 按住 `Alt`，在地图上向已选择部队下达移动或目标命令。
- 默认快捷键可在 `Ctrl+P` 的模组选项中修改。

### 战场 AI 指挥官

- “自动战场指挥官”默认开启。
- 完成部署并正式开战后，AI 自动接管我方所有可指挥编队。
- 你仍然控制主角的移动、骑乘和战斗。
- 按 `Ctrl+M` 收回编队指挥权；再次按下可重新交给 AI。
- 快捷键和自动接管开关可在 `Ctrl+P` 的模组选项中修改。

“实验性强化战场 AI”默认关闭。启用后会在野战中尝试自定义阵型推进、侧击和射程控制，但效果会随地图与兵种变化。未启用时使用游戏原生战术 AI；检测到 Realistic Battle AI 时也会优先让外部战斗 AI 接管。

## 兼容性说明

- 当前分支针对游戏 `v1.5.2` 编译和验证。
- `supported-game-versions.txt` 中列出了项目可构建的其他版本，但本分支未对它们进行同等强度的实战验证。
- 战场接管只用于单人任务，不会在多人会话中加载。
- 此分支沿用原模块 ID `Bannerlord.PartyAI`，可覆盖更新原版，但不要并列安装两个版本。

## 上游项目与致谢

本仓库基于 [adwitkow/Bannerlord.PartyAI](https://github.com/adwitkow/Bannerlord.PartyAI) 开发。原项目作者为 Adam Witkowski；大地图 Party AI 的核心架构、行为系统、设置界面和绝大部分基础代码均来自上游项目。

感谢 Adam Witkowski 及所有上游贡献者长期维护和开放源代码。没有上游项目，本中文增强分支无法实现。

原模组页面：[Nexus Mods - Party AI Controls](https://www.nexusmods.com/mountandblade2bannerlord/mods/6218)

本项目继续遵循 [MIT License](LICENSE)。
