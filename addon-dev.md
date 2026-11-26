# Addon Dev 自研插件

本分支的完整说明已迁移至 [README.md](./README.md)，插件列表、依赖说明、安装方式与反馈入口均以 README 为准。

本分支仅保留自研插件（作者：不是现在）、其依赖（LazyAPI / SourceGen / Shared）与上游原版 [AdditionalPylons](./src/AdditionalPylons/README.md)。

| 名称 | 版本 | 插件说明 | 上游版本 | 依赖 |
| :---: | :---: | :---: | :---: | :---: |
| [AdaptiveBossHP](./src/AdaptiveBossHP/README.md) | 1.0.1.0 | Boss 自适应血量：初始按百分比增幅，每失败一次临时减免，每成功一次永久递减 | 未收录 | — |
| [AutoEat](./src/AutoEat/README.md) | 1.0.0.0 | 自动进食 / 喝药：低血蓝自动吃食物，增益到期自动续（仅续你用过、刚过期的），可分别开关续食物 / 药水 / 增益 | 未收录 | — |
| [BottleRefund](./src/BottleRefund/README.md) | 1.0.0 | 喝药自动返还玻璃瓶：延迟数秒全面结算背包流水后发放，批量堆叠 / 入箱 / 丢弃不误发 | 1.0.0（相同） | — |
| [DailyMerchant](./src/DailyMerchant/README.md) | 1.1 | 旅商与骷髅商人每天到访：原版流动旅商每天仅约 22% 机会出现，这里改为每天必到；骷髅商人白天在出生点附近有人时就来一只 | 1.1（相同） | — |
| [ExplosionGuard](./src/ExplosionGuard/README.md) | 1.0.0 | 爆炸守卫：非玩家炮弹 / 爆炸保留伤害但不破坏方块，白名单 / 黑名单可控，绑定失败自动降级 | 未收录 | — |
| [FullHealthRespawn](./src/FullHealthRespawn/README.md) | 1.2.1 | 复活满血 | 未收录 | — |
| [HerbBloom](./src/HerbBloom/README.md) | 1.0.0 | 全草药无视条件常开（参考霜花逻辑），开花后不回退，生长速度可配置 | 1.0.0（相同） | — |
| [MonsterTuner](./src/MonsterTuner/README.md) | 1.2.0 | 怪物数值调整：按 BOSS / 精英 / 小怪分组倍率调整血量 / 伤害 / 防御 / 金币，`/mt` 查看、`/mtreload` 热重载 | 未收录 | — |
| [NoNaturalInvasion](./src/NoNaturalInvasion/README.md) | 1.0.1 | 阻止自然入侵：只拦凌晨自然刷的哥布林 / 海盗入侵，手动召唤（旗帜 / 海盗地图）与 `/invade` 不受影响 | 未收录 | — |
| [NurseHeal](./src/NurseHeal/README.md) | 1.0.0.0 | 与护士对话按原版定价公式扣费，回满血并移除可移除的减益 | 1.0.0.0（相同） | — |
| [Starfall](./src/Starfall/README.md) | 2.0.3.0 | 服务端控制坠落之星生成速率、掉落与天气事件倍率 | 2.0.3.0（相同） | — |
| [TerraNews](./src/TerraNews/README.md) | 1.1.1 | 泰拉新闻：每天 04:30 播报月相、渔夫任务鱼与月相特售货架，大风 / 日食 / 血月 / 沙尘暴 / 暴风雪特别报道，满月 / 血月 / 雨天 / 午后附带草药采收提醒 | 1.1.1（相同） | — |
