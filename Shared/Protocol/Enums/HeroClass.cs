#pragma warning disable CS8632

namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 英雄职业枚举 —— 决定该卡牌可被哪类英雄装备。
    /// Universal 表示通用牌，所有职业均可使用。
    /// </summary>
    public enum HeroClass
    {
        /// <summary>通用牌，所有职业可用</summary>
        Universal = 0,

        /// <summary>战士 —— 擅长护甲与力量增益</summary>
        Warrior = 1,

        /// <summary>刺客 —— 擅长多段伤害与吸血</summary>
        Assassin = 2,

        /// <summary>法师 —— 擅长元素伤害与控制</summary>
        Mage = 3,

        /// <summary>辅助 —— 擅长治疗与增益队友</summary>
        Support = 4,

        /// <summary>坦克 —— 擅长护盾与反伤</summary>
        Tank = 5,
    }
}
