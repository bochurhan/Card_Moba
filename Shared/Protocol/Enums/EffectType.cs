namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果类型 —— 定义单个效果的具体行为类型。
    /// 
    /// 一张卡牌可以有多个效果，每个效果有独立的类型。
    /// 效果按照所属堆叠层分别结算，符合《定策牌结算机制》多子类型拆分铁律。
    /// </summary>
    public enum EffectType
    {
        /// <summary>无效果/占位</summary>
        无 = 0,

        // ── 堆叠1层：防御与数值修正 ──

        /// <summary>获得护甲（减少受到的伤害）</summary>
        获得护甲 = 101,

        /// <summary>获得护盾（吸收伤害）</summary>
        获得护盾 = 102,

        /// <summary>伤害减免（百分比减少受到的伤害）</summary>
        伤害减免 = 103,

        /// <summary>无敌（完全免疫伤害）</summary>
        无敌 = 104,

        /// <summary>增加力量（增加造成的伤害）</summary>
        增加力量 = 111,

        /// <summary>降低力量（减少造成的伤害）</summary>
        降低力量 = 112,

        /// <summary>破甲（降低目标护甲）</summary>
        破甲 = 113,

        /// <summary>穿透（无视目标护甲）</summary>
        穿透 = 114,

        /// <summary>易伤（目标受到的伤害增加）</summary>
        易伤 = 115,

        /// <summary>虚弱（目标造成的伤害减少）</summary>
        虚弱 = 116,

        // ── 堆叠2层-步骤1：主动伤害 ──

        /// <summary>造成伤害（直接扣减目标生命值）</summary>
        造成伤害 = 201,

        // ── 堆叠2层-步骤2：触发式效果 ──

        /// <summary>反伤（受到伤害时反弹伤害）</summary>
        反伤 = 211,

        /// <summary>吸血（造成伤害时回复生命）</summary>
        吸血 = 212,

        /// <summary>受击回血（受到伤害时回复生命）</summary>
        受击回血 = 213,

        /// <summary>受击获得护甲（受到伤害时获得护甲）</summary>
        受击获得护甲 = 214,

        /// <summary>击杀回血（击杀敌人时回复生命）</summary>
        击杀回血 = 215,

        // ── 堆叠3层-普通效果 ──

        /// <summary>抽牌（从牌库抽取卡牌）</summary>
        抽牌 = 301,

        /// <summary>弃牌（弃置手牌）</summary>
        弃牌 = 302,

        /// <summary>回复能量</summary>
        回复能量 = 303,

        /// <summary>回复生命</summary>
        回复生命 = 304,

        /// <summary>沉默（禁止使用技能牌）</summary>
        沉默 = 311,

        /// <summary>眩晕（跳过操作回合）</summary>
        眩晕 = 312,

        /// <summary>减速（行动顺序降低）</summary>
        减速 = 313,

        // ── 堆叠0层：反制效果 ──

        /// <summary>反制卡牌（使目标卡牌无效）</summary>
        反制卡牌 = 401,

        /// <summary>反制首张伤害牌</summary>
        反制首张伤害牌 = 402,

        /// <summary>反制并反弹（使目标卡牌无效并反弹效果）</summary>
        反制并反弹 = 403,
    }
}
