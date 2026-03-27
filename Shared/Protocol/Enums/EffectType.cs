namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// 卡牌效果类型。
    /// 数值 ID 一旦分配就不再修改，新增效果只能向后追加。
    /// </summary>
    public enum EffectType
    {
        None = 0,

        // Layer 0: Counter
        Counter = 1,

        // Layer 1: Defense / modifiers
        Shield = 2,
        Armor = 3,
        AttackBuff = 4,
        AttackDebuff = 5,
        Reflect = 6,
        DamageReduction = 7,
        Invincible = 8,

        // Layer 2: Damage
        Damage = 10,
        Lifesteal = 11,
        Thorns = 12,
        ArmorOnHit = 13,
        Pierce = 14,

        // Layer 4 in current runtime, historical IDs retained
        Heal = 20,
        Stun = 21,
        Vulnerable = 22,
        Weak = 23,
        Draw = 24,
        Discard = 25,
        GainEnergy = 26,
        Silence = 27,
        Slow = 28,
        DoubleStrength = 29,
        BanDraw = 30,

        // Current V2 additions
        AddBuff = 31,
        GenerateCard = 32,
        DOT = 33,
        ReturnSourceCardToHandAtRoundEnd = 34,
        UpgradeCardsInHand = 35,
        MoveSelectedCardToDeckTop = 36,
    }
}
