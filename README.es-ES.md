<div align="center">
  
[![TShockPlugin](https://socialify.git.ci/UnrealMultiple/TShockPlugin/image?description=1&descriptionEditable=A%20TShock%20Chinese%20Plugin%20Collection%20Repository&forks=1&issues=1&language=1&logo=https%3A%2F%2Fgithub.com%2FUnrealMultiple%2FTShockPlugin%2Fblob%2Fmaster%2Ficon.png%3Fraw%3Dtrue&name=1&pattern=Circuit%20Board&pulls=1&stargazers=1&theme=Auto)](https://github.com/UnrealMultiple/TShockPlugin)  
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/UnrealMultiple/TShockPlugin/.github%2Fworkflows%2Fbuild.yml)](https://github.com/UnrealMultiple/TShockPlugin/actions)
[![GitHub contributors](https://img.shields.io/github/contributors/UnrealMultiple/TShockPlugin?style=flat)](https://github.com/UnrealMultiple/TShockPlugin/graphs/contributors)
[![NET6](https://img.shields.io/badge/Core-%20.NET_6-blue)](https://dotnet.microsoft.com/zh-cn/)
[![QQ](https://img.shields.io/badge/QQ-EB1923?logo=tencent-qq&logoColor=white)](https://qm.qq.com/cgi-bin/qm/qr?k=54tOesIU5g13yVBNFIuMBQ6AzjgE6f0m&jump_from=webapi&authKey=6jzafzJEqQGzq7b2mAHBw+Ws5uOdl83iIu7CvFmrfm/Xxbo2kNHKSNXJvDGYxhSW)
[![TShock](https://img.shields.io/badge/TShock5.2.0-2B579A.svg?&logo=TShock&logoColor=white)](https://github.com/Pryaxis/TShock)

[简体中文](README.md) | [English](README.en-US.md) | **&gt; Spanish/Español &lt;**

</div>

## Introduccion
- Este es un repositorio dedicado a recopilar e integrar complementos "TShock".
- Algunos de los complementos de la biblioteca se recopilan de Internet y se descompilan.
- Debido a la naturaleza especial del proyecto, puede causar infracción. Si hay alguna infracción, por favor contáctenos para resolverla.
- Continuaremos recopilando complementos de "TShock" de alta calidad, actualizándolos de manera oportuna y estando al día con la última versión de "TShock".
- Si desea unirse a nosotros, siga las "Notas del desarrollador" y envíe una "Solicitud de extracción" a este repositorio.


## Notas de usuario

- Tenga en cuenta que algunos complementos pueden requerir dependencias; consulte la lista a continuación para instalar las dependencias.
- Cada complemento tiene una nota de uso; haga clic en el hipervínculo en la lista a continuación para ver las instrucciones específicas.
- Se dice que a las personas a las que les gusta destacar repositorios, sus complementos no son fáciles de generar errores.

## Descargas

- Github Release: [Plugins.zip](https://github.com/UnrealMultiple/TShockPlugin/releases/download/V1.0.0.0/Plugins.zip)
- ApmApi Release: [Plugins.zip](http://api.terraria.ink:11434/plugin/get_all_plugins)

#### AutoPluginManager
    /apm l Lista todos los plugins
    /apm i <plugin name> Una vez - Instalar Plugin
    /apm u [plugin name] Ver si el plugin tiene actualizacion

## Notas de Desarrollador

> Estandarización de codigo

- Está prohibido el uso de variables chinas.
- No utilice funciones peligrosas.
- Evite el uso de subprocesos múltiples siempre que sea posible.
- No dejar puertas traseras en los complementos.
- Incluya un archivo README.md con cada proyecto de complemento.

## Comentarios

> Cualquier comentario, sugerencia o mejora en esta biblioteca de códigos se considerará contribución pública y podrá incluirse en esta biblioteca de códigos a menos que se indique explícitamente lo contrario.

- Si hay un error, proporcione la información relevante del sistema, la versión de TShock y el proceso de reproducción del error en la página "problema" de GitHub.

### Plugins Recolectados

> Haga clic en los hipervínculos para ver la descripción detallada del complemento

> [!NOTE]
> Es posible que la documentación del complemento en inglés no se actualice tan rápidamente como la documentación del complemento en chino.
> Intente consultar la documentación china siempre que sea posible.

<Details>
<Summary>Listado de Plugins</Summary>

| Nombre del plugin | Porcentaje de traducción | Descripcion del Plugin  | Dependencias |
| :-: | :-: | :-: | :-: |
| [AdaptiveBossHP](.\src\AdaptiveBossHP/README.md) | 0.0% | Boss自适应血量：初始按百分比增幅，每失败一次临时减免，每成功一次永久递减 |  |
| [AdditionalPylons](.\src\AdditionalPylons/README.md) | 80.0% | Colocar más pilones | [LazyAPI](.\src\LazyAPI/README.md) |
| [AutoEat](.\src\AutoEat/README.md) | 0.0% | 自动进食/自动喝药：低血蓝自动吃食物，增益到期自动续（仅续你用过、刚过期的），可分别开关续食物/药水/增益 |  |
| [BottleRefund](.\src\BottleRefund/README.md) | 0.0% | 喝药自动返还玻璃瓶：延迟数秒全面结算背包流水后发放，合并堆叠/入箱/丢弃等批量操作不会误发，生长无需任何指令 |  |
| [DailyMerchant](.\src\DailyMerchant/README.md) | -25.0% | El mercader viajero y el mercader esqueleto aparecen todos los días, cada uno con su propio interruptor |  |
| [ExplosionGuard](.\src\ExplosionGuard/README.md) | 0.0% | 爆炸守卫：非玩家炮弹/爆炸保留伤害但不破坏方块，白名单/黑名单可控，绑定失败自动降级 |  |
| [FullHealthRespawn](.\src\FullHealthRespawn/README.md) | -100.0% | Restaurar salud completa al reaparecer |  |
| [HerbBloom](.\src\HerbBloom/README.md) | 0.0% | 全草药常开：成熟即开花、无视昼夜/天气/月相条件，开花后永不回退；生长速度默认保持原版（可配置加速） |  |
| [LazyAPI](.\src\LazyAPI/README.md) | -11.1% | Biblioteca base para plugins | [linq2db]() |
| [MonsterTuner](.\src\MonsterTuner/README.md) | 0.0% | 怪物数值调整：按 BOSS/精英/小怪分组倍率调整血量/伤害/防御/金币，/mt 查看、/mtreload 热重载 |  |
| [NoNaturalInvasion](.\src\NoNaturalInvasion/README.md) | 0.0% | 阻止自然入侵：只拦凌晨自然刷的哥布林/海盗入侵，手动召唤（旗帜/海盗地图）与 /invade 不受影响 |  |
| [NurseHeal](.\src\NurseHeal/README.md) | 0.0% | 护士治疗 |  |
| [Starfall](.\src\Starfall/README.md) | 0.0% | 服务端控制坠落之星生成速率、掉落与天气事件倍率 |  |
| [TerraNews](.\src\TerraNews/README.md) | -12.5% | 每日渔夫任务、月相、沙尘暴与旅商播报 |  |

</Details>

## Traducción

- Si deseas participar en el trabajo de traducción, no dudes en visitar nuestro enlace de [Crowdin](https://es.crowdin.com/project/tshock-chinese-plugin)

## Links

- [TShock Plugin Development Documentation](https://github.com/ACaiCat/TShockPluginDocument)
- [Tshock Comprehensive Navigation](https://github.com/UnrealMultiple/Tshock-nav)
