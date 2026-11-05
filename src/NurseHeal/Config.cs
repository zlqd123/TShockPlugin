namespace NurseHeal;

/// <summary>
/// 插件常量。
///
/// 本插件不读写配置文件，行为固定：
///   * 治疗始终回满血（与原版护士一致）；
///   * 价格 = 原版护士定价公式 × 价格系数 1.0（即不加任何倍率）；
///   * 无冷却，每次对话即时结算；
///   * 付费方式固定为扣除背包/存钱罐/保险箱里的原版钱币。
/// </summary>
public static class Consts
{
    /// <summary>价格系数，固定 1.0（表示完全按原版公式，不做任何缩放）。</summary>
    public const double PriceMultiplier = 1.0;
}
