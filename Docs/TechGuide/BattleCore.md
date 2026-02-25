# BattleCore 核心代码解读

**文档版本**：V3.1  
**最后更新**：2025-02-25  
**适用对象**：需要理解或修改结算逻辑的开发者  
**前置阅读**：[QuickStart.md](QuickStart.md)、[../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)  
**阅读时间**：20 分钟

---

## 🎯 BattleCore 定位

| 特性 | 说明 |
|------|------|
| **位置** | `Shared/BattleCore/` |
| **职责** | 所有战斗结算逻辑的唯一实现 |
| **约束** | `noEngineReferences: true` — 禁止使用 UnityEngine |
| **消费者** | Unity Client、.NET Server 共同引用 |

### 核心原则

```
┌─────────────────────────────────────────────────────────┐
│             确定性 + 无状态 + 模块化                      │
├─────────────────────────────────────────────────────────┤
│  • 相同输入 → 相同输出（可复现）                         │
│  • 所有状态存储在 BattleContext 中                      │
│  • 效果处理通过 Handler 模块化，可独立测试               │
│  • 随机使用 SeededRandom，种子由服务端下发               │
│  • PlayerId 统一使用 string 类型（服务端兼容）           │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 目录结构（V3.1）

```
Shared/BattleCore/
├── Buff/                       # Buff 系统
│   ├── BuffConfig.cs           # Buff 配置
│   ├── BuffInstance.cs         # Buff 实例
│   ├── BuffManager.cs          # Buff 管理器
│   └── BuffType.cs             # Buff 类型枚举
│
├── Context/                    # 战斗上下文（所有可变状态）
│   ├── BattleContext.cs        # 主上下文
│   ├── PlayerBattleState.cs    # 玩家战斗状态
│   ├── LaneState.cs            # 分路状态
│   └── PlayedCard.cs           # ★ 已打出的卡牌（结算专用）
│
├── Event/                      # 战斗事件
│   └── BattleEvent.cs          # 事件记录
│
├── Settlement/                 # 结算引擎
│   ├── SettlementEngine.cs     # 主结算引擎 V3.0
│   ├── TargetResolver.cs       # 目标解析器
│   ├── DamageHelper.cs         # 伤害计算辅助
│   │
│   └── Handlers/               # ★ 模块化效果处理器
│       ├── IEffectHandler.cs   # Handler 接口
│       ├── HandlerRegistry.cs  # Handler 注册中心
│       ├── CommonHandlers.cs   # 通用处理器集合
│       ├── DamageHandler.cs    # 伤害处理
│       ├── HealHandler.cs      # 治疗处理
│       ├── ShieldHandler.cs    # 护盾处理
│       ├── StunHandler.cs      # 眩晕处理
│       └── CounterHandler.cs   # 反制处理
│
├── Random/                     # 确定性随机
│   └── SeededRandom.cs         # 种子随机数生成器
│
├── RoundStateMachine/          # 回合状态机
│   └── RoundManager.cs         # 回合管理器
│
└── Trigger/                    # 触发系统
    ├── TriggerManager.cs       # 触发管理器
    └── TriggerTypes.cs         # 触发时机枚举
```

---

## 🔧 EffectType 枚举体系（V3.1）

### V3.0 核心类型（推荐使用）

| 枚举值 | 名称 | 层级 | 说明 |
|--------|------|------|------|
| `Counter = 1` | 反制 | Layer 0 | 反制敌方卡牌 |
| `Damage = 2` | 伤害 | Layer 2 | 造成伤害 |
| `Shield = 3` | 护盾 | Layer 1 | 获得护盾 |
| `Heal = 4` | 治疗 | Layer 3 | 恢复生命 |
| `Stun = 5` | 眩晕 | Layer 3 | 眩晕控制 |
| `Armor = 6` | 护甲 | Layer 1 | 获得护甲 |
| `AttackBuff = 7` | 攻击强化 | Layer 1 | 增加攻击力 |
| `Reflect = 8` | 反伤 | Layer 1 | 反弹伤害 |
| `Vulnerable = 9` | 易伤 | Layer 3 | 受伤增加 |
| `Draw = 10` | 抽牌 | Layer 3 | 抽取卡牌 |

### 旧版兼容类型（100+）

| 范围 | 层级 | 说明 |
|------|------|------|
| 100-199 | Layer 1 | 防御/修正（GainArmor, GainShield, Weak 等） |
| 200-299 | Layer 2 | 伤害/触发（DealDamage, Lifesteal, Thorns 等） |
| 300-399 | Layer 3 | 功能（GainEnergy, Silence, Discard 等） |
| 400-499 | Layer 0 | 反制（CounterCard, CounterFirstDamage 等） |

### 层级判断方法

```csharp
/// <summary>
/// 获取效果所属结算层（V3.0 四层架构）
/// </summary>
public int GetSettlementLayerV3()
{
    return EffectType switch
    {
        // V3.0 核心类型
        EffectType.Counter => 0,
        EffectType.Shield or EffectType.Armor or EffectType.AttackBuff or EffectType.Reflect => 1,
        EffectType.Damage => 2,
        EffectType.Heal or EffectType.Stun or EffectType.Vulnerable or EffectType.Draw => 3,
        
        // 旧版兼容：根据编号范围判断
        _ => GetLegacyLayer()
    };
}
```

---

## 🏗️ Handler 注册表（V3.1 完整列表）

| Handler | 支持的 EffectType |
|---------|------------------|
| `CounterHandler` | Counter(1), CounterCard(401), CounterFirstDamage(402), CounterAndReflect(403) |
| `ShieldHandler` | Shield(3), GainShield(102) |
| `ArmorHandler` | Armor(6), GainArmor(101) |
| `StrengthHandler` | AttackBuff(7), GainStrength(111), ReduceStrength(112) |
| `VulnerableHandler` | Vulnerable(9) |
| `WeakHandler` | Weak(116) |
| `DamageReductionHandler` | DamageReduction(103) |
| `InvincibleHandler` | Invincible(104) |
| `ThornsHandler` | Reflect(8), Thorns(211) |
| `DamageHandler` | Damage(2), DealDamage(201) |
| `LifestealHandler` | Lifesteal(212) |
| `HealHandler` | Heal(4) |
| `StunHandler` | Stun(5) |
| `SilenceHandler` | Silence(311) |
| `DrawHandler` | Draw(10) |
| `EnergyHandler` | GainEnergy(303) |

### 注册示例（HandlerRegistry.cs）

```csharp
public static void Initialize()
{
    // V3.0 核心类型
    Register(EffectType.Counter, new CounterHandler());
    Register(EffectType.Damage, new DamageHandler());
    Register(EffectType.Shield, new ShieldHandler());
    Register(EffectType.Heal, new HealHandler());
    Register(EffectType.Stun, new StunHandler());
    
    // 旧版兼容映射
    Register(EffectType.DealDamage, new DamageHandler());      // 201 → 2
    Register(EffectType.GainShield, new ShieldHandler());      // 102 → 3
    Register(EffectType.CounterCard, new CounterHandler());    // 401 → 1
    // ...
}
```

---

## 🔄 结算引擎执行流程（V3.1）

### 瞬策牌结算（即时）

```csharp
public bool ResolveInstantCard(BattleContext ctx, PlayedCard card)
{
    // 1. 解析目标
    card.ResolvedTargets = _targetResolver.Resolve(card, ctx);
    
    // 2. 触发 BeforePlayCard（可被反制）
    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.BeforePlayCard, ...);
    
    // 3. 检查是否被反制
    if (cancelled) return false;
    
    // 4. 执行所有效果（通过 Handler）
    foreach (var effect in card.Config.Effects)
    {
        ExecuteEffect(ctx, card, effect, source);
    }
    
    // 5. 触发 AfterPlayCard
    ctx.TriggerManager.FireTriggers(ctx, TriggerTiming.AfterPlayCard, ...);
    
    return true;
}
```

### 定策牌结算（回合结束统一）

```csharp
public void ResolvePlanCards(BattleContext ctx)
{
    // 1. 预处理：为所有卡牌解析目标
    PreResolveTargets(ctx);
    
    // 2. Layer 0: 反制效果
    ResolveLayer0_Counter(ctx);
    
    // 3. Layer 1: 防御与数值修正
    ResolveLayer1_Defense(ctx);
    
    // 4. Layer 2: 伤害与触发
    ResolveLayer2_Damage(ctx);
    
    // 5. Layer 3: 功能效果
    ResolveLayer3_Utility(ctx);
}
```

### 执行流程图

```
┌───────────────────────────────────────────────────────────────┐
│                    ResolvePlanCards                           │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  PreResolveTargets                                            │
│  └─ 为每张牌调用 TargetResolver.Resolve()                      │
│                     │                                         │
│                     ▼                                         │
│  Layer 0: ResolveLayer0_Counter                               │
│  ├─ 处理上回合反制牌                                           │
│  ├─ 标记被反制的卡牌                                           │
│  └─ 输出 ValidPlanCards（未被反制）                            │
│                     │                                         │
│                     ▼                                         │
│  Layer 1: ResolveLayer1_Defense                               │
│  └─ 收集并同步应用所有 Layer1 效果                              │
│     （护甲、护盾、力量、反伤等）                                │
│                     │                                         │
│                     ▼                                         │
│  Layer 2: ResolveLayer2_Damage                                │
│  ├─ Step1: 同步收集并应用所有伤害                              │
│  │   └─ 护盾优先吸收 → 扣血 → 标记濒死                         │
│  └─ Step2: 处理触发效果（吸血、反伤等）                        │
│                     │                                         │
│                     ▼                                         │
│  Layer 3: ResolveLayer3_Utility                               │
│  ├─ SubPhase1: 普通效果（治疗、眩晕、抽牌等）                   │
│  └─ SubPhase2: 传说特殊牌                                      │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```

---

## 📦 关键数据结构

### PlayedCard（已打出卡牌）

```csharp
public class PlayedCard
{
    // ── 卡牌标识 ──
    public string RuntimeId { get; set; }           // 运行时唯一ID
    public CardConfig Config { get; set; }          // 关联配置
    
    // ── 施法者与目标 ──
    public string SourcePlayerId { get; set; }      // 施法者ID（string）
    public List<string> RawTargetGroup { get; set; } // 玩家选择的原始目标
    public List<string> ResolvedTargets { get; set; } // 解析后的实际目标
    
    // ── 结算状态 ──
    public bool IsCountered { get; set; }           // 是否被反制
}
```

### BattleContext（战斗上下文）

```csharp
public class BattleContext
{
    // ── 玩家状态 ──
    public List<PlayerBattleState> Players { get; set; }
    
    // ── 待结算卡牌 ──
    public List<PlayedCard> PendingPlanCards { get; set; }      // 本回合定策牌
    public List<PlayedCard> PendingCounterCards { get; set; }   // 上回合反制牌
    public List<PlayedCard> ValidPlanCards { get; set; }        // 有效卡牌（未被反制）
    public List<PlayedCard> CounteredCards { get; set; }        // 被反制的卡牌
    
    // ── 触发效果 ──
    public List<PendingTriggerEffect> PendingTriggerEffects { get; set; }
    public bool HasChainTriggeredThisRound { get; set; }        // 连锁封顶标记
    
    // ── 日志 ──
    public List<string> RoundLog { get; set; }
}
```

---

## ⚠️ 开发注意事项

### 禁止事项（CRITICAL）

| ❌ 禁止 | 原因 |
|--------|------|
| 使用 `UnityEngine.*` | asmdef 限制 + 服务端兼容性 |
| 使用 `System.Random` 或 `UnityEngine.Random` | 必须用 `SeededRandom` 保证确定性 |
| 使用浮点数运算 | 精度问题导致 C/S 不一致 |
| 在 Handler 中保存私有状态 | 破坏无状态原则 |
| 使用旧版 `GetSettlementLayer()` | 已废弃，使用 `GetSettlementLayerV3()` |

### 必须遵守

| ✅ 必须 | 原因 |
|--------|------|
| 新效果使用 V3.0 核心类型（1-10） | 统一规范 |
| 所有 Handler 必须注册到 Registry | 可发现性 |
| Handler.Execute() 必须处理 null target | 自身增益效果 |
| 使用 `#pragma warning disable CS8632` | Unity C# 版本兼容 |

### 添加新效果的步骤

```
1. 在 Protocol/Enums/EffectType.cs 添加新枚举值（优先使用 1-10 范围）
2. 在 Handlers/ 创建 XxxHandler.cs 实现 IEffectHandler
3. 在 HandlerRegistry.Initialize() 中注册
4. 更新 effects.json 配置
5. 运行游戏验证
```

---

## 📝 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| V3.1 | 2025-02-25 | 清理旧版API；统一EffectType体系；完善Handler列表 |
| V3.0 | 2024-01 | Handler 模块化重构；PlayerId 改为 string；新增 PlayedCard |
| V2.0 | 2024-01 | 引入 4 层结算栈；TargetResolver 分离 |
| V1.0 | 初始 | 基础结算引擎 |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 结算规则设计 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 卡牌系统 | [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md) |
| 快速入门 | [QuickStart.md](QuickStart.md) |
| 代码位置 | `Shared/BattleCore/Settlement/` |