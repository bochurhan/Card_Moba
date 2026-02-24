namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌功能子类型 —— 决定结算优先级和效果分类。
    /// </summary>
    public enum CardSubType
    {
        /// <summary>伤害型：对目标造成数值伤害</summary>
        伤害型 = 1,

        /// <summary>功能型：增益/减益/控制等非直接伤害效果</summary>
        功能型 = 2,

        /// <summary>反制型：抵消或反弹对手卡牌效果，结算优先级最高</summary>
        反制型 = 3,

        /// <summary>防御型：提供护盾或减伤</summary>
        防御型 = 4,
    }
}
