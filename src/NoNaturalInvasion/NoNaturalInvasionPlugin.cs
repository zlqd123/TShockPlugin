using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace NoNaturalInvasion
{
    /// <summary>
    /// NoNaturalInvasion —— 只禁止清晨自然触发的哥布林军团 / 海盗入侵，手动召唤与命令不受影响。
    ///
    /// 背景与原理（Terraria 1.4.5.8 / TShock 6.2.1）：
    /// - 自然触发只发生在每天清晨 4:30（Main.dayTime 翻转、Main.time 归零后的那一两个 tick），
    ///   而手动召唤（哥布林战旗 361 / 海盗地图 1315）与 TShock /invade 命令在一天任意时刻都可用；
    ///   原版两条路径最终都调用 Main.StartInvasion(type)（type 1=哥布林军团、3=海盗入侵），
    ///   且没有任何"来源"标记 —— 因此唯一可靠的区分点是触发时刻。
    /// - 多人是客户端权威：服务器看不到"使用物品"这个动作本身，无法从数据包层面拦截召唤，
    ///   唯一可靠的拦截点是所有路径的必经之处 Main.StartInvasion（MonoMod detour）。
    ///
    /// 机制（三层，逐层兜底）：
    /// 1. 每秒检测（GameUpdate + OnTickForThirdPartySoftwareOnly 双泵时间戳去重）：
    ///    清晨翻转后延后 5 秒（清晨窗口，避开 4:30 各插件高频触发的时间点）做终检 ——
    ///    若入侵激活、且确认非上一夜残留（跨夜残留视为手动）、且窗口内没有放行过手动召唤/命令，
    ///    则判定为自然触发，立即取消并发送全服广播。
    /// 2. MonoMod detour Main.StartInvasion（按"名字 + 单 int 参数"绑定，失败自动降级为纯计时模式）：
    ///    - 清晨窗口内的物品召唤（战旗/海盗地图）：拦截（入侵不启动），道具在下一 tick 原样返还
    ///      （detour 时原版尚未扣道具，先排队、后返还，避免"先发后扣"抵消）；
    ///    - 窗口内的 /invade 命令（调用栈含 TShockAPI 帧）：照常放行；
    ///    - 窗口外的所有召唤/命令：照常放行；
    ///    - 自然触发（调用栈含 WorldGen / Main.CheckProgress / Main.*UpdateTime* 帧，
    ///      即 mfwh_UpdateTime_StartDay 清晨滚动链路）：放行，交给第 1 层在窗口结束后取消。
    /// 3. 若 detour 绑定失败（模式=纯计时）：仅剩第 1 层。
    ///
    /// v1.0.1 变更：
    /// - 自然判定补上 mfwh 构建的清晨滚动帧（mfwh_UpdateTime_StartDay / UpdateTime）——
    ///   v1.0.0 只认 WorldGen/CheckProgress 帧，把自然触发误判为"手动召唤"拦在源头；
    /// - DawnWindow 5.0 → 300.0：Main.time 60 单位≈1 现实秒，v1.0.0 的 5.0 实为 83 毫秒窗口；
    /// - 启动假黎明：首个采样先用 Main.dayTime 初始化 _lastDayTime，不再把白天误判为清晨翻转；
    /// - 关键判定行与泵心跳写入文件日志（TShock.Log.Info → ServerLog.txt），便于事后排查。
    /// </summary>
    [ApiVersion(2, 1)]
    public class NoNaturalInvasionPlugin : TerrariaPlugin
    {
        public override string Name => "NoNaturalInvasion";
        public override string Author => "不是现在";
        public override string Description =>
            "仅禁止清晨自然触发的哥布林军团/海盗入侵；手动召唤（战旗/海盗地图）与 /invade 命令不受影响。";
        public override Version Version => new Version(1, 0, 1);

        // 清晨检测窗口（Main.time 单位，60 单位 ≈ 1 现实秒；白天 0→54000）：
        // 4:30 后 5 秒内的召唤无法与自然触发区分（同 tick 竞争时原版也不做区分），窗口内一律拦截+返还。
        private const double DawnWindow = 300.0;

        private IDisposable? _detour;

        // ------------------------------------------------------------------
        // 状态（均为每秒采样粒度）
        // ------------------------------------------------------------------

        private long _lastPumpStamp = -1;
        private long _lastSecondStamp = -1;
        private bool _samplePrimed;         // 首个采样只初始化、不做清晨判定（修启动假黎明）
        private bool _lastDayTime;
        private bool _dawnSeen;             // 本个清晨翻转已记录，等待窗口结束后的终检
        private bool _invasionActiveSample; // 上一次采样时入侵是否进行中
        private bool _activeBeforeDawn;     // 翻转前（夜侧）最后一次采样入侵是否进行中（跨夜残留 = 手动）
        private bool _manualPassedSinceDawn; // 本个清晨窗口内是否放行过手动召唤/命令

        // 诊断：泵计数与心跳（每 5 分钟一条，清晨窗口内每秒一条，写文件日志）
        private long _pumpCount;
        private long _heartbeatStamp = -1;

        // 待返还队列：(玩家, 物品 ID, 前缀)。detour 发生在原版扣道具之前，必须延迟到下一 tick 返还。
        private readonly List<(int Plr, int ItemId, int Prefix)> _pendingRefunds = new();

        internal bool DetourActive { get; private set; }

        /// <summary>
        /// 判定行统一出口：控制台 + 自追加文件 NoNaturalInvasion.log（与 ServerLog.txt 同目录）。
        /// TShock.Log.Info 在部分构建不落盘、不进 ServerLog.txt，因此文件由插件自己写。
        /// </summary>
        private static void Line(string format, params object?[] args)
        {
            string msg = args.Length == 0 ? format : string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
            TShock.Log.ConsoleInfo("{0}", msg);
            try
            {
                string path = Path.Combine(TShock.SavePath, "NoNaturalInvasion.log");
                File.AppendAllText(path,
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}", DateTime.Now, msg, Environment.NewLine));
            }
            catch
            {
                // 文件写入失败不影响拦截逻辑
            }
        }

        public NoNaturalInvasionPlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            // 主线程泵。GameUpdate 在部分 TSAPI/OTAPI 组合下不触发，
            // 同时挂原版专用服务器主循环的 OnTickForThirdPartySoftwareOnly，时间戳去重共用一个泵。
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            Main.OnTickForThirdPartySoftwareOnly += OnServerTick;

            // MonoMod detour：所有触碰 MonoMod 的代码集中在 DetourBridge，
            // 服务器未捆绑 MonoMod 时这里会抛 TypeLoadException，捕获后降级为纯计时模式。
            try
            {
                StartInvasionDetourDelegate detour = StartInvasionDetour;
                _detour = DetourBridge.TryCreate(detour);
                DetourActive = _detour is not null;
            }
            catch (Exception ex)
            {
                DetourActive = false;
                TShock.Log.ConsoleWarn(
                    "[NoNaturalInvasion] Main.StartInvasion detour 绑定失败，降级为纯计时模式（清晨窗口内的手动召唤将无法区分，可能被误取消且不返还）：{0}",
                    ex.Message);
                Line(
                    "[NoNaturalInvasion] detour 绑定失败，模式=纯计时(回退)：{0}", ex.Message);
            }

            Line(
                "[NoNaturalInvasion] v{0} 已加载：模式={1}，清晨窗口={2:0.#}秒；仅拦自然触发，手动召唤与命令不受影响。",
                Version, DetourActive ? "精确(detour)" : "纯计时(回退)", DawnWindow / 60);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                Main.OnTickForThirdPartySoftwareOnly -= OnServerTick;
                _detour?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------
        // 主线程泵：每 tick 冲一次返还队列，每 60 tick（1 秒）做一次检测
        // ------------------------------------------------------------------

        private void OnGameUpdate(EventArgs args) => Pump();

        private void OnServerTick() => Pump();

        private void Pump()
        {
            long stamp = Environment.TickCount64;
            if (stamp == _lastPumpStamp)
                return; // 双泵去重：同一 tick 只跑一次
            _lastPumpStamp = stamp;
            _pumpCount++;

            FlushRefunds();

            if (_lastSecondStamp >= 0 && stamp - _lastSecondStamp < 1000)
                return; // 每秒（60 tick）一次
            _lastSecondStamp = stamp;

            PerSecondCheck();
        }

        // ------------------------------------------------------------------
        // 每秒检测：记录清晨翻转，窗口结束后终检并取消自然入侵
        // ------------------------------------------------------------------

        private void PerSecondCheck()
        {
            var dayTime = Main.dayTime;
            bool invasionActive = Main.invasionType is 1 or 3;

            if (!_samplePrimed)
            {
                // 首个采样只做初始化：直接取当前 dayTime，避免把白天误判为清晨翻转（启动假黎明）。
                _samplePrimed = true;
                _lastDayTime = dayTime;
                _invasionActiveSample = invasionActive;
                _heartbeatStamp = Environment.TickCount64;
                return;
            }

            if (dayTime && !_lastDayTime)
            {
                // 清晨 4:30 翻转：记录夜侧（翻转前）入侵是否已在进行 —— 跨夜残留必然是手动召唤。
                // 自然触发发生在翻转后的第一个 tick，不会影响夜侧采样。
                _dawnSeen = true;
                _activeBeforeDawn = _invasionActiveSample;
                _manualPassedSinceDawn = false;
                Line("[NoNaturalInvasion] 清晨翻转已记录（time={0:0}，入侵={1}，夜侧残留={2}）",
                    Main.time, invasionActive, _activeBeforeDawn);
            }
            _lastDayTime = dayTime;
            _invasionActiveSample = invasionActive;

            // 延后 5 秒（Main.time ≥ 清晨窗口）再终检，避开 4:30 各插件高频触发的时间点。
            if (_dawnSeen && dayTime && Main.time >= DawnWindow)
            {
                _dawnSeen = false;
                bool leftover = _activeBeforeDawn;
                _activeBeforeDawn = false;

                if (invasionActive && !leftover && !_manualPassedSinceDawn)
                {
                    int type = Main.invasionType;
                    Main.invasionSize = 0;
                    Main.invasionType = 0;

                    TShock.Utils.Broadcast(
                        type == 1 ? "自然触发的哥布林军团已被禁止。" : "自然触发的海盗入侵已被禁止。",
                        255, 170, 0);
                    Line("[NoNaturalInvasion] 已取消自然入侵事件（type={0}）", type);
                }
                else if (invasionActive)
                {
                    Line("[NoNaturalInvasion] 终检放行（type={0}，残留={1}，窗口内手动={2}）",
                        Main.invasionType, leftover, _manualPassedSinceDawn);
                }
            }

            // 心跳：清晨窗口内每秒一条，其余每 5 分钟一条（证明泵存活，写文件日志）
            long now = Environment.TickCount64;
            if (_dawnSeen || now - _heartbeatStamp >= 300_000)
            {
                _heartbeatStamp = now;
                Line("[NoNaturalInvasion] 心跳：泵={0} dayTime={1} time={2:0} 入侵={3} 清晨待终检={4}",
                    _pumpCount, dayTime, Main.time, Main.invasionType, _dawnSeen);
            }
        }

        // ------------------------------------------------------------------
        // Main.StartInvasion detour：区分 自然 / 命令 / 物品召唤
        // ------------------------------------------------------------------

        internal void StartInvasionDetour(StartInvasionOrig orig, int type)
        {
            try
            {
                if (type != 1 && type != 3)
                {
                    orig(type); // 其他事件（霜冻军团/火星暴乱等）不做处理
                    return;
                }

                if (Main.dayTime && Main.time <= DawnWindow)
                {
                    // 清晨窗口内：三种来源分流
                    if (StackLooksNatural())
                    {
                        // 自然触发：放行，交给每秒终检在窗口结束后取消并广播
                        Verdict(type, "自然触发", true);
                        orig(type);
                        return;
                    }

                    if (StackHasTShockCommand())
                    {
                        // /invade 等命令：照常放行，并标记本窗口内出现过手动入侵（终检跳过）
                        _manualPassedSinceDawn = true;
                        Verdict(type, "命令召唤", true);
                        orig(type);
                        return;
                    }

                    // 物品手动召唤（哥布林战旗/海盗地图）：拦截，道具下一 tick 返还
                    Verdict(type, "物品召唤", false);
                    BlockManualSummon(type);
                    return;
                }

                // 窗口外：自然触发只可能发生在清晨窗口内，这里出现的都是手动 —— 照常放行。
                // 若终检尚未跑（时钟落在窗口结束与终检之间的间隙），该标记会让终检跳过。
                _manualPassedSinceDawn = true;
                Verdict(type, "窗口外召唤/命令", true);
                orig(type);
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError("[NoNaturalInvasion] StartInvasion detour 处理异常：{0}", ex);
                Line("[NoNaturalInvasion] StartInvasion detour 处理异常：{0}", ex.Message);
                orig(type); // 兜底：宁可放行也不吞掉原版调用
            }
        }

        private void BlockManualSummon(int invasionType)
        {
            int itemId = invasionType == 1 ? ItemID.GoblinBattleStandard : ItemID.PirateMap;

            // 找出召唤者：优先"正在挥动该物品"（itemAnimation > 0），回退为"手持该物品"。
            int plr = FindSummoner(itemId, requireAnimation: true);
            if (plr < 0)
                plr = FindSummoner(itemId, requireAnimation: false);

            if (plr >= 0)
            {
                _pendingRefunds.Add((plr, itemId, Main.player[plr].HeldItem.prefix));
            }

            string itemName = invasionType == 1 ? "哥布林战旗" : "海盗地图";
            if (plr >= 0 && TShock.Players[plr] is { } ts)
            {
                ts.SendInfoMessage($"清晨检测窗口内已拦截手动召唤，{itemName}将自动返还（{DawnWindow / 60:0}秒后可正常召唤）。");
            }

            Line("[NoNaturalInvasion] 已拦截清晨窗口内的手动召唤：{0}（玩家：{1}）",
                itemName, plr >= 0 ? Main.player[plr].name : "未定位，道具未返还");
        }

        private static int FindSummoner(int itemId, bool requireAnimation)
        {
            for (int i = 0; i < Main.player.Length; i++)
            {
                var p = Main.player[i];
                if (p is not { active: true })
                    continue;
                if (requireAnimation && p.itemAnimation <= 0)
                    continue;
                if (p.HeldItem?.type == itemId)
                    return i;
            }
            return -1;
        }

        /// <summary>每条 detour 判定写一行文件日志：分类、处置、时间与栈顶调用者。</summary>
        private static void Verdict(int type, string source, bool pass)
        {
            Line("[NoNaturalInvasion] StartInvasion 判定：type={0} 分类={1} 处置={2} dayTime={3} time={4:0} 调用者={5}",
                type, source, pass ? "放行" : "拦截", Main.dayTime, Main.time, TopFrameName());
        }

        /// <summary>栈顶的真实调用者（跳过本插件帧、Main.StartInvasion 包装器与 MonoMod 动态蹦床帧）。</summary>
        private static string TopFrameName()
        {
            var frames = new StackTrace(false).GetFrames();
            if (frames is null)
                return "?";
            string dynamicName = "?";
            foreach (var f in frames)
            {
                var m = f?.GetMethod();
                if (m is null)
                    continue;
                var declaring = m.DeclaringType;
                // MonoMod 蹦床是动态方法（Hook<...> / SyncProxy<...> 等，DeclaringType 为空）
                if (declaring is null)
                {
                    if (dynamicName == "?")
                        dynamicName = m.Name;
                    continue;
                }
                if (declaring == typeof(NoNaturalInvasionPlugin))
                    continue;
                if (declaring == typeof(Main) && m.Name == "StartInvasion")
                    continue;
                return declaring.Name + "." + m.Name;
            }
            return dynamicName;
        }

        private static bool StackLooksNatural()
        {
            var frames = new StackTrace(false).GetFrames();
            if (frames is null)
                return false;

            bool sawNatural = false;
            bool sawPlayerSummon = false;
            foreach (var f in frames)
            {
                var m = f?.GetMethod();
                if (m is null)
                    continue;
                var declaring = m.DeclaringType;
                // 自然滚动判定所在帧：
                // - WorldGen.* / Main.CheckProgress（1.4.4/1.4.5 的两个可能位置）
                // - Main.*UpdateTime*（mfwh 构建清晨滚动链路 mfwh_UpdateTime_StartDay / mfwh_UpdateTime）
                if (declaring == typeof(WorldGen))
                    sawNatural = true;
                else if (declaring == typeof(Main) &&
                         (m.Name == "CheckProgress" || m.Name.Contains("UpdateTime") || m.Name.Contains("StartDay")))
                    sawNatural = true;
                // 物品召唤链路（Player.ItemCheck_* → Main.StartInvasion）
                else if (declaring == typeof(Player))
                    sawPlayerSummon = true;
            }

            if (sawNatural && !sawPlayerSummon)
                return true;
            // 兜底：清晨窗口内、既无玩家物品链路也无命令链路的召唤视为自然触发。
            return !sawPlayerSummon && !StackHasTShockCommand();
        }

        private static bool StackHasTShockCommand()
        {
            var frames = new StackTrace(false).GetFrames();
            if (frames is null)
                return false;
            foreach (var f in frames)
            {
                var ns = f?.GetMethod()?.DeclaringType?.FullName;
                if (ns is not null && ns.StartsWith("TShockAPI", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 返还队列：每 tick 冲一次。detour 时原版尚未扣道具（UseItem 返回 true 后才扣），
        // 推迟到下一 tick 执行 GiveItem，避免"先发后扣"互相抵消。
        // ------------------------------------------------------------------

        private void FlushRefunds()
        {
            if (_pendingRefunds.Count == 0)
                return;

            foreach (var (plr, itemId, prefix) in _pendingRefunds)
            {
                var ts = plr >= 0 && plr < TShock.Players.Length ? TShock.Players[plr] : null;
                if (ts is null)
                {
                    Line("[NoNaturalInvasion] 返还失败：玩家 {0} 已离线（物品 {1}）", plr, itemId);
                    continue;
                }
                ts.GiveItem(itemId, 1, prefix);
            }
            _pendingRefunds.Clear();
        }
    }
}
