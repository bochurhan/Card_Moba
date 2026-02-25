# BattleCore 核心代码解读

**文档版本**：V1.0  
**适用对象**：需要理解或修改结算逻辑的开发者  
**前置阅读**：[QuickStart.md](QuickStart.md)、[../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md)  
**阅读时间**：15 分钟

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
│                   确定性 + 无状态                        │
├─────────────────────────────────────────────────────────┤
│  • 相同输入 → 相同输出（可复现）                         │
│  • 所有状态存储在 BattleContext 中                      │
│  • 引擎函数只读取/修改 Context，不持有私有状态           │
│  • 随机使用 SeededRandom，种子由服务端下发               │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 目录结构

```
Shared/BattleCore/
├── CardEffects/            # 卡牌效果组件与管线
│   ├── ICardEffect.cs      # 效果接口
│   ├── EffectPipeline.cs   # 效果执行管线
│   └── Effects/            # 具体效果实现
│       ├── DamageEffect.cs
│       ├── ShieldEffect.cs
│       └── ...
├── Context/                # 战斗上下文（所有可变状态）
│   ├── BattleContext.cs    # 主上下文
│   ├── PlayerState.cs      # 玩家状态
│   ├── LaneState.cs        # 分路状态
│   └── CardInstance.cs     # 卡牌实例
├── Random/                 # 确定性随机
│   └── SeededRandom.cs     # 种子随机数生成器
├── RoundStateMachine/      # 回合状态机
│   ├── RoundPhase.cs       # 阶段枚举
│   └── RoundStateMachine.cs# 状态机实现
└── Settlement/             # 结算引擎
    ├── SettlementEngine.cs # 主结算引擎
    ├── LayerExecutor.cs    # 分层执行器
    └── TargetResolver.cs   # 目标解析器
```

---

## 🏗️ 核心类详解

### 1. BattleContext（战斗上下文）

**所有战斗状态的唯一容器**，结算函数只能通过它读写状态。

```csharp
/// <summary>
/// 战斗上下文 - 存储一场战斗的所有可变状态
/// </summary>
public class BattleContext
{
    // ── 基础信息 ──
    /// <summary>战斗唯一ID</summary>
    public string BattleId { get; set; }
    
    /// <summary>随机种子（服务端下发）</summary>
    public int RandomSeed { get; set; }
    
    /// <summary>种子随机数生成器</summary>
    public SeededRandom Random { get; private set; }
    
    // ── 回合状态 ──
    /// <summary>当前回合数</summary>
    public int CurrentRound { get; set; }
    
    /// <summary>当前阶段</summary>
    public RoundPhase CurrentPhase { get; set; }
    
    // ── 玩家状态 ──
    /// <summary>所有玩家状态（playerId → PlayerState）</summary>
    public Dictionary<int, PlayerState> Players { get; set; }
    
    // ── 分路状态 ──
    /// <summary>所有分路状态</summary>
    public List<LaneState> Lanes { get; set; }
    
    // ── 本回合数据 ──
    /// <summary>本回合提交的定策牌</summary>
    public List<PlayedCard> PendingPlanCards { get; set; }
    
    /// <summary>本回合已执行的瞬策牌</summary>
    public List<PlayedCard> ExecutedInstantCards { get; set; }
    
    // ── 公共方法 ──
    public PlayerState GetPlayer(int playerId) => Players[playerId];
    public LaneState GetLane(int laneId) => Lanes[laneId];
    
    /// <summary>初始化随机数生成器</summary>
    public void InitRandom()
    {
        Random = new SeededRandom(RandomSeed);
    }
}
```

### 2. PlayerState（玩家状态）

```csharp
/// <summary>
/// 玩家战斗状态
/// </summary>
public class PlayerState
{
    // ── 身份 ──
    public int PlayerId { get; set; }
    public int TeamId { get; set; }        // 0 = A阵营, 1 = B阵营
    public int CurrentLaneId { get; set; } // 当前所在分路
    
    // ── 资源 ──
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    
    // ── 战斗修正 ──
    public int Shield { get; set; }        // 护盾值
    public int Armor { get; set; }         // 护甲（减伤百分比）
    public int AttackModifier { get; set; }// 攻击力修正
    
    // ── 状态标记 ──
    public bool IsDead { get; set; }
    public bool IsStunned { get; set; }    // 晕眩（跳过操作）
    public bool IsSilenced { get; set; }   // 沉默（禁用技能牌）
    
    // ── 卡牌 ──
    public List<CardInstance> Hand { get; set; }       // 手牌
    public List<CardInstance> Deck { get; set; }       // 牌库
    public List<CardInstance> DiscardPile { get; set; }// 弃牌堆
    
    // ── 方法 ──
    /// <summary>受到伤害（计算护甲减伤）</summary>
    public void TakeDamage(int rawDamage)
    {
        int reduced = rawDamage * (100 - Armor) / 100;
        int absorbed = Math.Min(Shield, reduced);
        Shield -= absorbed;
        CurrentHp -= (reduced - absorbed);
        
        if (CurrentHp <= 0)
        {
            CurrentHp = 0;
            // 濒死标记，等待濒死判定期处理
        }
    }
    
    /// <summary>抽牌</summary>
    public void DrawCards(int count, SeededRandom random)
    {
        for (int i = 0; i < count; i++)
        {
            if (Deck.Count == 0)
            {
                // 牌库耗尽，弃牌堆洗入牌库
                ShuffleDiscardIntoDeck(random);
            }
            
            if (Deck.Count > 0)
            {
                var card = Deck[0];
                Deck.RemoveAt(0);
                Hand.Add(card);
            }
        }
    }
    
    private void ShuffleDiscardIntoDeck(SeededRandom random)
    {
        Deck.AddRange(DiscardPile);
        DiscardPile.Clear();
        random.Shuffle(Deck);
    }
}
```

### 3. SettlementEngine（结算引擎）

**核心结算逻辑的入口**，按四层顺序执行定策牌结算。

```csharp
/// <summary>
/// 结算引擎 - 执行定策牌统一结算
/// </summary>
public class SettlementEngine
{
    private readonly LayerExecutor _layerExecutor;
    private readonly TargetResolver _targetResolver;
    
    public SettlementEngine()
    {
        _layerExecutor = new LayerExecutor();
        _targetResolver = new TargetResolver();
    }
    
    /// <summary>
    /// 执行定策牌统一结算（主入口）
    /// </summary>
    public SettlementResult Execute(BattleContext ctx)
    {
        var result = new SettlementResult();
        var activeCards = ctx.PendingPlanCards.ToList();
        
        // Layer 0: 反制
        _layerExecutor.ExecuteCounterPhase(ctx, activeCards, result);
        
        // 过滤被反制的卡牌
        var uncountered = activeCards.Where(c => !c.IsCountered).ToList();
        
        // Layer 1: 防御/修正
        _layerExecutor.ExecuteDefensePhase(ctx, uncountered, result);
        
        // Layer 2: 伤害
        _layerExecutor.ExecuteDamagePhase(ctx, uncountered, result);
        
        // Layer 3: 功能
        _layerExecutor.ExecuteUtilityPhase(ctx, uncountered, result);
        
        // 清理本回合数据
        ctx.PendingPlanCards.Clear();
        
        return result;
    }
}

/// <summary>
/// 结算结果 - 记录本次结算的所有事件
/// </summary>
public class SettlementResult
{
    public List<SettlementEvent> Events { get; set; } = new();
    
    public void AddEvent(SettlementEventType type, int sourceId, int targetId, int value)
    {
        Events.Add(new SettlementEvent
        {
            Type = type,
            SourcePlayerId = sourceId,
            TargetPlayerId = targetId,
            Value = value,
            Timestamp = Events.Count // 相对顺序
        });
    }
}

public enum SettlementEventType
{
    Countered,      // 被反制
    ShieldGained,   // 获得护盾
    DamageTaken,    // 受到伤害
    HealReceived,   // 治疗
    Stunned,        // 被晕眩
    Silenced,       // 被沉默
    CardDrawn,      // 抽牌
    Died,           // 死亡
}
```

### 4. LayerExecutor（分层执行器）

```csharp
/// <summary>
/// 分层执行器 - 按层级执行效果
/// </summary>
public class LayerExecutor
{
    /// <summary>执行 Layer 0: 反制</summary>
    public void ExecuteCounterPhase(BattleContext ctx, List<PlayedCard> cards, SettlementResult result)
    {
        var counterCards = cards.Where(c => 
            IsInLayer(c.PrimaryEffectType, 0)).ToList();
        
        foreach (var counter in counterCards)
        {
            var targets = FindCounterTargets(ctx, counter, cards);
            foreach (var target in targets)
            {
                target.IsCountered = true;
                result.AddEvent(SettlementEventType.Countered, 
                    counter.OwnerId, target.OwnerId, 0);
                
                // 执行惩罚效果（如有）
                ApplyCounterPunishment(ctx, counter, target, result);
            }
        }
    }
    
    /// <summary>执行 Layer 1: 防御/修正</summary>
    public void ExecuteDefensePhase(BattleContext ctx, List<PlayedCard> cards, SettlementResult result)
    {
        var defenseCards = cards.Where(c => 
            IsInLayer(c.PrimaryEffectType, 1)).ToList();
        
        // 收集所有防御效果
        var modifiers = new Dictionary<int, DefenseModifier>();
        foreach (var card in defenseCards)
        {
            CollectDefenseEffects(ctx, card, modifiers);
        }
        
        // 同时应用
        foreach (var (playerId, modifier) in modifiers)
        {
            var player = ctx.GetPlayer(playerId);
            player.Shield += modifier.Shield;
            player.Armor += modifier.Armor;
            player.AttackModifier += modifier.AttackMod;
            
            if (modifier.Shield > 0)
            {
                result.AddEvent(SettlementEventType.ShieldGained, 
                    playerId, playerId, modifier.Shield);
            }
        }
    }
    
    /// <summary>执行 Layer 2: 伤害</summary>
    public void ExecuteDamagePhase(BattleContext ctx, List<PlayedCard> cards, SettlementResult result)
    {
        var damageCards = cards.Where(c => 
            IsInLayer(c.PrimaryEffectType, 2)).ToList();
        
        // 收集所有伤害
        var damageMap = new Dictionary<int, int>();
        foreach (var card in damageCards)
        {
            var targets = ResolveTargets(ctx, card);
            foreach (var targetId in targets)
            {
                int damage = CalculateDamage(ctx, card, targetId);
                damageMap[targetId] = damageMap.GetValueOrDefault(targetId) + damage;
            }
        }
        
        // 同时应用伤害
        foreach (var (playerId, damage) in damageMap)
        {
            var player = ctx.GetPlayer(playerId);
            player.TakeDamage(damage);
            result.AddEvent(SettlementEventType.DamageTaken, 
                0, playerId, damage);
        }
        
        // 处理触发效果（反伤、吸血等）
        ProcessTriggerEffects(ctx, damageMap, result);
    }
    
    /// <summary>执行 Layer 3: 功能</summary>
    public void ExecuteUtilityPhase(BattleContext ctx, List<PlayedCard> cards, SettlementResult result)
    {
        var utilityCards = cards.Where(c => 
            IsInLayer(c.PrimaryEffectType, 3)).ToList();
        
        foreach (var card in utilityCards)
        {
            foreach (var effect in card.Config.Effects)
            {
                ApplyUtilityEffect(ctx, card, effect, result);
            }
        }
    }
    
    /// <summary>判断效果类型所属层级</summary>
    private bool IsInLayer(int effectType, int layer)
    {
        return layer switch
        {
            0 => effectType >= 100 && effectType < 200,  // Counter
            1 => effectType >= 200 && effectType < 300,  // Defense
            2 => effectType >= 300 && effectType < 400,  // Damage
            3 => effectType >= 400 && effectType < 500,  // Utility
            _ => false
        };
    }
}
```

### 5. SeededRandom（种子随机）

```csharp
/// <summary>
/// 种子随机数生成器 - 确保确定性
/// </summary>
public class SeededRandom
{
    private readonly System.Random _random;
    
    public SeededRandom(int seed)
    {
        _random = new System.Random(seed);
    }
    
    /// <summary>生成 [0, maxExclusive) 范围的整数</summary>
    public int Next(int maxExclusive)
    {
        return _random.Next(maxExclusive);
    }
    
    /// <summary>生成 [min, maxExclusive) 范围的整数</summary>
    public int Next(int min, int maxExclusive)
    {
        return _random.Next(min, maxExclusive);
    }
    
    /// <summary>洗牌（Fisher-Yates 算法）</summary>
    public void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
```

---

## 🔄 数据流

### 结算数据流

```
客户端/服务端
    │
    │  1. 构建 BattleContext
    │  2. 填充 PendingPlanCards
    ▼
┌─────────────────────────┐
│   SettlementEngine      │
│   .Execute(ctx)         │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────────────────────────┐
│  LayerExecutor                              │
│  ├── ExecuteCounterPhase  (Layer 0)         │
│  ├── ExecuteDefensePhase  (Layer 1)         │
│  ├── ExecuteDamagePhase   (Layer 2)         │
│  └── ExecuteUtilityPhase  (Layer 3)         │
└───────────┬─────────────────────────────────┘
            │
            ▼
    SettlementResult
    (事件列表，用于客户端播放动画)
```

### 客户端使用方式

```csharp
// 客户端预测结算（本地计算）
public void PredictSettlement()
{
    // 复制一份 Context 用于预测
    var predictCtx = _battleContext.DeepClone();
    
    // 执行结算
    var engine = new SettlementEngine();
    var result = engine.Execute(predictCtx);
    
    // 播放预测动画
    _animator.PlayPrediction(result);
}

// 收到服务端权威结果
public void OnServerSettlement(SettlementResult serverResult)
{
    // 应用服务端权威状态
    _battleContext = serverResult.FinalContext;
    
    // 校正动画（如有差异）
    _animator.CorrectAnimation(serverResult);
}
```

---

## ⚠️ 开发注意事项

### 禁止事项

| ❌ 禁止 | 原因 |
|--------|------|
| 使用 `UnityEngine.*` | asmdef 限制，且会破坏服务端兼容性 |
| 使用 `System.Random` | 不确定性，必须用 `SeededRandom` |
| 使用浮点数运算 | 精度问题可能导致客户端/服务端结果不一致 |
| 在结算函数中保存私有状态 | 破坏无状态原则，所有状态必须在 Context |
| 直接修改 CardConfig | Config 是只读配置，运行时状态用 CardInstance |

### 必须遵守

| ✅ 必须 | 原因 |
|--------|------|
| 所有随机使用 `ctx.Random` | 确保确定性 |
| 伤害公式使用整数运算 | 避免浮点精度问题 |
| 修改状态通过 Context | 可追踪、可序列化 |
| 为所有公共 API 添加 XML 注释 | 代码即文档 |
| 单元测试覆盖核心路径 | 结算逻辑不能出错 |

---

## 🧪 测试指南

### 单元测试示例

```csharp
[TestClass]
public class SettlementEngineTests
{
    [TestMethod]
    public void Execute_CounterNullifiesDamage()
    {
        // Arrange
        var ctx = CreateTestContext();
        var damageCard = CreateDamageCard(damage: 10, owner: 1, target: 2);
        var counterCard = CreateCounterCard(owner: 2, targetTag: CardTag.Damage);
        
        ctx.PendingPlanCards.Add(damageCard);
        ctx.PendingPlanCards.Add(counterCard);
        
        var engine = new SettlementEngine();
        
        // Act
        var result = engine.Execute(ctx);
        
        // Assert
        Assert.IsTrue(damageCard.IsCountered);
        Assert.AreEqual(100, ctx.GetPlayer(2).CurrentHp); // HP 未变化
    }
    
    [TestMethod]
    public void Execute_SimultaneousDamage()
    {
        // 验证双方同时出伤害牌，双方同时扣血
        var ctx = CreateTestContext();
        ctx.GetPlayer(1).CurrentHp = 100;
        ctx.GetPlayer(2).CurrentHp = 100;
        
        var card1 = CreateDamageCard(damage: 20, owner: 1, target: 2);
        var card2 = CreateDamageCard(damage: 30, owner: 2, target: 1);
        
        ctx.PendingPlanCards.Add(card1);
        ctx.PendingPlanCards.Add(card2);
        
        var engine = new SettlementEngine();
        engine.Execute(ctx);
        
        // 双方同时扣血，不存在"先杀死对方"的情况
        Assert.AreEqual(70, ctx.GetPlayer(1).CurrentHp);
        Assert.AreEqual(80, ctx.GetPlayer(2).CurrentHp);
    }
}
```

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 结算规则设计 | [../GameDesign/SettlementRules.md](../GameDesign/SettlementRules.md) |
| 卡牌系统 | [../GameDesign/CardSystem.md](../GameDesign/CardSystem.md) |
| 快速入门 | [QuickStart.md](QuickStart.md) |
| 代码位置 | `Shared/BattleCore/Settlement/SettlementEngine.cs` |
