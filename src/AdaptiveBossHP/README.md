# AdaptiveBossHP —— TShock Boss 自适应血量插件

Boss 的血量不再是固定值：**初始按百分比增幅（X），每次失败临时减免（Y），每成功一次永久递减（半衰期算法）**。
所有数值均为**百分比**配置，对任意难度（经典/专家/大师）与世界类型均按原版血量等比缩放。
Boss 的血量阶段（如世纪之花 50% 转阶段）不受影响——插件等比缩放 life 与 lifeMax，阶段照常在相同百分比触发。

## 公式（v1.2.0 起）

**成功衰减（永久，线性递减，默认）：**

```
成功率 = max(下限, 首战倍率 - SuccessReducePerWin% × 成功次数)
```

- **每成功一次，血量固定下降 `SuccessReducePerWin` 个百分点**（默认 2，即 150% → 148% → 146%…），直到下限。
- 每一场都有明确可见的变化；总衰减周期 = 加成空间 ÷ 步长（默认从 150% 到 50% 共 50 次）。

**失败减免（临时，连败累计，成功即清零恢复）：**

```
失败率 = (1 - FailReducePercent%)^当前连败次数
```

**最终：**

```
血量 = 原版 × 成功率 × 失败率        （不低于下限，默认 50%）
```

| 连败次数（Y=15%） | 血量比例 | | 累计成功次数（步长 2pp） | 血量比例 |
| :-: | :-: | :-: | :-: | :-: |
| 0（首战） | 150% | | 0（首战） | 150% |
| 1 | 127.5% | | 1 | 148% |
| 3 | 92.1% | | 10 | 130% |
| 5 | 66.6% | | 25 | 100% |
| **7** | **50%（下限）** | | 40 | 70% |
| 20 / 50 / 100 | 50%（兜底） | | **50** | **50%（下限）** |

- **Y 是临时的**：连败减免只在"上次成功之后"累计，一旦击败立即清零恢复。
- **成功衰减是线性的**：每次成功固定降，永远有可感知的变化，到下限为止。
- **其他算法可选**（`SuccessDecayMode`）：
  - `"HalfLife"`：加成空间每 `SuccessHalfLife` 次成功减半，前快后慢（v1.1 行为）。
  - `"PercentPerWin"`：每次成功整体乘 `(1 - SuccessReducePercent%)`（v1.0 行为）。

## 判定规则

| 事件 | 判定 | 时机 |
|---|---|---|
| **成功** | 本场战斗中该 Boss 的所有关键部位（`KillTargets`）全部被击杀 | 击杀瞬间记录，全组 NPC 消失后结算 |
| **失败** | 有本组 NPC 在**未被击杀**的情况下消失（逃逸/团灭/天亮逃跑） | GameUpdate 轮询（约每 1/6 秒） |

边界情况处理：

- **多段 Boss**：毁灭者/世界吞噬怪/血肉墙等所有部位按同一比例缩放；只有关键部位（毁灭者头部、月总核心等）死亡才算成功。
- **双子魔眼**：两尊都被击杀才算成功，只杀一只后团灭 = 失败。
- **世界吞噬怪分裂**：分裂出的新头也会被自动纳入追踪并等比缩放，需全部击杀。
- **血量分阶段**：所有阶段判定基于 `life/lifeMax` 比例，等比缩放不影响触发点。
- **无人在线保护**：Boss 消失时若服务器无人（停服、最后一名玩家退出），不计成败，静默清理。
- **同一 Boss 多次召唤**：同时存在的同组 Boss 共享一场会话，需全部击杀才算成功。

## 安装

1. 需要 **TShock 6.x（Terraria 1.4.5+）**，插件基于 `.NET 9 / TSAPI 2.1` 构建。
2. 将 `AdaptiveBossHP.dll` 复制到服务器的 `ServerPlugins/` 目录。
3. 启动服务器，自动生成默认配置。

> 从 v1.0/v1.1 升级：已生成的 `tshock/AdaptiveBossHP.json` 不会自动更新默认值——请把 `GlobalMinPercent` 改为 `50`，并把各组的 `SuccessDecayMode` 改为 `LinearPerWin`（或直接删除配置文件重新生成）。
> 旧版 TShock 5.2（Terraria 1.4.4，.NET 6）需将 `AdaptiveBossHP.csproj` 中 `TargetFramework` 改为 `net6.0` 后重新编译。

## 配置文件

首次运行生成 `tshock/AdaptiveBossHP.json`：

```jsonc
{
  "Enabled": true,                    // 总开关
  "BroadcastMessages": true,          // 广播现身/成功/失败消息
  "GlobalMinPercent": 50,             // 全局下限：最终血量不低于原版的 50%
  "Groups": {
    "EyeOfCthulhu": {
      "DisplayName": "克苏鲁之眼",
      "NPCIDs": [4],                  // 本组全部 NPC（含部位/随从），都会被缩放
      "KillTargets": [4],             // 死亡才算"击败"的关键部位
      "BonusPercent": 50,             // X：初始增幅（首战 150%）
      "FailReducePercent": 15,        // Y：每次失败的临时减免
      "SuccessReducePerWin": 2,       // LinearPerWin 模式：每成功一次减 2 个百分点
      "SuccessReducePercent": 10,     // 仅 PercentPerWin 模式使用
      "SuccessDecayMode": "LinearPerWin", // 成功衰减算法：LinearPerWin（默认）/ HalfLife / PercentPerWin
      "SuccessHalfLife": 10,          // 仅 HalfLife 模式使用
      "MinPercent": null              // 单组下限，null=用全局值
    }
    // … 其余默认组见下方列表
  }
}
```

新增/自定义组：在 `Groups` 里加一段即可，NPC ID 对照 [Terraria Wiki: NPC IDs](https://terraria.wiki.gg/wiki/NPC_IDs)。
月度四柱（太阳/星旋/星云/星尘之柱）默认未收录，需要可自行添加。

## 状态文件

`tshock/AdaptiveBossHP_State.json`（自动生成）：

```jsonc
{
  "Groups": {
    "EyeOfCthulhu": {
      "SuccessCount": 2,   // 累计成功 —— 驱动永久半衰
      "FailStreak": 1,     // 当前连败（成功清零）—— 驱动临时减免 Y
      "TotalFails": 3      // 历史失败总数（仅统计）
    }
  }
}
```

变更即写盘，重启不丢。想手动清零直接删除文件或用指令。

## 指令（权限：`adaptivebosshp.admin`）

| 指令 | 说明 |
|---|---|
| `/bossdiff status` | 查看各 Boss 成功次数/连败次数/下次血量（含原版血量换算） |
| `/bossdiff reset <组名\|all>` | 重置统计（组名可用 `status` 里的方括号名或中文显示名） |
| `/bossdiff reload` | 重载配置（也支持 TShock 的 `/reload`） |

别名：`/bdhp`

## 默认 Boss 组

史莱姆王、克苏鲁之眼、世界吞噬怪、克苏鲁之脑、蜂王、骷髅王、独眼巨鹿、血肉墙、史莱姆皇后、
双子魔眼、毁灭者、机械骷髅王、世纪之花、石巨人、猪龙鱼公爵、光之女皇、双足翼龙、飞翔海盗船、
火星飞碟、拜月教邪教徒、月亮领主、哀木、南瓜王、常绿尖叫怪、圣诞坦克、冰雪女王。

## 构建

```bash
cd AdaptiveBossHP
dotnet build -c Release
# 产物：bin/Release/AdaptiveBossHP.dll
```

依赖：.NET 9 SDK；NuGet 自动还原 `TShock 6.2.1`（含 TSAPI）。

## 已知限制

- 同组 Boss 的多个召唤物共享一场会话：同时召两只克眼需全杀才算成功。
- Boss 被管理指令（如 `/butcher`）杀死同样计为"成功"。
- 四柱默认不收录（它们没有"逃逸失败"的概念）。
