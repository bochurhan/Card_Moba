namespace CardMoba.Protocol.Enums
{
    /// <summary>
    /// Buff 类型枚举。
    /// </summary>
    public enum BuffType
    {
        Unknown = 0,

        // Numeric modifiers
        Armor = 101,
        Strength = 102,
        Shield = 103,
        Regeneration = 104,
        EnergyGain = 105,
        MaxHpBonus = 106,

        // Positive states
        Invincible = 201,
        Lifesteal = 202,
        Thorns = 203,
        DamageReduction = 204,
        DamageAmplify = 205,
        Evasion = 206,
        Block = 207,

        // Negative states
        Vulnerable = 301,
        Weak = 302,
        Poison = 303,
        Burn = 304,
        Bleed = 305,
        Curse = 306,

        // Control
        Stun = 401,
        Silence = 402,
        Disarm = 403,
        Freeze = 404,
        Charm = 405,
        Root = 406,

        // Special
        Mark = 501,
        Stealth = 502,
        Taunt = 503,
        SoulLink = 504,
        Resurrection = 505,
        NoDrawThisTurn = 506,
        NoDamageCardThisTurn = 507,
        DelayedVulnerableNextRound = 508,
        BloodRitual = 509,
        Corruption = 510,
    }

    /// <summary>
    /// Buff 叠加规则。
    /// </summary>
    public enum BuffStackRule
    {
        None = 0,
        RefreshDuration = 1,
        StackValue = 2,
        Independent = 3,
        KeepHighest = 4,
    }

    /// <summary>
    /// Buff 触发时机。
    /// </summary>
    public enum BuffTriggerTiming
    {
        None = 0,
        OnRoundStart = 1,
        OnRoundEnd = 2,
        OnDamageTaken = 3,
        OnDamageDealt = 4,
        OnCardPlayed = 5,
        OnCardDrawn = 6,
        OnNearDeath = 7,
        OnKill = 8,
        OnHealed = 9,
        OnBuffRemoved = 10,
    }
}
