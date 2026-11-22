using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace AdaptiveBossHP
{
    /// <summary>
    /// Boss 自适应血量插件。
    ///
    /// 规则（全部百分比配置）：
    ///   最终血量 = 原版血量 × (1 + BonusPercent%) × (1 - SuccessReducePercent%)^成功次数 × (1 - FailReducePercent%)^连败次数
    ///   - X = BonusPercent        初始增幅，永久存在
    ///   - Y = FailReducePercent   每失败一次（Boss 逃逸/团灭）临时减免，成功后自动恢复
    ///   - Z = SuccessReducePercent 每成功一次永久递减（乘法累积）
    ///   - 结果不低于 MinPercent%（原版的百分比下限）
    ///
    /// 判定：
    ///   成功 = 本次会话中所有 KillTargets 全部被击杀（NpcKilled）
    ///   失败 = 有本组 NPC 在未被击杀的情况下消失（GameUpdate 轮询 active 变 false）
    ///   服务器无人在线时的消失不计成败（停服/最后一名玩家退出的保护）
    /// </summary>
    [ApiVersion(2, 1)]
    public class AdaptiveBossHPPlugin : TerrariaPlugin
    {
        public override string Name => "AdaptiveBossHP";
        public override string Author => "zlqd123";
        public override string Description =>
            "Boss 自适应血量：初始按百分比增幅，失败临时减免，成功永久递减";
        public override Version Version => new Version(1, 2, 0);

        public static readonly string ConfigPath = Path.Combine("tshock", "AdaptiveBossHP.json");
        public static readonly string StatePath = Path.Combine("tshock", "AdaptiveBossHP_State.json");
        private const string Permission = "adaptivebosshp.admin";

        private PluginConfig _config = new PluginConfig();
        private PluginState _state = new PluginState();

        /// <summary>NPC 类型 ID -> 组名（由配置构建的快速反查表）。</summary>
        private readonly Dictionary<int, string> _typeToGroup = new Dictionary<int, string>();

        /// <summary>进行中的战斗会话（每组同时最多一场，多段 Boss 共享一个会话）。</summary>
        private readonly Dictionary<string, BossSession> _sessions =
            new Dictionary<string, BossSession>(StringComparer.OrdinalIgnoreCase);

        /// <summary>NPC 槽位（whoAmI）-> 组名。</summary>
        private readonly Dictionary<int, string> _npcToSession = new Dictionary<int, string>();

        private int _pollCounter;

        public AdaptiveBossHPPlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            _config = PluginConfig.Load(ConfigPath);
            _state = PluginState.Load(StatePath);
            RebuildTypeMap();

            ServerApi.Hooks.NpcSpawn.Register(this, OnNpcSpawn);
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            GeneralHooks.ReloadEvent += OnReload;

            Commands.ChatCommands.Add(new Command(Permission, OnBossDiffCommand, "bossdiff", "bdhp")
            {
                HelpText = "自适应Boss血量：/bossdiff status | /bossdiff reset <组名|all> | /bossdiff reload"
            });

            TShock.Log.ConsoleInfo($"[AdaptiveBossHP] 已加载，共 {_config.Groups.Count} 个Boss组（启用={_config.Enabled}）");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NpcSpawn.Deregister(this, OnNpcSpawn);
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                GeneralHooks.ReloadEvent -= OnReload;
                Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == OnBossDiffCommand);
                _state?.Save(StatePath);
            }
            base.Dispose(disposing);
        }

        #region 配置

        private void RebuildTypeMap()
        {
            _typeToGroup.Clear();
            foreach (var kv in _config.Groups)
            {
                foreach (int id in kv.Value.NPCIDs)
                    _typeToGroup[id] = kv.Key;
            }
        }

        private void OnReload(ReloadEventArgs args)
        {
            _config = PluginConfig.Load(ConfigPath);
            RebuildTypeMap();
            TShock.Log.ConsoleInfo($"[AdaptiveBossHP] /reload 完成，当前 {_config.Groups.Count} 个Boss组（启用={_config.Enabled}）");
            args.Player?.SendInfoMessage("[AdaptiveBossHP] 配置已重载");
        }

        #endregion

        #region 核心事件

        private void OnNpcSpawn(NpcSpawnEventArgs args)
        {
            if (!_config.Enabled)
                return;

            int index = args.NpcId;
            if (index < 0 || index >= Main.npc.Length)
                return;

            NPC? npc = Main.npc[index];
            if (npc == null || !npc.active)
                return;

            if (!_typeToGroup.TryGetValue(npc.type, out var groupKey))
                return;

            var group = _config.Groups[groupKey];
            var state = _state.GetOrCreate(groupKey);

            if (!_sessions.TryGetValue(groupKey, out var session))
            {
                session = new BossSession(groupKey);
                _sessions[groupKey] = session;
            }

            // NPC 槽位可能被复用：若该槽位之前被标记过击杀，先清除
            session.KilledIndices.Remove(index);

            session.AllIndices.Add(index);
            session.TrackedIndices.Add(index);
            _npcToSession[index] = groupKey;

            bool isKillTarget = group.KillTargets.Contains(npc.type);
            if (isKillTarget)
                session.SpawnedKillTargets.Add(index);

            // 应用百分比血量
            double ratio = group.ComputeRatio(state, _config.GlobalMinPercent);
            int newMax = Math.Max(1, (int)Math.Round(npc.lifeMax * ratio));
            npc.lifeMax = newMax;
            npc.life = newMax;

            // 同步客户端血条（NpcUpdate 包）
            TSPlayer.All.SendData(PacketTypes.NpcUpdate, "", index);

            // 每场战斗只播报一次（以首个关键部位生成为准；虫体分节等后续生成不重复播报）
            if (_config.BroadcastMessages && isKillTarget && !session.SpawnAnnounced)
            {
                session.SpawnAnnounced = true;
                TShock.Utils.Broadcast(
                    $"[自适应Boss] {group.DisplayName} 现身！本场血量为原版的 {ratio * 100:0.#}%" +
                    $"（已成功 {state.SuccessCount} 次 / 当前连败 {state.FailStreak} 次）",
                    Color.Yellow);
            }
        }

        private void OnNpcKilled(NpcKilledEventArgs args)
        {
            if (!_config.Enabled || args.npc == null)
                return;

            NPC npc = args.npc;
            if (!_npcToSession.TryGetValue(npc.whoAmI, out var groupKey))
                return;
            if (!_sessions.TryGetValue(groupKey, out var session))
                return;

            session.KilledIndices.Add(npc.whoAmI);
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (_sessions.Count == 0)
                return;

            // 每 10 tick（约 1/6 秒）轮询一次，判定逃逸
            if (++_pollCounter < 10)
                return;
            _pollCounter = 0;

            bool playersOnline = TShock.Players != null && TShock.Players.Any(p => p != null && p.Active);

            foreach (var kv in _sessions.ToList())
            {
                var session = kv.Value;
                bool anyEscaped = false;

                foreach (int index in session.TrackedIndices.ToList())
                {
                    NPC? npc = (index >= 0 && index < Main.npc.Length) ? Main.npc[index] : null;
                    if (npc == null || !npc.active)
                    {
                        session.TrackedIndices.Remove(index);
                        if (!session.KilledIndices.Contains(index))
                            anyEscaped = true;
                    }
                }

                if (session.TrackedIndices.Count == 0)
                    FinalizeSession(kv.Key, session, anyEscaped, playersOnline);
            }
        }

        private void FinalizeSession(string groupKey, BossSession session, bool anyEscaped, bool playersOnline)
        {
            _sessions.Remove(groupKey);
            foreach (int index in session.AllIndices)
                _npcToSession.Remove(index);

            if (!_config.Groups.TryGetValue(groupKey, out var group))
                return;
            var state = _state.GetOrCreate(groupKey);

            // 成功：本次会话生成的所有关键部位都被击杀（双子=两尊全死；月总=核心死；EoW=所有头死）
            bool success = session.SpawnedKillTargets.Count > 0
                        && session.SpawnedKillTargets.All(session.KilledIndices.Contains);

            if (success)
            {
                state.SuccessCount++;
                state.FailStreak = 0; // 临时减免恢复
                _state.Save(StatePath);
                double nextRatio = group.ComputeRatio(state, _config.GlobalMinPercent);
                TShock.Log.ConsoleInfo(
                    $"[AdaptiveBossHP] {group.DisplayName} 被击败：成功次数={state.SuccessCount}，下次血量 {nextRatio * 100:0.#}%，连败已清零");

                if (_config.BroadcastMessages)
                    TShock.Utils.Broadcast(
                        $"[自适应Boss] {group.DisplayName} 被击败！下次召唤血量为原版的 {nextRatio * 100:0.#}%" +
                        $"（累计成功 {state.SuccessCount} 次），失败减免已恢复",
                        Color.LimeGreen);
            }
            else if (anyEscaped && playersOnline)
            {
                state.FailStreak++;
                state.TotalFails++;
                _state.Save(StatePath);
                double nextRatio = group.ComputeRatio(state, _config.GlobalMinPercent);
                TShock.Log.ConsoleInfo(
                    $"[AdaptiveBossHP] {group.DisplayName} 未被击败：连败={state.FailStreak}，下次血量 {nextRatio * 100:0.#}%");

                if (_config.BroadcastMessages)
                    TShock.Utils.Broadcast(
                        $"[自适应Boss] {group.DisplayName} 未能被击败（逃逸/团灭），下次召唤血量为原版的 {nextRatio * 100:0.#}%" +
                        $"（当前连败 {state.FailStreak} 次）",
                        Color.Orange);
            }
            else
            {
                // 无人在线时的静默清理（停服或最后一名玩家退出），不计成败
                TShock.Log.ConsoleInfo($"[AdaptiveBossHP] {group.DisplayName} 会话结束（无人在线，不计成败）");
            }
        }

        #endregion

        #region 指令 /bossdiff

        private void OnBossDiffCommand(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                PrintHelp(args);
                return;
            }

            switch (args.Parameters[0].ToLowerInvariant())
            {
                case "status":
                    CmdStatus(args);
                    break;
                case "reset":
                    CmdReset(args);
                    break;
                case "reload":
                    _config = PluginConfig.Load(ConfigPath);
                    RebuildTypeMap();
                    args.Player.SendSuccessMessage("[AdaptiveBossHP] 配置已重载");
                    break;
                default:
                    PrintHelp(args);
                    break;
            }
        }

        private void PrintHelp(CommandArgs args)
        {
            args.Player.SendInfoMessage("/bossdiff status —— 查看各 Boss 的成功次数/连败次数与下次血量");
            args.Player.SendInfoMessage("/bossdiff reset <组名|all> —— 重置统计（成功与连败清零）");
            args.Player.SendInfoMessage("/bossdiff reload —— 重载配置文件");
        }

        private void CmdStatus(CommandArgs args)
        {
            if (!_config.Enabled)
                args.Player.SendWarningMessage("[AdaptiveBossHP] 当前处于禁用状态（Enabled=false）");

            args.Player.SendInfoMessage($"── 自适应Boss血量状态（下限：原版 {_config.GlobalMinPercent:0.#}%）──");
            foreach (var kv in _config.Groups)
            {
                var group = kv.Value;
                var state = _state.GetOrCreate(kv.Key);
                double ratio = group.ComputeRatio(state, _config.GlobalMinPercent);
                int vanilla = GetVanillaLifeMax(group.KillTargets.Count > 0 ? group.KillTargets[0] : group.NPCIDs[0]);

                args.Player.SendInfoMessage(
                    $"{group.DisplayName} [{kv.Key}]：成功 {state.SuccessCount} 次 / 连败 {state.FailStreak} 次（累计失败 {state.TotalFails}）" +
                    $"，下次血量 {ratio * 100:0.#}% ≈ {Math.Round(vanilla * ratio)} HP");
            }
        }

        private void CmdReset(CommandArgs args)
        {
            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("用法：/bossdiff reset <组名|all>");
                return;
            }

            string target = args.Parameters[1];
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                _state.ResetAll();
                _state.Save(StatePath);
                args.Player.SendSuccessMessage("[AdaptiveBossHP] 已重置全部 Boss 统计");
                return;
            }

            var match = _config.Groups.Keys
                .FirstOrDefault(k => k.Equals(target, StringComparison.OrdinalIgnoreCase))
                ?? _config.Groups.FirstOrDefault(g => g.Value.DisplayName == target).Key;

            if (match == null)
            {
                args.Player.SendErrorMessage($"未找到 Boss 组 \"{target}\"，请用 /bossdiff status 查看组名");
                return;
            }

            _state.Reset(match);
            _state.Save(StatePath);
            args.Player.SendSuccessMessage($"[AdaptiveBossHP] 已重置 {_config.Groups[match].DisplayName} 的统计");
        }

        private static int GetVanillaLifeMax(int npcId)
        {
            try
            {
                var npc = new NPC();
                npc.SetDefaults(npcId);
                return npc.lifeMax;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }

    /// <summary>一场战斗会话：从本组任一 NPC 生成开始，到本组全部 NPC 消失为止。</summary>
    internal class BossSession
    {
        public readonly string GroupKey;

        /// <summary>本会话出现过的所有 NPC 槽位（用于清理反查表）。</summary>
        public readonly HashSet<int> AllIndices = new HashSet<int>();

        /// <summary>当前仍存活的 NPC 槽位。</summary>
        public readonly HashSet<int> TrackedIndices = new HashSet<int>();

        /// <summary>本会话生成过的"关键部位"槽位（判定成功用）。</summary>
        public readonly HashSet<int> SpawnedKillTargets = new HashSet<int>();

        /// <summary>已被击杀的槽位。</summary>
        public readonly HashSet<int> KilledIndices = new HashSet<int>();

        /// <summary>是否已播报过现身消息。</summary>
        public bool SpawnAnnounced;

        public BossSession(string groupKey)
        {
            GroupKey = groupKey;
        }
    }
}
