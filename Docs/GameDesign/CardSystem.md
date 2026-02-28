# 卡牌系统详解

**文档版本**：V2.0  
**最后更新**：2026-02-28  
**前置阅读**：[Overview.md](Overview.md)  
**阅读时间**：8 分钟

---

## 🏗️ 卡牌分类体系

### 主分类：双轨体系

| CardTrackType | 枚举值 | 核心定位 |
|---------------|--------|----------|
| **瞬策牌** | 1 | 立即生效，卡组循环为主 |
| **定策牌** | 2 | 回合末结算，对抗效果为主 |

### 标签系统：CardTag（Flags 枚举）

使用 **位标志(Flags)** 设计，一张卡可以拥有多个标签：

```csharp
[Flags]
public enum CardTag
{
    None       = 0,
    Draw       = 1 << 0,   // 抽牌
    Damage     = 1 << 1,   // 伤害
    Defense    = 1 << 2,   // 防御
    Control    = 1 << 3,   // 控制
    Counter    = 1 << 4,   // 反制
    Execute    = 1 << 5,   // 斩杀
    CrossLane  = 1 << 6,   // 跨路生效
    Cycle      = 1 << 7,   // 卡组循环（抽/弃/洗）
    Buff       = 1 << 8,   // 增益
    Debuff     = 1 << 9,   // 减益
    Legendary  = 1 << 10,  // 传说
}
```

**用途**：
- UI 筛选 → `if ((card.Tags & CardTag.Damage) != 0)`
- 反制判定 → "反制所有带 `Damage` 标签的卡牌"
- 词条显示 → 组合多个标签生成卡牌描述

---

## 🎯 效果类型与结算层级

`EffectType` 决定效果在哪个结算层执行，完整定义见 `Shared/Protocol/Enums/EffectType.cs`：

| 层级 | 包含的 EffectType | 说明 |
|------|-----------------|------|
| Layer 0 | `Counter(1)` | 反制效果，最高优先级 |
| Layer 1 | `Shield(2)` `Armor(3)` `AttackBuff(4)` `AttackDebuff(5)` `Reflect(6)` `DamageReduction(7)` `Invincible(8)` | 防御/数值修正 |
| Layer 2 | `Damage(10)` `Lifesteal(11)` `Thorns(12)` `ArmorOnHit(13)` `Pierce(14)` | 伤害 + 触发效果 |
| Layer 3 | `Heal(20)` `Stun(21)` `Vulnerable(22)` `Weak(23)` `Draw(24)` `Discard(25)` `GainEnergy(26)` `Silence(27)` `Slow(28)` `DoubleStrength(29)` | 功能效果 |

> 完整 EffectType 说明及 Handler 映射见 [../TechGuide/BattleCore.md](../TechGuide/BattleCore.md)。

---

## 📋 卡牌配置结构

### 运行时卡牌数据来源

```
Config/Excel/Cards.csv      ← 策划编辑
Config/Excel/Effects.csv    ← 策划编辑
       ↓ Tools/ExcelConverter/convert.bat
Client/Assets/StreamingAssets/Config/cards.json   ← 游戏读取
Client/Assets/StreamingAssets/Config/effects.json ← 游戏读取
```

> 详细配置流程见 [../TechGuide/ConfigSystem.md](../TechGuide/ConfigSystem.md)。

### CardConfig（代码模型，位于 Shared/ConfigModels/）

```csharp
public class CardConfig
{
    public int CardId { get; set; }              // 数字 ID：1xxx=瞬策，2xxx=定策
    public string CardName { get; set; }         // 显示名称
    public CardTrackType TrackType { get; set; } // Instant / Plan
    public List<string> Tags { get; set; }       // 标签列表（JSON 存字符串，运行时解析）
    public int EnergyCost { get; set; }          // 能量消耗
    public int Rarity { get; set; }              // 稀有度 1/2/3
    public List<CardEffect> Effects { get; set; } // 效果列表（通过 effectIds 关联）
}

public class CardEffect
{
    public string EffectId { get; set; }         // 如 "E2001-1"
    public EffectType EffectType { get; set; }   // 对应枚举整数值
    public int Value { get; set; }               // 数值（伤害/护盾等）
    public int Duration { get; set; }            // 持续回合（0=即时）
    public string TargetOverride { get; set; }   // 覆盖目标（如 "Self"）
    public string TriggerCondition { get; set; } // 触发条件（反制牌筛选）
    public bool IsDelayed { get; set; }          // 是否延迟生效
}
```

---

## 🃏 瞬策牌设计规范

### 核心特性

| 特性 | 规则 |
|------|------|
| **生效时机** | 打出后立即生效，不进入结算栈 |
| **作用范围** | 主要针对自身（手牌、牌库、能量），部分可造成直伤 |
| **使用限制** | 仅在操作窗口期可打出 |
| **信息可见性** | 敌方不可见（使用动画可见，效果不可见） |

### 常用效果类型

| EffectType | ID | 说明 |
|------------|-----|------|
| `Draw` | 24 | 抽牌 |
| `Discard` | 25 | 弃牌 |
| `GainEnergy` | 26 | 能量回复 |
| `Silence` | 27 | 沉默（针对自身：禁止抽牌等） |
| `DoubleStrength` | 29 | 力量翻倍（消耗型增益） |

### 示例卡牌（当前套牌）

```json
// 战斗专注：抽3张牌，本回合不能再抽牌
{
  "cardId": 1001,
  "cardName": "战斗专注",
  "trackType": "Instant",
  "targetType": "Self",
  "energyCost": 0,
  "effectIds": ["E1001-1", "E1001-2"]
}

// 突破极限：力量翻倍（消耗型）
{
  "cardId": 1002,
  "cardName": "突破极限",
  "trackType": "Instant",
  "targetType": "Self",
  "energyCost": 1,
  "effectIds": ["E1002-1"]
}
```

---

## ⚔️ 定策牌设计规范

### 核心特性

| 特性 | 规则 |
|------|------|
| **生效时机** | 回合末统一结算 |
| **作用范围** | 敌方/场面，可带跨路标签 |
| **提交机制** | 操作窗口期内可提交/修改/取消 |
| **信息可见性** | 提交动画可见，内容不可见，结算时公开 |

### 允许的效果类型

所有 Layer 0-3 的效果类型均可用于定策牌。

### 跨路规则

仅带 `CrossLane` 标签的定策牌可以选择跨路目标：

```csharp
// 校验跨路合法性
bool CanTargetCrossLane(CardConfig card)
{
    return (card.Tags & CardTag.CrossLane) != 0 
        && card.TrackType == CardTrackType.定策牌;
}
```

### 示例卡牌

```json
{
  "CardId": "E2001",
  "CardName": "火球术",
  "TrackType": 2,
  "Tags": 2,  // Damage
  "EnergyCost": 3,
  "Effects": [
    {
      "EffectType": 300,
      "Value": 5,
      "TargetType": 1,
      "Description": "对单个敌人造成5点伤害"
    }
  ]
}
```

---

## 🔢 资源系统

### 能量（Energy）

| 规则 | 说明 |
|------|------|
| 初始能量 | 3 |
| 回合回复 | 每回合开始时回满至上限 |
| 能量上限 | 5（可通过卡牌临时增加） |
| 消耗规则 | 打出卡牌时立即扣除 |

### 抽牌规则

| 规则 | 说明 |
|------|------|
| 初始手牌 | 5 张 |
| 回合抽牌 | 1 张 |
| 手牌上限 | 10 张（超出则强制弃牌） |
| 牌库循环 | 牌库耗尽时弃牌堆洗入牌库 |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 结算规则详解 | [SettlementRules.md](SettlementRules.md) |
| 核心代码解读 | [../TechGuide/BattleCore.md](../TechGuide/BattleCore.md) |
| 配置系统说明 | [../TechGuide/ConfigSystem.md](../TechGuide/ConfigSystem.md) |
| 枚举定义 | `Shared/Protocol/Enums/` |