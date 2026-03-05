namespace CardMoba.Tools.ExcelConverter.Converters;

/// <summary>
/// 效果类型映射 —— 将 Excel/CSV 中的文本类型映射为 EffectType 枚举整数值。
///
/// 重要：此处的数值必须与 Shared/Protocol/Enums/EffectType.cs 中的枚举值完全一致，
/// 因为 CardConfigManager 直接执行 (EffectType)data.effectType 强转。
///
/// 当前 EffectType 枚举对照：
///   Counter         = 1   (Layer 0 反制层)
///   Shield          = 2   (Layer 1 防御层)
///   Armor           = 3
///   AttackBuff      = 4
///   AttackDebuff    = 5
///   Reflect         = 6
///   DamageReduction = 7
///   Invincible      = 8
///   Damage          = 10  (Layer 2 伤害层)
///   Lifesteal       = 11
///   Thorns          = 12
///   ArmorOnHit      = 13
///   Pierce          = 14
///   Heal            = 20  (Layer 3 功能层)
///   Stun            = 21
///   Vulnerable      = 22
///   Weak            = 23
///   Draw            = 24
///   Discard         = 25
///   GainEnergy      = 26
///   Silence         = 27
///   Slow            = 28
///   DoubleStrength  = 29
///   BanDraw         = 30
/// </summary>
public static class EffectTypeMapper
{
    private static readonly Dictionary<string, int> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // ═══ Layer 0 — 反制层 ═══
        { "Counter",            1  },   // 反制

        // ═══ Layer 1 — 防御与数值修正层 ═══
        { "Shield",             2  },   // 护盾
        { "GainShield",         2  },   // 护盾（别名）
        { "Armor",              3  },   // 护甲
        { "GainArmor",          3  },   // 护甲（别名）
        { "AttackBuff",         4  },   // 力量增益
        { "GainStrength",       4  },   // 力量增益（别名）
        { "AttackDebuff",       5  },   // 力量削减
        { "Reflect",            6  },   // 反伤
        { "Thorns",             6  },   // 反伤（别名，注意：Thorns 枚举值=12，此别名指 Reflect）
        { "DamageReduction",    7  },   // 伤害减免
        { "Invincible",         8  },   // 无敌

        // ═══ Layer 2 — 主动伤害与触发式效果层 ═══
        { "Damage",             10 },   // 造成伤害
        { "DealDamage",         10 },   // 造成伤害（旧别名，保持向后兼容）
        { "Lifesteal",          11 },   // 吸血
        { "ThornsEffect",       12 },   // 荆棘（受到攻击后反伤，Layer 2）
        { "ArmorOnHit",         13 },   // 受击获甲
        { "Pierce",             14 },   // 穿透

        // ═══ Layer 3 — 全局功能收尾层 ═══
        { "Heal",               20 },   // 治疗
        { "Stun",               21 },   // 眩晕
        { "Vulnerable",         22 },   // 易伤
        { "Weak",               23 },   // 虚弱
        { "Draw",               24 },   // 抽牌
        { "DrawCard",           24 },   // 抽牌（别名）
        { "Discard",            25 },   // 弃牌
        { "GainEnergy",         26 },   // 回复能量
        { "Silence",            27 },   // 沉默
        { "Slow",               28 },   // 减速
        { "DoubleStrength",     29 },   // 力量翻倍
        { "BanDraw",            30 },   // 禁止抽牌
    };

    /// <summary>
    /// 将文本效果类型转换为 EffectType 枚举整数值。
    /// 未找到时返回 0（对应 EffectType.None）。
    /// </summary>
    /// <param name="effectTypeName">效果类型名称（如 "Damage"、"Shield"）</param>
    public static int GetTypeCode(string effectTypeName)
    {
        if (string.IsNullOrWhiteSpace(effectTypeName))
            return 0;

        if (TypeMap.TryGetValue(effectTypeName.Trim(), out int code))
            return code;

        // 未识别时打印警告，返回 0
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  [警告] 未知效果类型: \"{effectTypeName.Trim()}\"，将输出 0 (None)");
        Console.ResetColor();
        return 0;
    }

    /// <summary>
    /// 根据效果类型和数值生成可读描述文本
    /// </summary>
    public static string GenerateDescription(string effectType, int value, int duration)
    {
        string baseDesc = effectType.Trim().ToLowerInvariant() switch
        {
            "damage" or "dealdamage"    => $"造成 {value} 点伤害",
            "shield" or "gainshield"    => $"获得 {value} 点护盾",
            "armor" or "gainarmor"      => $"获得 {value} 点护甲",
            "attackbuff" or "gainstrength" => $"获得 {value} 点力量",
            "attackdebuff"              => $"降低目标 {value} 点力量",
            "reflect"                   => $"反伤 {value}%",
            "thornseffect"              => $"受击后对攻击者造成 {value} 点伤害",
            "damagereduction"           => $"减少受到伤害的 {value}%",
            "invincible"                => $"本回合免疫伤害",
            "lifesteal"                 => $"吸血 {value}%",
            "armoronhit"                => $"受击时获得 {value} 点护甲",
            "pierce"                    => $"穿透护甲，造成 {value} 点伤害",
            "heal"                      => $"回复 {value} 点生命",
            "stun"                      => $"眩晕目标 {value} 回合",
            "vulnerable"                => $"使目标易伤 {value}%",
            "weak"                      => $"使目标虚弱 {value}%",
            "draw" or "drawcard"        => $"抽 {value} 张牌",
            "discard"                   => $"弃置 {value} 张牌",
            "gainenergy"                => $"回复 {value} 点能量",
            "silence"                   => $"沉默目标",
            "slow"                      => $"使目标减速",
            "doublestrength"            => $"你的力量翻倍",
            "counter"                   => $"反制敌方首张伤害牌",
            _                           => $"{effectType}: {value}",
        };

        if (duration > 0)
        {
            baseDesc += $"，持续 {duration} 回合";
        }

        return baseDesc;
    }
}