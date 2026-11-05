using System.Reflection;
using Terraria;
using TShockAPI;

namespace NurseHeal;

/// <summary>
/// 复刻「原版护士」的治疗定价逻辑。
///
/// 原版公式（Terraria 1.4.x，参见 https://terraria.wiki.gg/wiki/Nurse）：
///   费用(铜币) = (缺失生命 × 1 + 减益数 × 100)
///              × 护士当前快乐度(0.75 ~ 1.5)
///              × 世界进度倍率(按已击败的 Boss 取最高适用值)
///              × (专家 / 大师模式 2)
///
/// 价格系数固定为 1.0（见 <see cref="Consts.PriceMultiplier"/>），完全按原版公式结算。
///
/// 其中「世界进度倍率」取自在 Main 上的 downed* 标志，按官方表取最高一档：
///   Eye of Cthulhu=1.5, Eater/Brain=2, Skeletron=3, Queen Bee=3.5,
///   Wall of Flesh(困难模式)=4, 机械三王任一=5, Plantera=6, Golem=7,
///   Duke Fishron=7.5, Lunatic Cultist=8, Moon Lord=10。
///
/// 读取 Main / NPC 的内部字段全部走反射 + 容错：任一字段在当前版本不存在时
/// 回退到中性值（1.0 / false），绝不抛异常，且公式结构始终与原版一致。
/// </summary>
public static class VanillaPricing
{
    // 世界进度倍率表：Boss 击败标志字段名 -> 倍率（官方原版数值）。取所有为 true 者中的最大值。
    private static readonly (string Field, double Mult)[] ProgressTiers =
    {
        ("downedBoss1", 1.5),        // 克苏鲁之眼
        ("downedBoss2", 2.0),        // 吞噬者 / 克苏鲁之脑
        ("downedBoss3", 3.0),        // 骷髅王
        ("downedQueenBee", 3.5),     // 蜂后
        ("hardMode", 4.0),           // 血肉墙 -> 困难模式
        ("downedMechBoss1", 5.0),    // 双子魔眼
        ("downedMechBoss2", 5.0),    // 毁灭者
        ("downedMechBoss3", 5.0),    // 机械骷髅王
        ("downedPlantBoss", 6.0),    // 世纪之花
        ("downedGolem", 7.0),        // 石巨人
        ("downedFishron", 7.5),      // 猪龙鱼公爵
        ("downedAncientCultist", 8.0), // 拜月教邪教徒
        ("downedMoonlord", 10.0),    // 月球领主
    };

    private static readonly object CacheLock = new();
    private static FieldInfo? _mainDebuff;       // Main.debuff : bool[]
    private static FieldInfo? _mainExpert;       // Main.expertMode : bool
    private static FieldInfo? _mainMaster;       // Main.masterMode : bool
    private static FieldInfo? _npcHappy;         // NPC.happy : bool
    private static FieldInfo? _npcUnhappy;       // NPC.unhappy : bool (部分版本)
    private static bool _cacheReady;

    private static void EnsureCache()
    {
        if (_cacheReady)
        {
            return;
        }

        lock (CacheLock)
        {
            if (_cacheReady)
            {
                return;
            }

            var main = typeof(Main);
            var npc = typeof(NPC);
            _mainDebuff = main.GetField("debuff", BindingFlags.Public | BindingFlags.Static);
            _mainExpert = main.GetField("expertMode", BindingFlags.Public | BindingFlags.Static);
            _mainMaster = main.GetField("masterMode", BindingFlags.Public | BindingFlags.Static);
            _npcHappy = npc.GetField("happy", BindingFlags.Public | BindingFlags.Instance);
            _npcUnhappy = npc.GetField("unhappy", BindingFlags.Public | BindingFlags.Instance);
            _cacheReady = true;
        }
    }

    /// <summary>
    /// 计算对指定玩家（与指定护士 NPC 对话时）的一次真实治疗费用，单位：铜币。
    /// </summary>
    public static long ComputePrice(TSPlayer player, NPC nurse)
    {
        EnsureCache();

        var p = player.TPlayer;
        var maxLife = p.statLifeMax2;
        var current = p.statLife;
        var missing = Math.Max(0L, maxLife - current);

        // 1) 缺失生命 × 1 铜 + 减益数 × 100 铜（1 银）
        long baseCost = missing + (long) CountDebuffs(p) * 100L;

        // 2) 护士当前快乐度因子（0.75 ~ 1.5）
        double happiness = ReadHappiness(nurse);

        // 3) 世界进度倍率（取最高适用档）
        double progress = ReadWorldProgress();

        // 4) 专家 / 大师模式翻倍
        double difficulty = (ReadBool(_mainExpert) || ReadBool(_mainMaster)) ? 2.0 : 1.0;

        // 5) 价格系数固定 1.0（完全按原版公式，不做缩放）
        double multiplier = Consts.PriceMultiplier;

        double raw = baseCost * happiness * progress * difficulty * multiplier;
        long price = (long) Math.Max(0, Math.Round(raw, MidpointRounding.AwayFromZero));
        return price;
    }

    /// <summary>统计玩家当前生效的减益数量（沿用原版 Main.debuff 判定）。</summary>
    private static int CountDebuffs(Player p)
    {
        bool[]? debuffArr = null;
        if (_mainDebuff is not null)
        {
            try
            {
                debuffArr = _mainDebuff.GetValue(null) as bool[];
            }
            catch
            {
                debuffArr = null;
            }
        }

        int count = 0;
        for (int i = 0; i < p.buffType.Length && i < p.buffTime.Length; i++)
        {
            int type = p.buffType[i];
            if (type > 0 && p.buffTime[i] > 0)
            {
                bool isDebuff = debuffArr is { Length: > 0 } && type < debuffArr.Length && debuffArr[type];
                if (isDebuff)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 读取护士快乐度价格因子：
    /// 最快乐 → 0.75（原版下限），最不快乐 → 1.5（原版上限），其余 → 1.0。
    /// 字段在版本间可能不存在，缺失时按中性 1.0 处理。
    /// </summary>
    private static double ReadHappiness(NPC nurse)
    {
        if (nurse is null)
        {
            return 1.0;
        }

        bool happy = _npcHappy is not null && ReadInstanceBool(_npcHappy, nurse);
        if (happy)
        {
            return 0.75;
        }

        bool unhappy = _npcUnhappy is not null && ReadInstanceBool(_npcUnhappy, nurse);
        if (unhappy)
        {
            return 1.5;
        }

        return 1.0;
    }

    private static double ReadWorldProgress()
    {
        double best = 1.0;
        foreach (var (field, mult) in ProgressTiers)
        {
            var fi = typeof(Main).GetField(field, BindingFlags.Public | BindingFlags.Static);
            if (fi is not null && fi.FieldType == typeof(bool))
            {
                try
                {
                    if ((bool) (fi.GetValue(null) ?? false) && mult > best)
                    {
                        best = mult;
                    }
                }
                catch
                {
                    // 忽略单个标志读取失败
                }
            }
        }

        return best;
    }

    private static bool ReadBool(FieldInfo? fi)
    {
        if (fi is null)
        {
            return false;
        }

        try
        {
            return (bool) (fi.GetValue(null) ?? false);
        }
        catch
        {
            return false;
        }
    }

    private static bool ReadInstanceBool(FieldInfo fi, object target)
    {
        try
        {
            return (bool) (fi.GetValue(target) ?? false);
        }
        catch
        {
            return false;
        }
    }
}
