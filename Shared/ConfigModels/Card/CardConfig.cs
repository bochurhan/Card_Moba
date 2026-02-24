using CardMoba.Protocol.Enums;

namespace CardMoba.ConfigModels.Card
{
    /// <summary>
    /// 卡牌静态配置 —— 定义一张卡牌的所有不变属性。
    /// 这是"配置表"里一行数据的 C# 映射，运行时只读。
    /// </summary>
    public class CardConfig
    {
        // ── 基础信息 ──

        /// <summary>卡牌唯一ID（全局唯一，配置表主键）</summary>
        public int CardId { get; set; }

        /// <summary>卡牌显示名称</summary>
        public string CardName { get; set; } = string.Empty;

        /// <summary>卡牌描述文本（展示给玩家看的效果说明）</summary>
        public string Description { get; set; } = string.Empty;

        // ── 类型分类 ──

        /// <summary>双轨类型：瞬策牌 or 定策牌</summary>
        public CardTrackType TrackType { get; set; }

        /// <summary>功能子类型：伤害型/功能型/反制型/防御型</summary>
        public CardSubType SubType { get; set; }

        /// <summary>目标类型：决定这张牌作用于谁</summary>
        public CardTargetType TargetType { get; set; }

        // ── 费用 ──

        /// <summary>能量消耗（每回合玩家获得固定能量，出牌消耗能量）</summary>
        public int EnergyCost { get; set; }

        // ── 效果数值 ──

        /// <summary>基础效果值（伤害牌=伤害量，防御牌=护盾量，功能牌=效果强度）</summary>
        public int EffectValue { get; set; }

        /// <summary>效果持续回合数（0=即时生效无持续，>0=持续N回合的buff/debuff）</summary>
        public int Duration { get; set; }

        // ── 稀有度 ──

        /// <summary>稀有度等级（1=普通，2=稀有，3=史诗，4=传说）</summary>
        public int Rarity { get; set; } = 1;
    }
}
