namespace CardMoba.Tools.ExcelConverter.Converters;

/// <summary>
/// 效果类型映射 —— 将 Excel 中的文本类型映射为数值编码
/// 
/// 编码规则：
///   1xx = 防御类
///   2xx = 伤害类
///   3xx = 回复类
///   4xx = 反制类
///   5xx = Buff/Debuff类
/// </summary>
public static class EffectTypeMapper
{
    private static readonly Dictionary<string, int> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // ═══ 防御类 (1xx) ═══
        { "GainShield",    101 },   // 获得护盾
        { "GainArmor",     102 },   // 获得护甲
        { "GainStrength",  111 },   // 获得力量
        { "Thorns",        121 },   // 反伤
        
        // ═══ 伤害类 (2xx) ═══
        { "DealDamage",    201 },   // 造成伤害
        { "Vulnerable",    221 },   // 易伤（提高受到的伤害）
        
        // ═══ 回复类 (3xx) ═══
        { "Heal",          301 },   // 治疗
        { "Lifesteal",     302 },   // 吸血（百分比）
        
        // ═══ 反制类 (4xx) ═══
        { "CounterFirstDamage", 401 }, // 反制首张伤害牌
        { "Counter",            401 }, // 别名
        
        // ═══ 抽牌/资源类 (5xx) ═══
        { "DrawCard",      501 },   // 抽牌
        { "GainEnergy",    502 },   // 获得能量
    };

    /// <summary>
    /// 将文本效果类型转换为数值编码
    /// </summary>
    /// <param name="effectTypeName">效果类型名称（如 "DealDamage"）</param>
    /// <returns>数值编码，未找到返回 0</returns>
    public static int GetTypeCode(string effectTypeName)
    {
        if (string.IsNullOrWhiteSpace(effectTypeName))
            return 0;
            
        return TypeMap.TryGetValue(effectTypeName.Trim(), out int code) ? code : 0;
    }

    /// <summary>
    /// 根据效果类型生成描述文本
    /// </summary>
    public static string GenerateDescription(string effectType, int value, int duration)
    {
        string baseDesc = effectType.ToLowerInvariant() switch
        {
            "dealdamage"         => $"造成{value}点伤害",
            "gainshield"         => $"获得{value}点护盾",
            "gainarmor"          => $"获得{value}点护甲",
            "heal"               => $"回复{value}点生命",
            "lifesteal"          => $"吸血{value}%",
            "gainstrength"       => $"获得{value}点力量",
            "thorns"             => $"反伤{value}%",
            "vulnerable"         => $"易伤{value}%",
            "counterfirstdamage" => "反制敌方首张伤害牌",
            "counter"            => "反制敌方首张伤害牌",
            "drawcard"           => $"抽{value}张牌",
            "gainenergy"         => $"获得{value}点能量",
            _                    => $"{effectType}: {value}"
        };

        if (duration > 0)
        {
            baseDesc += $"，持续{duration}回合";
        }

        return baseDesc;
    }
}
