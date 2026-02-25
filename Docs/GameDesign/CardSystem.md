# 卡牌系统详解

**文档版本**：V1.0  
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

`EffectType` 决定效果在哪个结算层执行：

| 层级 | EffectType 范围 | 说明 |
|------|-----------------|------|
| Layer 0 | 100-199 | 反制效果（无效化、惩罚） |
| Layer 1 | 200-299 | 防御/数值修正（护盾、护甲、攻击修正） |
| Layer 2 | 300-399 | 伤害 + 触发效果 |
| Layer 3 | 400-499 | 功能效果（控制、资源操作、传说） |

### 当前已定义的 EffectType

```csharp
public enum EffectType
{
    // ── Layer 0: 反制（100-199）──
    Counter_Nullify = 100,     // 无效化目标卡牌
    Counter_Punish  = 101,     // 无效化并惩罚
    
    // ── Layer 1: 防御/修正（200-299）──
    Defense_Shield  = 200,     // 护盾
    Defense_Armor   = 201,     // 护甲加成
    Modify_Attack   = 210,     // 攻击力修正
    Modify_Energy   = 220,     // 能量修正
    
    // ── Layer 2: 伤害（300-399）──
    Damage_Direct   = 300,     // 直接伤害
    Damage_Area     = 301,     // 范围伤害
    Damage_Reflect  = 320,     // 反伤（触发型）
    Damage_Lifesteal= 321,     // 吸血（触发型）
    
    // ── Layer 3: 功能（400-499）──
    Utility_Stun    = 400,     // 晕眩控制
    Utility_Silence = 401,     // 沉默
    Utility_Draw    = 410,     // 抽牌
    Utility_Discard = 411,     // 弃牌
    Utility_EnergyGain = 420,  // 能量回复
    Legendary_Special = 450,   // 传说牌特殊效果
}
```

---

## 📋 卡牌配置结构

### CardConfig（配置模型）

```csharp
public class CardConfig
{
    public string CardId { get; set; }          // "E1001" 格式
    public string CardName { get; set; }        // "火球术"
    public CardTrackType TrackType { get; set; } // 瞬策牌/定策牌
    public CardTag Tags { get; set; }           // 标签组合
    public int EnergyCost { get; set; }         // 能量消耗
    public List<CardEffect> Effects { get; set; } // 效果列表
}

public class CardEffect
{
    public EffectType EffectType { get; set; }  // 效果类型
    public int Value { get; set; }              // 数值（伤害/护盾等）
    public TargetType TargetType { get; set; }  // 目标类型
    public string Description { get; set; }     // 效果描述
}

public enum TargetType
{
    Self = 0,           // 自身
    SingleEnemy = 1,    // 单个敌人
    AllEnemies = 2,     // 所有敌人
    SingleAlly = 3,     // 单个友方
    AllAllies = 4,      // 所有友方
    LaneEnemies = 5,    // 本路敌人
    CrossLaneTarget = 6 // 跨路目标（需要 CrossLane 标签）
}
```

---

## 🃏 瞬策牌设计规范

### 核心特性

| 特性 | 规则 |
|------|------|
| **生效时机** | 打出后立即生效，不进入结算栈 |
| **作用范围** | 仅限自身（手牌、牌库、能量） |
| **使用限制** | 仅在操作窗口期可打出 |
| **信息可见性** | 敌方不可见（使用动画可见，效果不可见） |

### 允许的效果类型

| EffectType | 说明 |
|------------|------|
| `Utility_Draw` | 抽牌 |
| `Utility_Discard` | 弃牌 |
| `Utility_EnergyGain` | 能量回复 |
| `Modify_Energy` | 能量修正 |

### 禁止的效果

- ❌ 伤害敌方
- ❌ 控制敌方
- ❌ 跨路作用

### 示例卡牌

```json
{
  "CardId": "E1001",
  "CardName": "急速抽牌",
  "TrackType": 1,
  "Tags": 129,  // Draw | Cycle
  "EnergyCost": 1,
  "Effects": [
    {
      "EffectType": 410,
      "Value": 2,
      "TargetType": 0,
      "Description": "抽2张牌"
    }
  ]
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
| 枚举定义 | `Shared/Protocol/Enums/` |
