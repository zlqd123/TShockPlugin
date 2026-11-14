using System.Reflection;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace NurseHeal;

/// <summary>
/// 护士治疗（NurseHeal）
///
/// 功能：玩家与「护士」NPC 对话时，按「原版护士」的真实定价逻辑自动扣费并把生命值回满，
/// 不再需要玩家手动点击原版「治疗」按钮、也不再由客户端决定扣多少钱。
///
/// 行为固定、无配置文件：
///   * 始终回满血（与原版护士一致）；
///   * 价格 = 原版护士定价公式 × 1.0（生命 + 减益 + 快乐度 + 世界进度 + 专家/大师）；
///   * 无冷却，每次对话即时结算；
///   * 付费方式固定为扣除背包/存钱罐/保险箱里的原版钱币。
///
/// 定价逻辑见 <see cref="VanillaPricing"/>，扣款见 <see cref="PaymentService"/>。
/// </summary>
[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    public override string Author => "不是现在";

    public override string Description => "与护士对话自动按原版定价扣费并回满血";

    public override string Name => System.Reflection.Assembly.GetExecutingAssembly().GetName().Name!;

    public override Version Version => new Version(1, 0, 0, 0);

    public Plugin(Main game) : base(game)
    {
        this.Order = 3;
    }

    public override void Initialize()
    {
        // 关键钩子：客户端发 NpcTalk(packet 40) 表示「开始/结束与某 NPC 对话」
        GetDataHandlers.NpcTalk.Register(this.OnNpcTalk);

        TShock.Log.ConsoleInfo($"[护士治疗 v{Version}] 已加载（原版定价 · 回满血 · 无冷却 · 背包钱币）");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GetDataHandlers.NpcTalk.UnRegister(this.OnNpcTalk);
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// 「和护士对话」事件的入口。
    /// e.NPCTalkTarget 是 Main.npc 的下标；-1 表示结束对话（不是开始）。
    /// </summary>
    private void OnNpcTalk(object? sender, GetDataHandlers.NpcTalkEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.NPCTalkTarget < 0 || e.NPCTalkTarget >= Main.npc.Length)
        {
            return;
        }

        var npc = Main.npc[e.NPCTalkTarget];
        // netID 是 NPC 的规范类型 ID（护士的各种变体都指向 NPCID.Nurse），比 type 更稳定
        if (npc is null || !npc.active || npc.netID != NPCID.Nurse)
        {
            return;
        }

        var player = e.Player;
        if (player is null || !player.Active || player.Dead || player.TPlayer is null)
        {
            return;
        }

        // NpcTalk 的 handler 在 TShock 自身的 Bouncer 检查「之前」执行；
        // 一旦我们吞包(Handled=true)就会跳过那些检查，所以这里补上等价校验，
        // 不给未登录 / 被禁用 / 被节流的玩家治疗，也不干扰 TShock 的默认处理。
        if (player.IsBeingDisabled() || player.IsBouncerThrottled())
        {
            return;
        }

        // 完全接管护士交互：吞掉这个对话包，并回一个 -1 让客户端不要弹出原版治疗界面。
        // 这与 TShock 源码 HandleNpcTalk 对 disabled 玩家的处理方式一致。
        e.Handled = true;
        player.SendData(PacketTypes.NpcTalk, "", player.Index, -1);

        this.TryHeal(player, npc);
    }

    /// <summary>
    /// 计算价格（原版护士公式）-> 校验余额 -> 扣费 -> 回满血。任何一步失败都不回血、不扣费。
    /// </summary>
    private void TryHeal(TSPlayer player, NPC nurse)
    {
        var maxLife = player.TPlayer.statLifeMax2;
        var current = player.TPlayer.statLife;
        var missing = Math.Max(0, maxLife - current);

        // 满血：不扣费，只提示
        if (missing <= 0)
        {
            player.SendInfoMessage("你现在生命值已满，无需治疗。");
            return;
        }

        // 本次实际回复量 = 缺失量（与原版护士一致：回满）
        var heal = missing;

        // 价格 = 原版护士定价公式
        var price = VanillaPricing.ComputePrice(player, nurse);
        var debuffs = CountDebuffs(player);

        // 先查余额，余额不足直接提示，避免扣款失败还要回滚
        if (PaymentService.GetBalance(player) < price)
        {
            player.SendErrorMessage($"治疗需要 {price} 铜币，你的余额不足！");
            return;
        }

        var deduct = PaymentService.Deduct(player, price);
        if (!deduct.Success)
        {
            player.SendErrorMessage($"治疗失败：{deduct.Reason ?? "扣款失败"}");
            return;
        }

        // 回血：
        // 1) 同步服务端 statLife（供 SSC 与后续价格计算使用，不发 PlayerHp 包以免与特效包重复加血）
        // 2) 调用 TShock 官方 Heal(heal)，广播 PlayerHealOther，由客户端加血并播放回血特效
        player.TPlayer.statLife = Math.Min(maxLife, current + heal);
        player.Heal(heal);

        player.SendSuccessMessage($"护士为你治疗，回复 {heal} 点生命（含 {debuffs} 个减益），花费 {price} 铜币。");

        TShock.Log.Write(
            $"[护士治疗] {player.Name} 回复 {heal} 点生命，花费 {price} 铜币",
            System.Diagnostics.TraceLevel.Info);
    }

    /// <summary>统计玩家当前生效的减益数量（沿用原版 Main.debuff 判定）。</summary>
    private static int CountDebuffs(TSPlayer player)
    {
        var p = player.TPlayer;
        bool[]? debuffArr = null;
        try
        {
            var fi = typeof(Main).GetField("debuff", BindingFlags.Public | BindingFlags.Static);
            if (fi is not null)
            {
                debuffArr = fi.GetValue(null) as bool[];
            }
        }
        catch
        {
            debuffArr = null;
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
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace NurseHeal;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    /// <summary>玩家与护士的最大交互距离(像素)。原版客户端只能在近距离内右键护士,这里放宽到 50 格,防改包客户端远程治疗。</summary>
    private const float MaxTalkDistance = 800f;

    public override string Author => "不是现在";

    public override string Description => "与护士对话:按原版定价公式扣费,回满血并移除可移除的减益";

    public override string Name => Assembly.GetExecutingAssembly().GetName().Name;

    public override Version Version => new Version(1, 0, 0, 0);

    public Plugin(Main game)
        : base(game)
    {
        Order = 3;
    }

    public override void Initialize()
    {
        GetDataHandlers.NpcTalk.Register(OnNpcTalk, HandlerPriority.Normal, false);
        TShock.Log.ConsoleInfo($"[护士治疗 v{Version}] 已加载(原版定价公式 · 移除减益 · 找零不足即回滚)");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GetDataHandlers.NpcTalk.UnRegister(OnNpcTalk);
        }
        base.Dispose(disposing);
    }

    private void OnNpcTalk(object? sender, GetDataHandlers.NpcTalkEventArgs e)
    {
        if (e.Handled || e.NPCTalkTarget < 0 || e.NPCTalkTarget >= Main.npc.Length)
        {
            return;
        }
        NPC nurse = Main.npc[e.NPCTalkTarget];
        if (nurse is not { active: true, netID: NPCID.Nurse })
        {
            return;
        }
        TSPlayer player = e.Player;
        if (player is not { Active: true, Dead: false, TPlayer: not null }
            || player.IsBeingDisabled() || player.IsBouncerThrottled())
        {
            return;
        }

        // 吞掉原包并回发 NpcTalk(-1) 关闭客户端原版护士界面(与 TShock 对 disabled 玩家的处理一致),
        // 之后由本插件统一结算,避免玩家再手动点原版“治疗”按钮、由客户端决定扣多少钱。
        e.Handled = true;
        player.SendData(PacketTypes.NpcTalk, "", player.Index, -1f);

        // 原版客户端只能在交互距离内右键护士;服务端补上距离校验(README 未覆盖、旧版缺失)
        if (DistanceSq(player.TPlayer, nurse) > MaxTalkDistance * MaxTalkDistance)
        {
            player.SendErrorMessage("你距离护士太远,无法接受治疗。");
            return;
        }

        ServeNurse(player, nurse);
    }

    private static float DistanceSq(Player p, NPC nurse)
    {
        float dx = p.Center.X - nurse.Center.X;
        float dy = p.Center.Y - nurse.Center.Y;
        return dx * dx + dy * dy;
    }

    private static void ServeNurse(TSPlayer player, NPC nurse)
    {
        Player p = player.TPlayer;
        int missing = Math.Max(0, p.statLifeMax2 - p.statLife);
        List<int> debuffSlots = VanillaPricing.CollectRemovableDebuffSlots(p);
        long price = VanillaPricing.ComputePrice(p, nurse);

        if (missing <= 0 && debuffSlots.Count == 0)
        {
            player.SendInfoMessage("你现在生命值已满,也没有可清除的减益,无需治疗。");
            return;
        }

        if (PaymentService.GetBalance(p) < price)
        {
            player.SendErrorMessage($"治疗需要 {price} 铜币,你的余额不足!");
            return;
        }

        DeductResult pay = PaymentService.Deduct(p, price);
        if (!pay.Success)
        {
            player.SendErrorMessage("治疗失败:" + (pay.Reason ?? "扣款失败"));
            return;
        }

        // 扣款成功后才同步被改动的物品槽(纯内存操作,不走 GiveItem,避免 SSC/掉落分支竞态)
        foreach (int slot in pay.ChangedSlots)
        {
            player.SendData(PacketTypes.PlayerSlot, "", player.Index, slot, 0f, 0f, 0);
        }

        // 回血:TShock 6.2.1 的 TSPlayer.Heal 只广播 PlayerHealOther(66) 包、不改服务端 statLife,
        // 所以服务端必须先手动落账(若未来 Heal 语义变化需回看此处)
        if (missing > 0)
        {
            p.statLife = Math.Min(p.statLifeMax2, p.statLife + missing);
            player.Heal(missing);
        }

        // 移除减益并全量同步 buff 列表(msg 50),客户端收到后同步清掉本地减益
        if (debuffSlots.Count > 0)
        {
            foreach (int i in debuffSlots)
            {
                p.buffType[i] = 0;
                p.buffTime[i] = 0;
            }
            player.SendData(PacketTypes.PlayerBuff, "", player.Index);
        }

        string lifePart = missing > 0 ? $"回复 {missing} 点生命" : "";
        string buffPart = debuffSlots.Count > 0 ? $"清除 {debuffSlots.Count} 个减益" : "";
        string join = lifePart.Length > 0 && buffPart.Length > 0 ? "," : "";
        player.SendSuccessMessage($"护士治疗完成:{lifePart}{join}{buffPart},花费 {price} 铜币。");
        TShock.Log.Write($"[护士治疗] {player.Name}:{lifePart}{join}{buffPart},花费 {price} 铜币", TraceLevel.Info);
    }
}
