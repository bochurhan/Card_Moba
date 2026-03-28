# 中枢塔系统详解

**文档版本**：V1.0  
**前置阅读**：[Overview.md](Overview.md)、[LaneSystem.md](LaneSystem.md)  
**阅读时间**：7 分钟

---

## 🎯 设计目标

| 目标 | 实现方式 |
|------|----------|
| **节奏调节** | 分路对线后插入 PVE 阶段，缓解对抗疲劳 |
| **资源争夺** | 击杀小怪/BOSS 获取奖励，拉开发育差距 |
| **阵营对抗** | BOSS 战双方同时参与，争夺最终击杀权 |
| **战略选择** | 选择打哪些怪、投入多少资源 |

---

## 🗺️ 中枢塔阶段概览

### 触发时机

```
第一分路对线期 → 第一中枢塔 → 第二分路对线期 → 第二中枢塔 → 决战
```

### 阶段结构

```
中枢塔阶段（每次约3-5分钟）
    │
    ├── 小怪清剿期（个人PVE）
    │   └── 各玩家独立挑战小怪，获取个人奖励
    │
    └── BOSS 讨伐期（阵营对抗）
        └── 双方阵营同时攻击 BOSS，争夺击杀权
```

---

## 👹 小怪清剿期

### 玩法流程

```
1. 进入中枢塔场景
2. 显示本轮可选小怪列表（3-5只）
3. 玩家选择挑战哪只/哪几只
4. 按顺序逐个战斗（使用同一套卡组）
5. 击杀获得奖励，失败无惩罚
6. 时间结束或全部击杀 → 进入 BOSS 期
```

### 小怪配置

| 等级 | 难度 | 回合上限 | 奖励类型 |
|------|------|----------|----------|
| **Lv1** | 简单 | 3 回合 | 金币、小量经验 |
| **Lv2** | 普通 | 4 回合 | 金币、经验、临时增益 |
| **Lv3** | 困难 | 5 回合 | 大量经验、稀有增益、换路次数 |

### 小怪 AI 行为

```csharp
/// <summary>
/// 小怪行为模式
/// </summary>
public enum MonsterBehavior
{
    /// <summary>固定模式：每回合固定出牌</summary>
    Fixed = 0,
    
    /// <summary>反应模式：根据玩家上回合行动调整</summary>
    Reactive = 1,
    
    /// <summary>随机模式：随机选择可用卡牌</summary>
    Random = 2,
}

/// <summary>
/// 小怪配置
/// </summary>
public class MonsterConfig
{
    public string MonsterId { get; set; }
    public string MonsterName { get; set; }
    public int Level { get; set; }
    public int MaxHp { get; set; }
    public int BaseAttack { get; set; }
    public MonsterBehavior Behavior { get; set; }
    public List<string> CardPool { get; set; }      // 可用卡牌ID列表
    public List<RewardConfig> Rewards { get; set; } // 击杀奖励
}
```

### 奖励系统

```csharp
/// <summary>
/// 奖励配置
/// </summary>
public class RewardConfig
{
    public RewardType Type { get; set; }
    public int Value { get; set; }
    public float DropRate { get; set; }  // 掉落概率
}

public enum RewardType
{
    Gold = 0,           // 金币（商店购买增益）
    Exp = 1,            // 经验（提升属性）
    TempBuff = 2,       // 临时增益（本局有效）
    LaneSwapToken = 3,  // 换路次数
    CardDraw = 4,       // 额外抽牌权
    EnergyBoost = 5,    // 能量上限提升
}
```

---

## 🐉 BOSS 讨伐期

### 玩法流程

```
1. 小怪期结束，全员进入 BOSS 战
2. BOSS 同时面对双方阵营（6人）
3. 每回合：所有玩家同时出牌 → BOSS 行动
4. BOSS HP 归零时，判定击杀方
5. 击杀方获得大量奖励，另一方获得参与奖励
```

### BOSS 特性

| 特性 | 说明 |
|------|------|
| **双目标** | BOSS 每回合可同时攻击两个阵营 |
| **技能预告** | 提前1回合显示下回合技能 |
| **阶段转换** | HP 降至阈值时切换行为模式 |
| **争夺机制** | 最后一击决定击杀归属 |

### BOSS 配置

```csharp
/// <summary>
/// BOSS 配置
/// </summary>
public class BossConfig
{
    public string BossId { get; set; }
    public string BossName { get; set; }
    public int MaxHp { get; set; }
    public int BaseAttack { get; set; }
    public List<BossPhase> Phases { get; set; }     // 阶段配置
    public List<BossSkill> Skills { get; set; }     // 技能池
}

/// <summary>
/// BOSS 阶段
/// </summary>
public class BossPhase
{
    public float HpThreshold { get; set; }  // HP 百分比阈值
    public string PhaseName { get; set; }
    public int AttackMultiplier { get; set; }  // 攻击力倍率(%)
    public List<string> AvailableSkills { get; set; }
}

/// <summary>
/// BOSS 技能
/// </summary>
public class BossSkill
{
    public string SkillId { get; set; }
    public string SkillName { get; set; }
    public BossSkillTarget Target { get; set; }
    public int Damage { get; set; }
    public string Description { get; set; }
}

public enum BossSkillTarget
{
    SingleRandom = 0,   // 随机单体
    TeamAAll = 1,       // A阵营全体
    TeamBAll = 2,       // B阵营全体
    AllPlayers = 3,     // 所有玩家
    HighestHp = 4,      // 当前HP最高者
    LowestHp = 5,       // 当前HP最低者
}
```

### 击杀权判定

```
判定规则：
1. BOSS HP 归零时，计算双方本回合造成的伤害
2. 伤害高的阵营获得"击杀"
3. 伤害相同时，按累计总伤害判定

奖励分配：
- 击杀方：100% 击杀奖励
- 参与方：50% 参与奖励
- 未参与：无奖励
```

---

## 📊 两次中枢塔差异

| 维度 | 第一中枢塔 | 第二中枢塔 |
|------|------------|------------|
| **时机** | 对线期1结束后 | 对线期2结束后 |
| **小怪等级** | Lv1-2 | Lv2-3 |
| **BOSS 强度** | 基础 BOSS | 强化 BOSS（多阶段） |
| **奖励价值** | 中等 | 高（决战前最后补强） |
| **战略意义** | 建立优势 | 逆转/扩大优势 |

---

## 🛒 中枢塔商店（可选系统）

### 概念设计

```
触发时机：小怪期和 BOSS 期之间

商品类型：
- 临时增益（本局有效）
- 卡牌强化（指定卡+伤害/效果）
- 属性提升（HP/能量上限）
- 特殊道具（换路券、复活券）

货币来源：
- 小怪击杀金币
- BOSS 参与奖励
- 对线期表现奖励
```

---

## 🔄 状态流转

### 中枢塔阶段状态机

```csharp
public enum CentralTowerPhase
{
    /// <summary>准备阶段：展示本轮内容</summary>
    Preparing = 0,
    
    /// <summary>小怪选择：玩家选择挑战目标</summary>
    MonsterSelection = 1,
    
    /// <summary>小怪战斗：逐个战斗</summary>
    MonsterBattle = 2,
    
    /// <summary>商店阶段：可选购买</summary>
    Shop = 3,
    
    /// <summary>BOSS 准备：展示 BOSS 信息</summary>
    BossPrepare = 4,
    
    /// <summary>BOSS 战斗：阵营对抗</summary>
    BossBattle = 5,
    
    /// <summary>结算：分配奖励</summary>
    Settlement = 6,
    
    /// <summary>完成：返回分路</summary>
    Completed = 7,
}
```

### 完整流转图

```
┌────────────────────────────────────────────────────────────┐
│                      中枢塔阶段流程                         │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  Preparing → MonsterSelection → MonsterBattle              │
│                                      │                     │
│                                      ▼                     │
│                                   [Shop] (可选)             │
│                                      │                     │
│                                      ▼                     │
│                              BossPrepare → BossBattle      │
│                                               │            │
│                                               ▼            │
│                                          Settlement        │
│                                               │            │
│                                               ▼            │
│                                           Completed        │
│                                               │            │
│                                               ▼            │
│                                        返回分路对线         │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## 🎮 UI 表现建议

### 小怪选择界面

```
┌─────────────────────────────────────────┐
│           中枢塔 - 小怪清剿              │
├─────────────────────────────────────────┤
│                                         │
│  [Lv1 哥布林]  [Lv2 狼人]  [Lv3 石像鬼]  │
│   HP: 10        HP: 20      HP: 35     │
│   奖励: 金币    奖励: 增益   奖励: 换路   │
│   ○ 已选择      ○ 未选择    ○ 未选择     │
│                                         │
│  已选择: 1/3    剩余时间: 30s            │
│                                         │
│          [开始挑战]                      │
└─────────────────────────────────────────┘
```

### BOSS 战界面

```
┌─────────────────────────────────────────┐
│              BOSS - 巨龙                 │
│     HP: ████████████░░░░ 75%            │
│     下回合技能: 火焰吐息（全体伤害）       │
├─────────────────────────────────────────┤
│  A阵营累计伤害: 120    B阵营累计伤害: 95  │
├─────────────────────────────────────────┤
│  [A1]  [A2]  [A3]    [B1]  [B2]  [B3]   │
│  HP:15 HP:20 HP:18   HP:12 HP:22 HP:17  │
├─────────────────────────────────────────┤
│            [我的操作区域]                 │
│         手牌 / 能量 / 定策提交            │
└─────────────────────────────────────────┘
```

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 核心玩法 | [Overview.md](Overview.md) |
| 分路系统 | [LaneSystem.md](LaneSystem.md) |
| 结算规则 | [SettlementRules.md](SettlementRules.md) |
