<div align="center">

[![TShockPlugin](https://socialify.git.ci/UnrealMultiple/TShockPlugin/image?description=1&descriptionEditable=A%20TShock%20Chinese%20Plugin%20Collection%20Repository&forks=1&issues=1&language=1&logo=https%3A%2F%2Fgithub.com%2FUnrealMultiple%2FTShockPlugin%2Fblob%2Fmaster%2Ficon.png%3Fraw%3Dtrue&name=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Auto)](https://github.com/UnrealMultiple/TShockPlugin)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/UnrealMultiple/TShockPlugin/.github%2Fworkflows%2Fbuild.yml)](https://github.com/UnrealMultiple/TShockPlugin/actions)
[![GitHub contributors](https://img.shields.io/github/contributors/UnrealMultiple/TShockPlugin?style=flat)](https://github.com/UnrealMultiple/TShockPlugin/graphs/contributors)
[![NET6](https://img.shields.io/badge/Core-%20.NET_6-blue)](https://dotnet.microsoft.com/zh-cn/)
[![QQ](https://img.shields.io/badge/QQ-EB1923?logo=tencent-qq&logoColor=white)](https://qm.qq.com/cgi-bin/qm/qr?k=54tOesIU5g13yVBNFIuMBQ6AzjgE6f0m&jump_from=webapi&authKey=6jzafzJEqQGzq7b2mAHBw+Ws5uOdl83iIu7CvFmrfm/Xxbo2kNHKSNXJvDGYxhSW)
[![TShock](https://img.shields.io/badge/TShock5.2.0-2B579A.svg?&logo=TShock&logoColor=white)](https://github.com/Pryaxis/TShock)

**&gt; 简体中文 &lt;** | [English](README.en-US.md) | [Spanish/Español](README.es-ES.md)

</div>

## 前言
- 这是一个致力于收集整合`TShock`插件的仓库。
- 我们将持续收集优质的`TShock`插件，进行及时的更新。并跟进`TShock`的最新版本。
- 如果你也想加入我们，请按照仓库WIKI中的`开发者注意事项`的规定对本仓库`提交PR`。
- 如果你想要参与翻译工作，欢迎访问我们的[Crowdin](https://zh.crowdin.com/project/tshock-chinese-plugin) 链接

## 使用注意事项
- `安装插件前`请查看对应插件的文档： [TShock中文插件库文档](http://docs.terraria.ink/zh/)
- 注意有些插件可能需要`安装依赖`，请查看下方列表安装依赖插件。
- 每个插件都有一个使用说明，在`下方列表`点击链接查看具体说明事项。

> [!IMPORTANT]
> 许多插件依赖于`LazyAPI`,而`LazyAPI`依赖于`linq2db`。使用本仓库插件前，推荐先安装`LazyAPI.dll`和`linq2db.dll`  
> 推荐使用`AutoPluginManager`安装本插件库插件，会自动补齐相应的依赖

## 下载 (推荐使用APM安装插件)

- APM镜像(国内推荐): [Plugins.zip](http://api.terraria.ink:11434/plugin/get_all_plugins)
- Github发布: [Plugins.zip](https://github.com/UnrealMultiple/TShockPlugin/releases/download/V1.0.0.0/Plugins.zip)

#### AutoPluginManager插件 (推荐)  
     /apm l 列出所有插件  
     /apm i <插件名> 一键安装插件  
     /apm u [插件名] 检查并且更新插件  

## 反馈

- [反馈BUG](https://github.com/UnrealMultiple/TShockPlugin/issues/new?template=report_bug.yaml)  

- [新功能建议](https://github.com/UnrealMultiple/TShockPlugin/issues/new?template=feature_request.yaml)

- [空白Issue](https://github.com/UnrealMultiple/TShockPlugin/issues/new)

> [!IMPORTANT]
> 反馈BUG时务必按照要求填写模板，提供详细的详细，必要时请安装`pdb`文件


### 已收集插件

> 点击`链接`可查看`插件文档`

<Details>
<Summary>插件列表</Summary>

| 名称 | 插件说明 | 依赖 |
| :-: | :-: | :-: |
| [AdaptiveBossHP](.\src\AdaptiveBossHP/README.md) | Boss自适应血量：初始按百分比增幅，每失败一次临时减免，每成功一次永久递减 |  |
| [AdditionalPylons](.\src\AdditionalPylons/README.md) | 放置更多晶塔 | [LazyAPI](.\src\LazyAPI/README.md) |
| [AutoEat](.\src\AutoEat/README.md) | 自动进食/自动喝药：低血蓝自动吃食物，增益到期自动续（仅续你用过、刚过期的），可分别开关续食物/药水/增益 |  |
| [BottleRefund](.\src\BottleRefund/README.md) | 喝药自动返还玻璃瓶：延迟数秒全面结算背包流水后发放，合并堆叠/入箱/丢弃等批量操作不会误发，生长无需任何指令 |  |
| [DailyMerchant](.\src\DailyMerchant/README.md) | 旅商与骷髅商人每天到访，各自可单独开关 |  |
| [ExplosionGuard](.\src\ExplosionGuard/README.md) | 爆炸守卫：非玩家炮弹/爆炸保留伤害但不破坏方块，白名单/黑名单可控，绑定失败自动降级 |  |
| [FullHealthRespawn](.\src\FullHealthRespawn/README.md) | 复活满血 |  |
| [HerbBloom](.\src\HerbBloom/README.md) | 全草药常开：成熟即开花、无视昼夜/天气/月相条件，开花后永不回退；生长速度默认保持原版（可配置加速） |  |
| [LazyAPI](.\src\LazyAPI/README.md) | 插件基础库 | [linq2db]() |
| [MonsterTuner](.\src\MonsterTuner/README.md) | 怪物数值调整：按 BOSS/精英/小怪分组倍率调整血量/伤害/防御/金币，/mt 查看、/mtreload 热重载 |  |
| [NoNaturalInvasion](.\src\NoNaturalInvasion/README.md) | 阻止自然入侵：只拦凌晨自然刷的哥布林/海盗入侵，手动召唤（旗帜/海盗地图）与 /invade 不受影响 |  |
| [NurseHeal](.\src\NurseHeal/README.md) | 护士治疗 |  |
| [Starfall](.\src\Starfall/README.md) | 服务端控制坠落之星生成速率、掉落与天气事件倍率 |  |
| [TerraNews](.\src\TerraNews/README.md) | 每日渔夫任务、月相、沙尘暴与旅商播报 |  |

</Details>


## 友情链接

- [TShock 插件开发文档](https://github.com/ACaiCat/TShockPluginDocument)
- [Tshock 相关内容大全导航](https://github.com/UnrealMultiple/Tshock-nav)

