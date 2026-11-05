using Terraria;
using Terraria.ID;
using TShockAPI;

namespace NurseHeal;

/// <summary>
/// 扣款结果。
/// </summary>
public sealed class DeductResult
{
    public bool Success { get; init; }
    public string? Reason { get; init; }
    public static DeductResult Ok() => new() { Success = true };
    public static DeductResult Fail(string reason) => new() { Success = false, Reason = reason };
}

/// <summary>
/// 付费方式固定为：直接扣玩家背包 / 存钱罐 / 保险箱里的原版钱币（单位统一换算成铜币）。
///
/// 扣法：把所有钱币堆「收拢」成铜币总额，扣掉 price，
/// 再把找零按 铂金→金→银→铜 重新分发回空槽，最后统一发 PlayerSlot 同步包。
/// 全程只改服务端内存 + 发包，不调用 GiveItem（避免 SSC/掉落分支的竞态）。
/// </summary>
public static class PaymentService
{
    // 原版钱币物品 ID：71 铜币 / 72 银币 / 73 金币 / 74 铂金币
    private const int CopperCoin = 71;
    private const int SilverCoin = 72;
    private const int GoldCoin = 73;
    private const int PlatinumCoin = 74;

    private static readonly int[] CoinIds = [CopperCoin, SilverCoin, GoldCoin, PlatinumCoin];
    private static readonly long[] CoinValues = [1L, 100L, 10000L, 1000000L];

    // 单堆上限，与钱币自动合并阈值保持一致
    private const int CoinStackMax = 100;

    /// <summary>查询余额（铜币）。固定为背包钱币总额。</summary>
    public static long GetBalance(TSPlayer player) => CountInventoryCopper(player);

    /// <summary>扣款，返回是否成功及失败原因。固定从背包/存钱罐/保险箱扣原版钱币。</summary>
    public static DeductResult Deduct(TSPlayer player, long price)
    {
        if (price <= 0)
        {
            return DeductResult.Ok();
        }

        return TakeInventoryCopper(player, price)
            ? DeductResult.Ok()
            : DeductResult.Fail("背包钱币不足");
    }

    #region 背包钱币

    /// <summary>钱币堆引用 + 对应的 PlayerSlot 同步槽位。</summary>
    private readonly record struct CoinSlot(Item Item, int Slot);

    private static List<CoinSlot> GetCoinSlots(TSPlayer player)
    {
        var p = player.TPlayer;
        var list = new List<CoinSlot>();

        // 背包 0..57：0-49 普通物品、50-53 钱币专用槽、54-57 弹药槽
        // （钱币也可能被拖到普通槽里，所以整段都扫）
        for (var i = 0; i < p.inventory.Length && i < 58; i++)
        {
            var it = p.inventory[i];
            list.Add(new CoinSlot(it, PlayerItemSlotID.Inventory0 + i));
        }
        for (var i = 0; i < p.bank.item.Length; i++)
        {
            list.Add(new CoinSlot(p.bank.item[i], PlayerItemSlotID.Bank1_0 + i));
        }
        for (var i = 0; i < p.bank2.item.Length; i++)
        {
            list.Add(new CoinSlot(p.bank2.item[i], PlayerItemSlotID.Bank2_0 + i));
        }
        return list;
    }

    private static bool IsCoin(Item? item) => item is not null && item.stack > 0 && CoinIds.Contains(item.type);

    private static long CoinValueOf(int type)
    {
        var i = Array.IndexOf(CoinIds, type);
        return i >= 0 ? CoinValues[i] : 0L;
    }

    private static long CountInventoryCopper(TSPlayer player) => GetCoinSlots(player)
        .Where(c => IsCoin(c.Item))
        .Sum(c => (long) c.Item.stack * CoinValueOf(c.Item.type));

    /// <summary>从玩家所有钱币里扣 price 铜币并自动找零。返回是否成功。</summary>
    private static bool TakeInventoryCopper(TSPlayer player, long price)
    {
        var slots = GetCoinSlots(player);
        var coinSlots = slots.Where(c => IsCoin(c.Item)).ToList();
        var total = coinSlots.Sum(c => (long) c.Item.stack * CoinValueOf(c.Item.type));
        if (total < price)
        {
            return false;
        }

        var change = total - price;

        // 1) 清空所有钱币堆
        foreach (var c in coinSlots)
        {
            c.Item.type = 0;
            c.Item.stack = 0;
            c.Item.prefix = 0;
        }

        // 2) 找零按大额→小额重新分发。
        //    可用空槽 = 刚才清空的钱币槽 + 所有其它空物品槽（含普通背包/钱币槽/弹药槽/储蓄罐/保险箱）。
        //    这样即使玩家只有 1 个铂金币、只扣 1 铜币，也能把 99铂+99金+99银+99铜 完整找回。
        var emptySlots = coinSlots.Select(c => c.Item).ToList();
        foreach (var c in slots)
        {
            if (!emptySlots.Contains(c.Item) && c.Item is { type: 0, stack: 0 })
            {
                emptySlots.Add(c.Item);
            }
        }

        var idx = 0;
        for (var i = CoinIds.Length - 1; i >= 0 && change > 0; i--)
        {
            var count = change / CoinValues[i];
            change -= count * CoinValues[i];
            while (count > 0 && idx < emptySlots.Count)
            {
                var stack = (int) Math.Min(count, CoinStackMax);
                var item = emptySlots[idx++];
                item.type = CoinIds[i];
                item.stack = stack;
                item.prefix = 0;
                count -= stack;
            }
            // 槽位用尽仍有零头（极端情况）：把剩余零头折算进下一级小额继续分发
            if (count > 0 && i > 0)
            {
                change += count * CoinValues[i];
            }
        }

        // 3) 统一同步所有涉及到的槽位
        foreach (var c in slots)
        {
            player.SendData(PacketTypes.PlayerSlot, "", player.Index, c.Slot);
        }
        return true;
    }

    #endregion
}
