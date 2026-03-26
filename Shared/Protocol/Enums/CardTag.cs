using System;

namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌标签（词条）—— 统一标记卡牌的用途分类和特殊行为。
    /// Card Tag — Unified marking for card purpose classification and special behaviors.
    /// 
    /// 使用 Flags 支持单张卡牌拥有多个标签，例如：
    /// - 「铁斩波」= Damage | Defense（既造成伤害，又提供护甲）
    /// - 「暗影吞噬」= Damage | Exhaust（伤害牌，使用后移出游戏）
    /// 
    /// 标签用于：
    /// 1. UI 展示（卡牌边框、分类筛选、关键词高亮）
    /// 2. 卡组构建规则验证
    /// 3. 效果的目标筛选（如"反制首张伤害牌"检查 Damage 标签）
    /// 4. 特殊行为规则（如 Exhaust 表示使用后移出游戏）
    /// 
    /// 注意：实际结算层由 EffectType 决定，而非 CardTag。
    /// </summary>
    [Flags]
    public enum CardTag
    {
        /// <summary>无标签 (None)</summary>
        None = 0,

        // ══════════════════════════════════════
        // 用途分类标签（原 SubType）
        // Purpose Classification Tags
        // ══════════════════════════════════════

        /// <summary>伤害：对目标造成数值伤害，可被反制 (Damage: deals numeric damage, can be countered)</summary>
        Damage = 1 << 0,      // 1

        /// <summary>防御：护甲、护盾、伤害减免等 (Defense: armor, shield, damage reduction)</summary>
        Defense = 1 << 1,     // 2

        /// <summary>反制：无效化/惩罚对手卡牌，本回合锁定下回合触发 (Counter: negates/punishes enemy cards)</summary>
        Counter = 1 << 2,     // 4

        /// <summary>增益：正面数值修正（力量、攻击增加等）(Buff: positive stat modifiers)</summary>
        Buff = 1 << 3,        // 8

        /// <summary>减益：负面数值修正（易伤、虚弱、破甲等）(Debuff: negative stat modifiers)</summary>
        Debuff = 1 << 4,      // 16

        /// <summary>支援：跨路支援、队友加成等 (Support: cross-lane assistance, ally buffs)</summary>
        Support = 1 << 5,     // 32

        /// <summary>传说：传说专属卡牌，独立优先级 (Legendary: unique priority, special effects)</summary>
        Legendary = 1 << 6,   // 64

        /// <summary>控制：沉默、眩晕、减速等 (Control: silence, stun, slow)</summary>
        Control = 1 << 7,     // 128

        /// <summary>资源：抽牌、回能、回血等 (Resource: draw, energy, healing)</summary>
        Resource = 1 << 8,    // 256

        // ══════════════════════════════════════
        // 特殊行为标签（原 Tag）
        // Special Behavior Tags
        // ══════════════════════════════════════

        /// <summary>跨路生效：可以作用于非同路的玩家 (CrossLane: can affect players in other lanes)</summary>
        CrossLane = 1 << 9,   // 512

        /// <summary>循环：打出后不进弃牌堆，返回牌库底部 (Recycle: returns to deck instead of discard pile)</summary>
        Recycle = 1 << 10,    // 1024

        /// <summary>消耗：打出后从游戏中移除，不进弃牌堆 (Exhaust: removed from game when played)</summary>
        Exhaust = 1 << 11,    // 2048

        /// <summary>固有：战斗开始时必定在起手 (Innate: always in starting hand)</summary>
        Innate = 1 << 12,     // 4096

        /// <summary>保留：回合结束不会被弃置 (Retain: not discarded at end of turn)</summary>
        Retain = 1 << 13,     // 8192

        /// <summary>斩杀：对低血量目标有额外效果 (Execute: bonus effect against low HP targets)</summary>
        Execute = 1 << 14,    // 16384

        /// <summary>反弹：反制牌附带效果，将被反制卡牌的伤害反弹给攻击者 (Reflect: bounces countered card's damage back to attacker)</summary>
        Reflect = 1 << 15,    // 32768

        Status = 1 << 16,     // 65536
    }
}
