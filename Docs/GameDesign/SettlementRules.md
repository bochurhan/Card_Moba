# 结算规则详解

**文档版本**：V3.1  
**最后更新**：2025-02-25  
**前置阅读**：[Overview.md](Overview.md)、[CardSystem.md](CardSystem.md)  
**阅读时间**：10 分钟

---

## 🎯 设计目标

解决 **同步回合制** 中 6 名玩家同时出牌的核心问题：

| 问题 | 解决方案 |
|------|----------|
| 先后手优势 | 分层同步结算，同层无顺序 |
| 反制时机 | Layer 0 优先执行，无效化后续效果 |
| 状态依赖 | 预结算 → 实际结算，一次性应用 |
| 确定性 | 服务端权威结算，客户端预测+校正 |

---

## 📊 四层结算模型（V3.0）

### 层级定义

```
定策牌结算顺序（同层无先后手）

    Layer 0: 反制层
    ↓ （被反制的卡牌不进入后续层）
    Layer 1: 防御/修正层
    ↓
    Layer 2: 伤害层（含触发效果）
    ↓
    Layer 3: 功能层
```

### EffectType → 层级映射

#### V3.0 核心类型（1-10，优先使用）

| EffectType | 名称 | 层级 |
|------------|------|------|
| `Counter = 1` | 反制 | Layer 0 |
| `Damage = 2` | 伤害 | Layer 2 |
| `Shield = 3` | 护盾 | Layer 1 |
| `Heal = 4` | 治疗 | Layer 3 |
| `Stun = 5` | 眩晕 | Layer 3 |
| `Armor = 6` | 护甲 | Layer 1 |
| `AttackBuff = 7` | 攻击强化 | Layer 1 |
| `Reflect = 8` | 反伤 | Layer 1 |
| `Vulnerable = 9` | 易伤 | Layer 3 |
| `Draw = 10` | 抽牌 | Layer 3 |

#### 旧版兼容类型（100+，保留兼容）

| 编号范围 | 层级 | 包含效果 |
|----------|------|----------|
| 100-199 | Layer 1 | GainArmor(101), GainShield(102), Weak(116)... |
| 200-299 | Layer 2 | DealDamage(201), Lifesteal(212), Thorns(211)... |
| 300-399 | Layer 3 | GainEnergy(303), Silence(311), Discard(302)... |
| 400-499 | Layer 0 | CounterCard(401), CounterFirstDamage(402)... |

### 详细说明

| 层级 | 内容 | 执行规则 |
|------|------|----------|
| **Layer 0** | 反制效果 | 判定成功的反制 → 标记目标卡牌为"已反制" → 执行惩罚效果（如有） |
| **Layer 1** | 防御/修正 | 同时应用所有护盾、护甲、攻击力修正 |
| **Layer 2** | 伤害+触发 | 1. 计算所有伤害值 2. 同时扣血 3. 处理触发效果（反伤、吸血） |
| **Layer 3** | 功能效果 | 控制、资源操作、传说牌等 |

---

## ⚔️ Layer 0: 反制机制

### 反制判定流程

```
1. 收集所有本回合打出的反制卡
   - V3.0: EffectType == Counter (1)
   - 兼容: EffectType 400-499
   
2. 收集所有可被反制的目标卡

3. 对于每张反制卡：
   a. 检查是否存在匹配的目标
   b. 如果匹配成功：
      - 标记目标卡为 "IsCountered = true"
      - 执行惩罚效果（如有）
      
4. 被反制的卡牌不进入 Layer 1-3 结算
```

### 反制匹配规则

| 反制类型 | 匹配条件 |
|----------|----------|
| 反制伤害牌 | `(targetCard.Tags & CardTag.Damage) != 0` |
| 反制控制牌 | `(targetCard.Tags & CardTag.Control) != 0` |
| 反制定策牌 | `targetCard.TrackType == 定策牌` |
| 反制指定卡 | `targetCard.CardId == specifiedId` |

### 代码接口（V3.1）

```csharp
/// <summary>
/// 执行 Layer 0 反制结算
/// </summary>
private void ResolveLayer0_Counter(BattleContext ctx)
{
    var counterCards = ctx.PendingCounterCards;
    
    foreach (var counterCard in counterCards)
    {
        foreach (var targetCard in ctx.PendingPlanCards)
        {
            if (ShouldCounter(counterCard, targetCard))
            {
                targetCard.IsCountered = true;
                ctx.CounteredCards.Add(targetCard);
                
                // 通过 Handler 执行反制效果
                var handler = HandlerRegistry.GetHandler(EffectType.Counter);
                handler?.Execute(ctx, counterCard, counterCard.Config.Effects[0], source);
            }
        }
    }
    
    // 过滤未被反制的卡牌
    ctx.ValidPlanCards = ctx.PendingPlanCards.Where(c => !c.IsCountered).ToList();
}
```

---

## 🛡️ Layer 1: 防御/修正

### 执行内容

| V3.0 类型 | 旧版类型 | 说明 |
|-----------|----------|------|
| `Shield (3)` | `GainShield (102)` | 添加护盾值到玩家状态 |
| `Armor (6)` | `GainArmor (101)` | 增加护甲（减伤百分比） |
| `AttackBuff (7)` | `GainStrength (111)` | 修正攻击力 |
| `Reflect (8)` | `Thorns (211)` | 反伤效果 |

### 同步应用规则

所有 Layer 1 效果**同时计算**，**同时应用**：

```csharp
/// <summary>
/// 执行 Layer 1 防御结算（V3.1）
/// </summary>
private void ResolveLayer1_Defense(BattleContext ctx)
{
    var layer1Effects = new List<EffectToResolve>();
    
    foreach (var card in ctx.ValidPlanCards)
    {
        foreach (var effect in card.Config.Effects)
        {
            // 使用 V3.0 层级判断方法
            if (effect.GetSettlementLayerV3() == 1)
            {
                layer1Effects.Add(new EffectToResolve 
                { 
                    Card = card, 
                    Effect = effect 
                });
            }
        }
    }
    
    // 同时应用所有效果
    foreach (var item in layer1Effects)
    {
        ExecuteEffect(ctx, item.Card, item.Effect);
    }
}
```

---

## 💥 Layer 2: 伤害+触发

### 伤害计算公式

```
实际伤害 = (基础伤害 + 攻击修正) × (1 - 护甲减伤) - 护盾
```

### 执行步骤

1. **收集伤害**：统计所有伤害效果及其目标
2. **计算修正**：应用攻击力修正、护甲减伤
3. **同时扣血**：对所有目标同时应用伤害
4. **处理触发**：反伤、吸血、濒死触发等

### 代码接口（V3.1）

```csharp
/// <summary>
/// 执行 Layer 2 伤害结算
/// </summary>
private void ResolveLayer2_Damage(BattleContext ctx)
{
    // Step1: 收集并同步应用所有伤害
    var damageMap = new Dictionary<string, int>();  // playerId → totalDamage
    
    foreach (var card in ctx.ValidPlanCards)
    {
        foreach (var effect in card.Config.Effects)
        {
            if (effect.GetSettlementLayerV3() == 2)
            {
                var handler = HandlerRegistry.GetHandler(effect.EffectType);
                handler?.Execute(ctx, card, effect, ctx.GetPlayer(card.SourcePlayerId));
            }
        }
    }
    
    // Step2: 处理触发效果（吸血、反伤）
    ProcessTriggerEffects(ctx);
}
```

### 触发效果处理

| 触发类型 | EffectType | 触发条件 | 效果 |
|----------|------------|----------|------|
| 反伤 | `Reflect(8)`, `Thorns(211)` | 受到伤害时 | 对伤害来源造成固定/比例伤害 |
| 吸血 | `Lifesteal(212)` | 造成伤害时 | 恢复固定/比例生命值 |
| 濒死触发 | - | 生命值归零时 | 触发救生效果（如有） |

---

## 🎲 Layer 3: 功能效果

### 执行内容

| V3.0 类型 | 旧版类型 | 说明 |
|-----------|----------|------|
| `Heal (4)` | - | 恢复生命值 |
| `Stun (5)` | - | 晕眩（下回合无法操作） |
| `Vulnerable (9)` | - | 易伤（受伤增加） |
| `Draw (10)` | - | 抽牌 |
| - | `Silence (311)` | 沉默（下回合无法使用技能牌） |
| - | `GainEnergy (303)` | 能量回复 |

### 执行规则

- 同层功能效果**同时生效**
- 控制效果**立即标记**，在下回合生效
- 资源效果**立即应用**

---

## ☠️ 濒死判定期

在 Layer 3 结束后，执行统一的死亡判定：

```
1. 检查所有玩家生命值
2. HP ≤ 0 的玩家标记为"濒死"
3. 检查是否有救生效果（护盾、回复等）
4. 无救生的濒死玩家判定为"死亡"
5. 清除死亡玩家尚未结算的效果
6. 执行死亡后续（奖励、支援解锁等）
```

---

## 🔄 完整结算流程图

```
回合开始
    │
    ▼
┌───────────────────────────┐
│  操作窗口期（25秒）         │
│  - 瞬策牌：立即生效         │
│  - 定策牌：提交/修改/取消    │
└───────────────────────────┘
    │
    ▼
指令锁定
    │
    ▼
┌───────────────────────────┐
│  PreResolveTargets        │
│  → 为所有卡牌解析目标        │
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 0: 反制结算          │
│  → 标记被反制的卡牌          │
│  → 输出 ValidPlanCards      │
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 1: 防御/修正结算      │
│  → 同时应用护盾/护甲/修正    │
│  → GetSettlementLayerV3()==1│
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 2: 伤害结算          │
│  → 同时扣血 → 触发效果      │
│  → GetSettlementLayerV3()==2│
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 3: 功能效果结算       │
│  → 控制/资源/传说           │
│  → GetSettlementLayerV3()==3│
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  濒死判定期                 │
│  → 死亡判定 → 后续处理      │
└───────────────────────────┘
    │
    ▼
回合结束（弃牌、清能量）
    │
    ▼
下一回合
```

---

## 🖥️ 客户端-服务端协作

### 预测与校正模型

```
客户端：
1. 本地预计算结算结果（使用相同的 BattleCore）
2. 显示预测动画
3. 收到服务端权威结果
4. 比对差异 → 如有差异则校正显示

服务端：
1. 收集所有玩家的锁定指令
2. 调用 BattleCore 执行权威结算
3. 广播结算结果给所有客户端
```

### 确定性保证

- **相同输入 → 相同输出**：BattleCore 必须是确定性的
- **使用 SeededRandom**：所有随机效果使用种子随机数
- **禁止浮点运算**：伤害计算使用整数

---

## 📝 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| V3.1 | 2025-02-25 | 同步 V3.0 EffectType 体系；更新代码示例使用 GetSettlementLayerV3() |
| V1.0 | 初始 | 基础结算规则文档 |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 卡牌系统 | [CardSystem.md](CardSystem.md) |
| 核心代码解读 | [../TechGuide/BattleCore.md](../TechGuide/BattleCore.md) |
| 代码位置 | `Shared/BattleCore/Settlement/SettlementEngine.cs` |
| Handler 注册 | `Shared/BattleCore/Settlement/Handlers/HandlerRegistry.cs` |