using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace MonsterTuner
{
    /// <summary>
    /// MonsterTuner —— 怪物数值分组调整（血量/伤害/防御/金币倍率）。
    ///
    /// 分组（依据 1.4.5.8 源码提取的 ID 表）：
    /// - BOSS 组：主线 BOSS（原版 boss 旗）+ 全部分段/部件（含世界吞噬怪头/身/尾）；
    /// - 精英组：事件小 BOSS（荷兰飞盗船/海盗船长/蛾怪/恐惧鹦鹉螺/哥布林术士/霜月南瓜月三王等）+ 精英怪（宝箱怪系/困难地牢精英）；
    /// - 小怪组：其余所有怪物（默认兜底）。
    ///
    /// 机制：
    /// - NpcSpawn / NpcTransform 时按所在组的倍率一次性改写，之后不再干预；
    ///   （SetDefaults 与原版多人缩放已完成，此处是最终值；变身怪重走 SetDefaults，故挂 NpcTransform 补一次）
    /// - 血量：lifeMax/life 同步改写；伤害：damage 改写（最低钳 1，不会出现 0 伤怪）；
    /// - 防御：defDefense 与 defense 同步改写（原版减防 debuff 会以 defDefense 为基准重算，两者一致才不会被还原）；
    /// - 金币：value 缩放（死亡掉钱按原版人数分摊逻辑正常工作）；
    /// - 倍率钳制：0.1~10；
    /// - 注：原版客户端无法从服务端改变怪物外观/体型——NPC 同步包（packet 23）没有 scale/宽高字段，
    ///   客户端按 netID 本地跑 SetDefaults，任何服务端体型改写都不可见，故不提供该维度。
    /// - 城镇 NPC（townNPC）不调整；
    /// - /mtreload 或 /reload 热重载；/mt 查看当前分组与倍率。
    /// </summary>
    [ApiVersion(2, 1)]
    public class MonsterTunerPlugin : TerrariaPlugin
    {
        public override string Name => "MonsterTuner";
        public override string Author => "不是现在";
        public override string Description => "按 BOSS/精英/小怪 三组独立调整怪物血量/伤害/防御/金币倍率，支持热重载。";
        public override Version Version => new Version(1, 2, 0);

        private static string ConfigPath => Path.Combine(TShock.SavePath, "怪物数值调整.json");

        // BOSS 组：主线 BOSS（boss 旗）+ 分段/部件。世界吞噬怪 13/14/15 按需求归入本组。
        private static readonly int[] BossIds =
        {
            4, 5,                      // 克苏鲁之眼 + 仆从
            13, 14, 15,                // 世界吞噬怪 头/身/尾（1.4.5 无 boss 旗，按需求视为 BOSS）
            35, 36,                    // 骷髅王 + 手
            50,                        // 史莱姆王
            113, 115, 116,             // 血肉墙 + 饿鬼两兄弟
            125, 126,                  // 双子魔眼
            127, 128, 129, 130, 131,   // 机械骷髅王 + 四臂
            134, 135, 136, 139,        // 毁灭者 + 身/尾 + 探测怪
            222,                       // 蜂王
            245, 246, 247, 248,        // 石巨人 身体/头/双拳
            262, 263, 264,             // 世纪之花 + 钩/触手
            266, 267,                  // 克苏鲁之脑 + 仆从
            370,                       // 猪龙鱼公爵
            392, 393, 394, 395,        // 火星飞碟 本体/炮塔/加农炮/核心
            396, 397, 398, 400,        // 月亮领主 头/手/核心/自由眼
            439,                       // 拜月教邪教徒
            636,                       // 光之女皇
            657,                       // 史莱姆女皇
            664,                       // 火炬神
            668,                       // 独眼巨鹿
        };

        // 精英组：事件小 BOSS + 精英怪
        private static readonly int[] EliteIds =
        {
            85,                        // 宝箱怪（地表）
            172,                       // 符文法师
            196,                       // 宁芙
            216,                       // 海盗船长
            283, 285, 287, 288,        // 死灵法师 / 恶魔祭师 / Bone Lee / 地牢幽灵
            290, 291, 292, 293,        // 圣骑士 / 骷髅狙击手 / 战术骷髅 / 骷髅突击队
            325, 327,                  // 哀木 / 南瓜王
            341,                       // 礼物宝箱怪
            344, 345, 346,             // 常绿怪 / 冰雪女王 / 圣诞坦克
            347,                       // 精灵直升机
            471, 472,                  // 哥布林术士 / 阴影焰幻灵
            473, 474, 475, 476, 477,   // 腐化/猩红/神圣/丛林 宝箱怪 + 蛾怪
            491,                       // 荷兰飞盗船
            551,                       // 双足飞龙 Betsy
            618,                       // 恐惧鹦鹉螺
        };

        private TunerConfig _config = new();
        private HashSet<int> _bossIds = new();
        private HashSet<int> _eliteIds = new();

        public MonsterTunerPlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            LoadConfig();

            ServerApi.Hooks.NpcSpawn.Register(this, OnNpcSpawn);
            ServerApi.Hooks.NpcTransform.Register(this, OnNpcTransform);
            Commands.ChatCommands.Add(new Command("monstertuner.reload", ReloadCommand, "mtreload"));
            Commands.ChatCommands.Add(new Command("mtinfo", InfoCommand, "mt"));
            GeneralHooks.ReloadEvent += OnReload;

            TShock.Log.ConsoleInfo(
                "[MonsterTuner] v{0} 已加载：BOSS 组 {1} 种（血×{2} 伤×{3} 防×{4} 金×{5}），精英组 {6} 种（血×{7} 伤×{8} 防×{9} 金×{10}），小怪默认（血×{11} 伤×{12} 防×{13} 金×{14}）",
                Version,
                _bossIds.Count, _config.Boss.HpMultiplier, _config.Boss.DamageMultiplier, _config.Boss.DefenseMultiplier, _config.Boss.CoinMultiplier,
                _eliteIds.Count, _config.Elite.HpMultiplier, _config.Elite.DamageMultiplier, _config.Elite.DefenseMultiplier, _config.Elite.CoinMultiplier,
                _config.Mob.HpMultiplier, _config.Mob.DamageMultiplier, _config.Mob.DefenseMultiplier, _config.Mob.CoinMultiplier);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NpcSpawn.Deregister(this, OnNpcSpawn);
                ServerApi.Hooks.NpcTransform.Deregister(this, OnNpcTransform);
                GeneralHooks.ReloadEvent -= OnReload;
                Commands.ChatCommands.RemoveAll(x => x.CommandDelegate is not null && (x.Name == "mtreload" || x.Name == "mt"));
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs e) => LoadConfig();

        // ------------------------------------------------------------------
        // 配置
        // ------------------------------------------------------------------

        internal sealed class GroupConfig
        {
            [JsonProperty("血量倍数")]
            public double HpMultiplier = 1.0;

            [JsonProperty("伤害倍数")]
            public double DamageMultiplier = 1.0;

            [JsonProperty("防御倍数")]
            public double DefenseMultiplier = 1.0;

            [JsonProperty("金币倍数")]
            public double CoinMultiplier = 1.0;

            [JsonProperty("怪物ID(小怪组无需填写)")]
            public List<int> NpcIds = new();

            /// <summary>钳制到允许范围，越界部分打日志。</summary>
            public void ClampAll(string name)
            {
                HpMultiplier = ClampRange(name, "血量", HpMultiplier, 0.1, 10.0);
                DamageMultiplier = ClampRange(name, "伤害", DamageMultiplier, 0.1, 10.0);
                DefenseMultiplier = ClampRange(name, "防御", DefenseMultiplier, 0.1, 10.0);
                CoinMultiplier = ClampRange(name, "金币", CoinMultiplier, 0.1, 10.0);
            }

            private static double ClampRange(string group, string label, double v, double lo, double hi)
            {
                var c = Math.Clamp(v, lo, hi);
                if (Math.Abs(v - c) > 1e-9)
                {
                    TShock.Log.ConsoleWarn("[MonsterTuner] {0}组{1}倍数 {2} 超出 {3}~{4}，已钳制为 {5}", group, label, v, lo, hi, c);
                }
                return c;
            }
        }

        internal sealed class TunerConfig
        {
            [JsonProperty("插件开关")]
            public bool Enabled = true;

            [JsonProperty("BOSS组(主线BOSS+分段部件,含世界吞噬怪)")]
            public GroupConfig Boss = new();

            [JsonProperty("精英组(事件小BOSS+精英怪)")]
            public GroupConfig Elite = new();

            [JsonProperty("小怪组(除以上两组外的全部怪物)")]
            public GroupConfig Mob = new();
        }

        private void LoadConfig()
        {
            var loaded = false;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    _config = JsonConvert.DeserializeObject<TunerConfig>(File.ReadAllText(ConfigPath)) ?? new TunerConfig();
                    loaded = true;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("[MonsterTuner] 配置读取失败，使用默认值：{0}", ex.Message);
                _config = new TunerConfig();
            }

            _config.Boss.ClampAll("BOSS");
            _config.Elite.ClampAll("精英");
            _config.Mob.ClampAll("小怪");

            _bossIds = new HashSet<int>(_config.Boss.NpcIds);
            _eliteIds = new HashSet<int>(_config.Elite.NpcIds);

            if (!loaded)
            {
                _config.Boss.NpcIds.AddRange(BossIds);
                _config.Elite.NpcIds.AddRange(EliteIds);
                SaveConfig();
                TShock.Log.ConsoleInfo("[MonsterTuner] 已生成默认配置：{0}", ConfigPath);
            }
        }

        private void SaveConfig()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }

        private (double Hp, double Dmg, double Def, double Coin) GetMultipliers(int npcType)
        {
            if (_bossIds.Contains(npcType))
                return (_config.Boss.HpMultiplier, _config.Boss.DamageMultiplier, _config.Boss.DefenseMultiplier, _config.Boss.CoinMultiplier);
            if (_eliteIds.Contains(npcType))
                return (_config.Elite.HpMultiplier, _config.Elite.DamageMultiplier, _config.Elite.DefenseMultiplier, _config.Elite.CoinMultiplier);
            return (_config.Mob.HpMultiplier, _config.Mob.DamageMultiplier, _config.Mob.DefenseMultiplier, _config.Mob.CoinMultiplier);
        }

        // ------------------------------------------------------------------
        // 应用：出生 / 变身时一次性改写
        // ------------------------------------------------------------------

        private void OnNpcSpawn(NpcSpawnEventArgs args) => Apply(args.NpcId);

        private void OnNpcTransform(NpcTransformationEventArgs args) => Apply(args.NpcId);

        private void Apply(int npcId)
        {
            if (!_config.Enabled)
                return;

            var npc = Main.npc[npcId];
            if (npc is not { active: true } || npc.townNPC)
                return;

            var (hpMult, dmgMult, defMult, coinMult) = GetMultipliers(npc.type);
            if (hpMult == 1.0 && dmgMult == 1.0 && defMult == 1.0 && coinMult == 1.0)
                return;

            if (hpMult != 1.0 && npc.lifeMax > 0)
            {
                npc.lifeMax = Math.Max(1, (int)Math.Round(npc.lifeMax * hpMult));
                npc.life = npc.lifeMax;
            }
            if (dmgMult != 1.0 && npc.damage > 0)
            {
                npc.damage = Math.Max(1, (int)Math.Round(npc.damage * dmgMult));
            }
            if (defMult != 1.0)
            {
                // defDefense 是原版减防 debuff（灵液/腐蚀等）重算的基准，两者必须一致改写
                npc.defDefense = Math.Max(0, (int)Math.Round(npc.defDefense * defMult));
                npc.defense = Math.Max(0, (int)Math.Round(npc.defense * defMult));
            }
            if (coinMult != 1.0)
            {
                npc.value = (float)Math.Max(0, npc.value * coinMult);
            }
        }

        // ------------------------------------------------------------------
        // 指令
        // ------------------------------------------------------------------

        private void ReloadCommand(CommandArgs args)
        {
            LoadConfig();
            args.Player.SendSuccessMessage(
                "[MonsterTuner] 已重载（血/伤/防/金）：BOSS ×{0}/×{1}/×{2}/×{3}，精英 ×{4}/×{5}/×{6}/×{7}，小怪 ×{8}/×{9}/×{10}/×{11}",
                _config.Boss.HpMultiplier, _config.Boss.DamageMultiplier, _config.Boss.DefenseMultiplier, _config.Boss.CoinMultiplier,
                _config.Elite.HpMultiplier, _config.Elite.DamageMultiplier, _config.Elite.DefenseMultiplier, _config.Elite.CoinMultiplier,
                _config.Mob.HpMultiplier, _config.Mob.DamageMultiplier, _config.Mob.DefenseMultiplier, _config.Mob.CoinMultiplier);
            TShock.Log.ConsoleInfo("[MonsterTuner] 配置已由 {0} 重载", args.Player.Name);
        }

        private void InfoCommand(CommandArgs args)
        {
            args.Player.SendInfoMessage("[MonsterTuner] 开关：{0}　倍率范围 0.1~10", _config.Enabled ? "开" : "关");
            args.Player.SendInfoMessage("BOSS 组（{0} 种）：血×{1} 伤×{2} 防×{3} 金×{4}", _bossIds.Count, _config.Boss.HpMultiplier, _config.Boss.DamageMultiplier, _config.Boss.DefenseMultiplier, _config.Boss.CoinMultiplier);
            args.Player.SendInfoMessage("精英组（{0} 种）：血×{1} 伤×{2} 防×{3} 金×{4}", _eliteIds.Count, _config.Elite.HpMultiplier, _config.Elite.DamageMultiplier, _config.Elite.DefenseMultiplier, _config.Elite.CoinMultiplier);
            args.Player.SendInfoMessage("小怪组（其余全部）：血×{0} 伤×{1} 防×{2} 金×{3}", _config.Mob.HpMultiplier, _config.Mob.DamageMultiplier, _config.Mob.DefenseMultiplier, _config.Mob.CoinMultiplier);
        }
    }
}
