#pragma warning disable CS8632

// ⚠️ 注意：EffectTypeV2 枚举已迁移至 CardMoba.Protocol.Enums.EffectType（Protocol 程序集）
// BattleCore 内所有新代码请直接 using CardMoba.Protocol.Enums; 并使用 EffectType。

namespace CardMoba.BattleCore.Foundation
{
    /// <summary>
    /// 效果结算层（决定效果在定策牌结算中的优先级顺序）。
    /// 同层内按出牌顺序执行，跨层严格按层级先后。
    ///
    /// ⚠️ 此枚举与 Protocol.Enums.SettlementLayer 保持数值一致，是其运行时镜像。
    /// 未来可直接替换为 Protocol.Enums.SettlementLayer。
    /// </summary>
    public enum SettleLayer
    {
        /// <summary>Layer 0 — 反制层（最高优先级）</summary>
        Counter = 0,

        /// <summary>Layer 1 — 防御与数值修正层（护盾/护甲/力量增减）</summary>
        Defense = 1,

        /// <summary>Layer 2 — 伤害层（直接/间接伤害，含吸血/荆棘触发）</summary>
        Damage = 2,

        /// <summary>Layer 3 — 资源层（抽牌/弃牌/能量/卡牌生成）</summary>
        Resource = 3,

        /// <summary>Layer 4 — 增益/特殊层（Buff附加/控制/复刻等）</summary>
        BuffSpecial = 4,
    }

}
