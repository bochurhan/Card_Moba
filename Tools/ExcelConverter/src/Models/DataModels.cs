namespace CardMoba.Tools.ExcelConverter.Models;

/// <summary>
/// 转换结果
/// </summary>
public class ConvertResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }
    
    /// <summary>转换的卡牌数量</summary>
    public int CardCount { get; set; }
    
    /// <summary>转换的效果数量</summary>
    public int EffectCount { get; set; }
    
    /// <summary>错误信息列表</summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>警告信息列表</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 卡牌 JSON 数据结构（用于序列化输出）
/// </summary>
public class CardJsonOutput
{
    public int cardId { get; set; }
    public string cardName { get; set; } = "";
    public string description { get; set; } = "";
    public string trackType { get; set; } = "";
    public string targetType { get; set; } = "";
    public List<string> tags { get; set; } = new();
    public int energyCost { get; set; }
    public int rarity { get; set; }
    public int duration { get; set; }
    public List<string> effectIds { get; set; } = new();
}

/// <summary>
/// 卡牌集合 JSON 根结构
/// </summary>
public class CardsJsonRoot
{
    public string version { get; set; } = "1.0.0";
    public List<CardJsonOutput> cards { get; set; } = new();
}

/// <summary>
/// 效果 JSON 数据结构
/// </summary>
public class EffectJsonOutput
{
    public string effectId { get; set; } = "";
    public int effectType { get; set; }
    public int value { get; set; }
    public int duration { get; set; }
    public string? targetOverride { get; set; }
    public bool? isDelayed { get; set; }
    public string description { get; set; } = "";
}

/// <summary>
/// 效果集合 JSON 根结构
/// </summary>
public class EffectsJsonRoot
{
    public string version { get; set; } = "1.0.0";
    public List<EffectJsonOutput> effects { get; set; } = new();
}

/// <summary>
/// CSV 行原始数据（卡牌表）
/// </summary>
public class CardCsvRow
{
    public int CardId { get; set; }
    public string CardName { get; set; } = "";
    public string Description { get; set; } = "";
    public string TrackType { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string Tags { get; set; } = "";       // 可能是 "Damage|Defense"
    public int EnergyCost { get; set; }
    public int Rarity { get; set; }
    public string EffectsRef { get; set; } = ""; // 可能是 "E2005-1|E2005-2"
}

/// <summary>
/// CSV 行原始数据（效果表）
/// </summary>
public class EffectCsvRow
{
    public string EffectId { get; set; } = "";
    public int CardId { get; set; }
    public string EffectType { get; set; } = "";
    public int Value { get; set; }
    public int Duration { get; set; }
    public string? TargetOverride { get; set; }
    public string? TriggerCondition { get; set; }
    public bool IsDelayed { get; set; }
}
