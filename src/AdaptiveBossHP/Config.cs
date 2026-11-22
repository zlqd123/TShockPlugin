using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria.ID;
using TShockAPI;

namespace AdaptiveBossHP
{
    /// <summary>
    /// 单个 Boss 组的百分比配置（配置文件键名均为中文）。
    ///
    /// 成功衰减算法（"成功衰减算法"配置项）：
    ///   线性递减（默认）：每成功一次，血量比例固定减少"每次成功永久减少百分点"，
    ///     线性下降直到下限——每一场都有明确可见的变化，总衰减周期 = 加成空间 / 步长。
    ///   半衰期：首战倍率与下限之间的"加成空间"按半衰期收敛——
    ///     ratio = 下限 + (首战倍率 - 下限) × 0.5^(成功次数 / 成功衰减半衰期)
    ///   乘法衰减：每次成功整体乘 (1 - "每次成功乘法衰减百分比")%（旧算法）。
    ///
    /// 失败减免（各算法通用）：ratio × (1 - "每次失败临时减少百分比")^连败次数，成功后清零恢复。
    /// 最终结果不低于 下限（"单独血量下限百分比"，未设置则用全局值）。
    /// </summary>
    public class BossGroupConfig
    {
        public const string DecayModeLinearPerWin = "LinearPerWin";
        public const string DecayModeHalfLife = "HalfLife";
        public const string DecayModePercentPerWin = "PercentPerWin";

        /// <summary>显示名（用于广播与指令输出）。</summary>
        [JsonProperty("显示名")]
        public string DisplayName { get; set; } = "";

        /// <summary>该组包含的所有 NPC ID（本体、部位、随从都会被缩放并计入追踪）。</summary>
        [JsonProperty("组内NPC的ID")]
        public List<int> NPCIDs { get; set; } = new List<int>();

        /// <summary>判定"击败"的关键 NPC ID（如月总的只有核心；双子是两尊都要死）。</summary>
        [JsonProperty("判定击败的关键NPC的ID")]
        public List<int> KillTargets { get; set; } = new List<int>();

        /// <summary>X：初始血量增幅（百分比，50 = 原版的 150%）。</summary>
        [JsonProperty("初始血量增幅百分比")]
        public double BonusPercent { get; set; } = 50;

        /// <summary>Y：每次失败（逃逸/团灭）临时减少的血量百分比，成功后自动恢复。</summary>
        [JsonProperty("每次失败临时减少百分比")]
        public double FailReducePercent { get; set; } = 15;

        /// <summary>线性递减模式：每成功一次固定减少的血量百分点（默认 10，即 150%→140%）。</summary>
        [JsonProperty("每次成功永久减少百分点")]
        public double SuccessReducePerWin { get; set; } = 10;

        /// <summary>乘法衰减模式：每次成功永久减少的血量百分比（乘法累积）。</summary>
        [JsonProperty("每次成功乘法衰减百分比")]
        public double SuccessReducePercent { get; set; } = 10;

        /// <summary>成功衰减算法：线性递减（默认）/ 半衰期 / 乘法衰减。</summary>
        [JsonProperty("成功衰减算法")]
        public string SuccessDecayMode { get; set; } = "线性递减";

        /// <summary>半衰期模式：每成功这么多次，"超出下限的加成"减半（默认 10）。</summary>
        [JsonProperty("成功衰减半衰期")]
        public double SuccessHalfLife { get; set; } = 10;

        /// <summary>血量下限（原版的百分比）；null 则使用全局值。</summary>
        [JsonProperty("单独血量下限百分比")]
        public double? MinPercent { get; set; } = null;

        /// <summary>把配置里的算法名（含中文别名）归一化为内部常量，无法识别时回退线性递减。</summary>
        private string NormalizeMode()
        {
            string m = (SuccessDecayMode ?? "").Trim();
            if (m == "乘法衰减" || string.Equals(m, DecayModePercentPerWin, StringComparison.OrdinalIgnoreCase))
                return DecayModePercentPerWin;
            if (m == "半衰期" || string.Equals(m, DecayModeHalfLife, StringComparison.OrdinalIgnoreCase))
                return DecayModeHalfLife;
            return DecayModeLinearPerWin;
        }

        public double ComputeRatio(BossGroupState state, double globalMinPercent)
        {
            double floor = Math.Clamp(Math.Max(0.0, MinPercent ?? globalMinPercent) / 100.0, 0.01, 10.0);
            double peak = Math.Max(1.0 + BonusPercent / 100.0, floor); // 首战倍率（不低于下限）

            double successFactor;
            switch (NormalizeMode())
            {
                case DecayModePercentPerWin:
                    // 乘法衰减：每次成功整体乘 (1 - Z%)
                    successFactor = peak * Math.Pow(Math.Clamp(1.0 - SuccessReducePercent / 100.0, 0.0, 1.0),
                        Math.Max(0, state.SuccessCount));
                    break;

                case DecayModeHalfLife:
                    // 半衰期：加成空间（首战倍率 - 下限）按 0.5^(S/半衰期) 收敛
                    double halfLife = SuccessHalfLife > 0 ? SuccessHalfLife : 10;
                    successFactor = floor + (peak - floor) * Math.Pow(0.5, Math.Max(0, state.SuccessCount) / halfLife);
                    break;

                default:
                    // 线性递减（默认）：每成功一次固定减"每次成功永久减少百分点"，直到下限
                    double step = Math.Max(0.0, SuccessReducePerWin) / 100.0;
                    successFactor = Math.Max(floor, peak - Math.Max(0, state.SuccessCount) * step);
                    break;
            }

            double failFactor = Math.Pow(Math.Clamp(1.0 - FailReducePercent / 100.0, 0.0, 1.0),
                Math.Max(0, state.FailStreak));

            return Math.Max(successFactor * failFactor, floor);
        }
    }

    public class PluginConfig
    {
        /// <summary>总开关。</summary>
        [JsonProperty("是否启用")]
        public bool Enabled { get; set; } = true;

        /// <summary>是否向全体玩家广播成功/失败/现身消息。</summary>
        [JsonProperty("是否广播消息")]
        public bool BroadcastMessages { get; set; } = true;

        /// <summary>全局血量下限：最终血量不低于原版的这个百分比。</summary>
        [JsonProperty("全局血量下限百分比")]
        public double GlobalMinPercent { get; set; } = 50;

        /// <summary>Boss 组配置，键为组名（指令 reset 用）。</summary>
        [JsonProperty("Boss组")]
        public Dictionary<string, BossGroupConfig> Groups { get; set; } = BuildDefaultGroups();

        public static Dictionary<string, BossGroupConfig> BuildDefaultGroups()
        {
            // Boss 组默认值："组内NPC的ID"为该组全部成员（会被缩放），"判定击败的关键NPC的ID"为判定击败的关键部位
            var g = new Dictionary<string, BossGroupConfig>();
            void Add(string key, string name, short[] ids, short[] killTargets,
                double bonus = 50, double fail = 15, double success = 10)
            {
                g[key] = new BossGroupConfig
                {
                    DisplayName = name,
                    NPCIDs = ids.Select(i => (int)i).ToList(),
                    KillTargets = killTargets.Select(i => (int)i).ToList(),
                    BonusPercent = bonus,
                    FailReducePercent = fail,
                    SuccessReducePerWin = success,
                };
            }

            Add("KingSlime", "史莱姆王", new[] { NPCID.KingSlime }, new[] { NPCID.KingSlime });
            Add("EyeOfCthulhu", "克苏鲁之眼", new[] { NPCID.EyeofCthulhu }, new[] { NPCID.EyeofCthulhu });
            Add("EaterOfWorlds", "世界吞噬怪", new[] { NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail }, new[] { NPCID.EaterofWorldsHead });
            Add("BrainOfCthulhu", "克苏鲁之脑", new[] { NPCID.BrainofCthulhu, NPCID.Creeper }, new[] { NPCID.BrainofCthulhu });
            Add("QueenBee", "蜂王", new[] { NPCID.QueenBee }, new[] { NPCID.QueenBee });
            Add("Skeletron", "骷髅王", new[] { NPCID.SkeletronHead, NPCID.SkeletronHand }, new[] { NPCID.SkeletronHead });
            Add("Deerclops", "独眼巨鹿", new[] { NPCID.Deerclops }, new[] { NPCID.Deerclops });
            Add("WallOfFlesh", "血肉墙", new[] { NPCID.WallofFlesh, NPCID.WallofFleshEye }, new[] { NPCID.WallofFlesh });
            Add("QueenSlime", "史莱姆皇后", new[] { NPCID.QueenSlimeBoss }, new[] { NPCID.QueenSlimeBoss });
            Add("TheTwins", "双子魔眼", new[] { NPCID.Retinazer, NPCID.Spazmatism }, new[] { NPCID.Retinazer, NPCID.Spazmatism });
            Add("TheDestroyer", "毁灭者", new[] { NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail }, new[] { NPCID.TheDestroyer });
            Add("SkeletronPrime", "机械骷髅王", new[] { NPCID.SkeletronPrime, NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser }, new[] { NPCID.SkeletronPrime });
            Add("Plantera", "世纪之花", new[] { NPCID.Plantera, NPCID.PlanterasTentacle }, new[] { NPCID.Plantera });
            Add("Golem", "石巨人", new[] { NPCID.Golem, NPCID.GolemHead, NPCID.GolemFistLeft, NPCID.GolemFistRight, NPCID.GolemHeadFree }, new[] { NPCID.Golem });
            Add("DukeFishron", "猪龙鱼公爵", new[] { NPCID.DukeFishron }, new[] { NPCID.DukeFishron });
            Add("EmpressOfLight", "光之女皇", new[] { NPCID.HallowBoss }, new[] { NPCID.HallowBoss });
            Add("Betsy", "双足翼龙", new[] { NPCID.DD2Betsy }, new[] { NPCID.DD2Betsy });
            Add("FlyingDutchman", "飞翔海盗船", new[] { NPCID.PirateShip }, new[] { NPCID.PirateShip });
            Add("MartianSaucer", "火星飞碟", new[] { NPCID.MartianSaucer, NPCID.MartianSaucerTurret, NPCID.MartianSaucerCannon, NPCID.MartianSaucerCore }, new[] { NPCID.MartianSaucerCore });
            Add("LunaticCultist", "拜月教邪教徒", new[] { NPCID.CultistBoss }, new[] { NPCID.CultistBoss });
            Add("MoonLord", "月亮领主", new[] { NPCID.MoonLordHead, NPCID.MoonLordHand, NPCID.MoonLordCore, NPCID.MoonLordFreeEye }, new[] { NPCID.MoonLordCore });

            // 万圣夜 / 霜月事件 Boss
            Add("MourningWood", "哀木", new[] { NPCID.MourningWood }, new[] { NPCID.MourningWood });
            Add("Pumpking", "南瓜王", new[] { NPCID.Pumpking, NPCID.PumpkingBlade }, new[] { NPCID.Pumpking });
            Add("Everscream", "常绿尖叫怪", new[] { NPCID.Everscream }, new[] { NPCID.Everscream });
            Add("SantaNK1", "圣诞坦克", new[] { NPCID.SantaNK1 }, new[] { NPCID.SantaNK1 });
            Add("IceQueen", "冰雪女王", new[] { NPCID.IceQueen }, new[] { NPCID.IceQueen });

            return g;
        }

        public static PluginConfig Load(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    var cfg = JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(path));
                    if (cfg?.Groups != null && cfg.Groups.Count > 0)
                        return cfg;
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[AdaptiveBossHP] 配置读取失败，已重建默认配置：{ex.Message}");
                }
            }

            var def = new PluginConfig();
            def.Save(path);
            return def;
        }

        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
