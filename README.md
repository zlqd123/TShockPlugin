<div align="center">

[![TShockPlugin](https://socialify.git.ci/UnrealMultiple/TShockPlugin/image?description=1&descriptionEditable=A%20TShock%20Chinese%20Plugin%20Collection%20Repository&forks=1&issues=1&language=1&logo=https%3A%2F%2Fgithub.com%2FUnrealMultiple%2FTShockPlugin%2Fblob%2Fmaster%2Ficon.png%3Fraw%3Dtrue&name=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Auto)](https://github.com/UnrealMultiple/TShockPlugin)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/UnrealMultiple/TShockPlugin/.github%2Fworkflows%2Fbuild.yml)](https://github.com/UnrealMultiple/TShockPlugin/actions)
[![GitHub contributors](https://img.shields.io/github/contributors/UnrealMultiple/TShockPlugin?style=flat)](https://github.com/UnrealMultiple/TShockPlugin/graphs/contributors)
[![.NET 6](https://img.shields.io/badge/Core-%20.NET_6-blue)](https://dotnet.microsoft.com/zh-cn/)
[![TShock](https://img.shields.io/badge/TShock5.2.0-2B579A.svg?&logo=TShock&logoColor=white)](https://github.com/Pryaxis/TShock)

**&gt; 简体中文 &lt;** | [English](README.en-US.md) | [Spanish/Español](README.es-ES.md)

</div>

# Addon 自研插件仓库

本仓库为 [UnrealMultiple/TShockPlugin](https://github.com/UnrealMultiple/TShockPlugin) 的 **Addon 分支**（默认分支 `addon-dev`），只收录作者「不是现在」自研的 TShock 插件、它们所依赖的基础库（`LazyAPI` / `SourceGen` / `Shared`），以及保留的上游原版插件 [AdditionalPylons](./src/AdditionalPylons/README.md)。上游全量插件列表请访问 [原仓库 master 分支](https://github.com/UnrealMultiple/TShockPlugin/tree/master)。

## 前言

- 本分支仅维护自研插件，聚焦功能开发、修复与热重载支持，不与上游收集列表同步。
- 插件使用 C# / .NET 6 编写，面向 TShock 5.2.0，构建产物为 `ServerPlugins` 目录下的 `.dll`。
- 插件说明、配置项与命令请点击下表链接查看对应 `README`。

> [!IMPORTANT]
> 多数插件依赖 `LazyAPI`，而 `LazyAPI` 依赖 `linq2db`。安装本分支插件时请一并安装 `LazyAPI.dll` 与 `linq2db.dll`；使用 `AutoPluginManager`（APM）安装可自动补齐依赖。

## 使用注意事项

- 安装插件前请先阅读对应插件文档，确认所需依赖与配置项。
- 所有插件均支持通过 TShock 的插件管理器热重载（标记「热重载」的插件），重载命令以各插件 README 为准。
- 插件配置文件（JSON）首次启动后生成于 `tshock/PluginConfigs` 或对应插件目录，可按需修改后重载生效。

## 下载与安装

- APM 镜像（国内推荐）：[Plugins.zip](http://api.terraria.ink:11434/plugin/get_all_plugins)
- GitHub Releases：[Plugins.zip](https://github.com/UnrealMultiple/TShockPlugin/releases/download/V1.0.0.0/Plugins.zip)

使用 AutoPluginManager（推荐）：

```text
/apm l            列出所有插件
/apm i <插件名>    一键安装插件（含依赖）
/apm u [插件名]    检查并更新插件
```

也可手动下载 DLL 放入 `ServerPlugins` 目录，并自行补齐依赖。

## 自研插件列表

> 点击名称查看插件文档、配置项与命令。作者均为「不是现在」，除注明上游原版的 [AdditionalPylons](./src/AdditionalPylons/README.md) 外。

| 名称 | 插件说明 | 依赖 |
| :---: | :---: | :---: |
| [AdaptiveBossHP](./src/AdaptiveBossHP/README.md) | Boss 自适应血量：初始按百分比增幅，每失败一次临时减免，每成功一次永久递减 | — |
| [AutoEat](./src/AutoEat/README.md) | 自动进食 / 喝药：低血蓝自动吃食物，增益到期自动续（仅续你用过、刚过期的），可分别开关续食物 / 药水 / 增益 | — |
| [BottleRefund](./src/BottleRefund/README.md) | 喝药自动返还玻璃瓶：延迟数秒全面结算背包流水后发放，批量堆叠 / 入箱 / 丢弃不误发 | — |
| [DailyMerchant](./src/DailyMerchant/README.md) | 旅商与骷髅商人每天到访：原版流动旅商每天仅约 22% 机会出现，这里改为每天必到；骷髅商人白天在出生点附近有人时就来一只 | — |
| [ExplosionGuard](./src/ExplosionGuard/README.md) | 爆炸守卫：非玩家炮弹 / 爆炸保留伤害但不破坏方块，白名单 / 黑名单可控，绑定失败自动降级 | — |
| [FullHealthRespawn](./src/FullHealthRespawn/README.md) | 复活满血 | — |
| [HerbBloom](./src/HerbBloom/README.md) | 全草药无视条件常开（参考霜花逻辑），开花后不回退，生长速度可配置 | — |
| [MonsterTuner](./src/MonsterTuner/README.md) | 怪物数值调整：按 BOSS / 精英 / 小怪分组倍率调整血量 / 伤害 / 防御 / 金币，`/mt` 查看、`/mtreload` 热重载 | — |
| [NoNaturalInvasion](./src/NoNaturalInvasion/README.md) | 阻止自然入侵：只拦凌晨自然刷的哥布林 / 海盗入侵，手动召唤（旗帜 / 海盗地图）与 `/invade` 不受影响 | — |
| [NurseHeal](./src/NurseHeal/README.md) | 与护士对话按原版定价公式扣费，回满血并移除可移除的减益 | — |
| [Starfall](./src/Starfall/README.md) | 服务端控制坠落之星生成速率、掉落与天气事件倍率 | — |
| [TerraNews](./src/TerraNews/README.md) | 泰拉新闻：每天 04:30 播报月相、渔夫任务鱼与月相特售货架，大风 / 日食 / 血月 / 沙尘暴 / 暴风雪特别报道，满月 / 血月 / 雨天 / 午后附带草药采收提醒 | — |

## 基础库与构建基建

这些目录不是独立插件，而是上述插件的公共依赖或构建所用工程，通常不需要单独安装（除 `LazyAPI` 需随插件部署）：

| 目录 | 说明 |
| :---: | :---: |
| [LazyAPI](./src/LazyAPI/README.md) | 插件基础库，提供配置、命令、本地化等通用能力；依赖 `linq2db` |
| [SourceGen](./src/SourceGen) | 源代码生成器工程，编译期辅助生成代码 |
| [Shared](./src/Shared) | 共享工具类（如本地化辅助） |
| [AdditionalPylons](./src/AdditionalPylons/README.md) | 上游原版插件：自定义各类晶塔数量上限，可配置跳过城镇 NPC 与群落检查；依赖 `LazyAPI` |

本地构建：使用 .NET 6 SDK 还原并编译 `Plugin.slnx`，将生成的 DLL 复制到服务器的 `ServerPlugins` 目录即可。CI 构建状态见顶部徽章。

## 反馈

- [反馈 BUG](https://github.com/zlqd123/TShockPlugin/issues)
- [新功能建议](https://github.com/zlqd123/TShockPlugin/issues)
- 反馈时请写明插件名称、TShock / .NET 版本、复现步骤与相关日志；必要时附上 `pdb` 文件以便定位。

## 友情链接

- [TShock 插件开发文档](https://github.com/ACaiCat/TShockPluginDocument)
- [TShock 相关内容大全导航](https://github.com/UnrealMultiple/Tshock-nav)
- [原仓库（上游全量插件）](https://github.com/UnrealMultiple/TShockPlugin)
- [TShock 中文插件库文档](http://docs.terraria.ink/zh/)
