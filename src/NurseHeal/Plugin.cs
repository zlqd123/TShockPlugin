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
