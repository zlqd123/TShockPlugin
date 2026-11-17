# MonsterTuner 怪物数值调整

按 BOSS / 精英 / 小怪分组，用倍率统一调整怪物的血量、伤害、防御、金币掉落，
用于平衡难度；支持命令查看与热重载。

## 功能
- 分组倍率：BOSS / 精英 / 小怪（可自定义 NPC ID 列表）。
- 每组可调：血量、伤害、防御、金币（0.1–10）。
- 在怪物生成时按倍率缩放对应属性。
- `/mt` 查看，`/mtreload` 热重载。

## 命令
| 命令 | 权限 | 说明 |
| --- | --- | --- |
| `/mt` | `mtinfo` | 查看当前各组倍率 |
| `/mtreload` | `monstertuner.reload` | 热重载配置 |

## 配置 `tshock/怪物数值调整.json`
每组结构：
| 字段 | 说明 |
| --- | --- |
| `Enabled` | 该组是否生效 |
| `HpMultiplier` | 血量倍率（0.1–10） |
| `DamageMultiplier` | 伤害倍率（0.1–10） |
| `DefenseMultiplier` | 防御倍率（0.1–10） |
| `CoinMultiplier` | 金币倍率（0.1–10） |
| `NpcIds` | 该组包含的 NPC ID 列表 |

## 部署
TShock 6.1.0+，把 `MonsterTuner.dll` 放进 `ServerPlugins/`，重启或用插件管理器加载。
