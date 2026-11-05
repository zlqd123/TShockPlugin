# NurseHeal 护士治疗

- 作者: 词库助手
- 参考: `ProgressBag 进度礼包`（同一套插件骨架思路）
- 功能: **和护士对话 → 按「原版护士」的真实定价自动扣钱 → 自动回满血**，
  无需玩家再点原版「治疗」按钮、也不再由客户端决定扣多少钱。

## 行为（固定，无配置文件）

| 项目 | 取值 |
| --- | --- |
| 回血量 | 始终**回满**（与原版护士一致） |
| 价格 | **原版护士定价公式**，价格系数固定 `1.0` |
| 冷却 | **无**，每次对话即时结算 |
| 付费方式 | 扣除背包 / 存钱罐 / 保险箱里的**原版钱币** |

> 本插件**不读写任何配置文件**，部署后即按上表运行，不需要 `tshock/NurseHeal.json`。

## 实现原理

1. **捕获对话**：客户端发出 `PacketTypes.NpcTalk`（40）时，`GetDataHandlers.NpcTalk`
   被触发；`e.NPCTalkTarget` 是 `Main.npc` 下标，`Main.npc[...].netID == NPCID.Nurse` 即护士，
   `-1` 表示结束对话。
2. **接管原版交互**：把 `e.Handled = true` 吞掉对话包，并回发
   `NpcTalk(playerIndex, -1)` 关闭客户端的原版护士界面（与 TShock `HandleNpcTalk`
   对 disabled 玩家的处理一致），避免玩家再手动花钱治疗一次。
3. **定价（核心）**：复刻原版护士的真实定价逻辑，见 `VanillaPricing.cs`：
   ```
   费用(铜币) = (缺失生命 × 1 + 减益数 × 100)
              × 护士当前快乐度(0.75~1.5)
              × 世界进度倍率(按已击败 Boss 取最高档)
              × (专家/大师模式 2)
              × 价格系数(固定 1.0)
   ```
   - 缺失生命：`statLifeMax2 - statLife`
   - 减益数：遍历玩家 buff，按 `Main.debuff[]` 判定（与原版护士一致的口径）
   - 护士快乐度 / 世界进度 / 专家大师：运行时反射读取 `Main` / `NPC` 内部字段，
     任一字段不存在则回退中性值（1.0 / 不翻倍），**永不抛异常**
   - 世界进度倍率表：眼 1.5、脑/吞噬者 2、骷髅王 3、蜂后 3.5、血肉墙(困难) 4、
     机械三王 5、世纪之花 6、石巨人 7、猪龙鱼 7.5、邪教徒 8、月球领主 10
4. **扣钱**：扫玩家背包 0–57（含 50–53 钱币专用槽）、存钱罐、保险箱里的原版钱币
   （71 铜 / 72 银 / 73 金 / 74 铂金），收拢成铜币总额后扣费，找零按
   铂金→金→银→铜 重新分发到空槽并统一发 `PlayerSlot` 同步包
   （纯内存操作 + 发包，不走 GiveItem，避免 SSC/掉落分支竞态与吞钱）。
   - `NpcTalk` 钩子早于 TShock 的 Bouncer 检查，吞包前补做
     `IsBeingDisabled()` / `IsBouncerThrottled()` 校验，不给未登录/被禁用/被节流的玩家治疗。
5. **回血**：先同步服务端 `statLife`，再调用官方 `TSPlayer.Heal(heal)`，
   它广播 `PacketTypes.PlayerHealOther`（66），由客户端加血并播放回血特效，
   不会与 `PlayerHp` 包重复加血。

## 编译

### 源码仓库内（基于 NuGet 的 TShock 6.1.0）

插件源码不依赖 LazyAPI（独立插件），只引用 `../../template.targets`：

```
dotnet build src/NurseHeal/NurseHeal.csproj -c Release
```

产物输出到 `out/Release/NurseHeal.dll`。

### 部署到具体服务器（推荐）

服务器上的 TShock 版本可能与仓库不一致（例如 5099 那台是 **6.2.1.0**）。
为避免跨版本 `MissingMethodException`，用服务器里真实的程序集重编译：

1. 从服务器拷出 `ServerPlugins/TShockAPI.dll`、`bin/TerrariaServer.dll`、
   `bin/OTAPI.dll`、`bin/ModFramework.dll` 到 `_deploy_ref/`；
2. `dotnet build _deploy/NurseHeal.Deploy.csproj -c Release`
3. 产物 `_deploy/bin/Release/NurseHeal.dll` 与运行时版本完全一致。

部署时只需把这一个 `NurseHeal.dll` 放进服务器 `ServerPlugins/`（**不需要 LazyAPI.dll 等任何依赖**），
重启服务器即生效。
