# 结算规则详解

**文档版本**：V4.0  
**最后更新**：2026-02-28  
**前置阅读**：[Overview.md](Overview.md)、[CardSystem.md](CardSystem.md)  
**阅读时间**：10 分钟

---

## 🎯 设计目标

解决 **同步回合制** 中 4 名玩家同时出牌的核心问题：

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

    Layer 0: 反制层（Counter=1）
    ↓ （被反制的卡牌不进入后续层）
    Layer 1: 防御/修正层（ID 2-8）
    ↓
    Layer 2: 伤害层（ID 10-14，含触发效果）
    ↓
    Layer 3: 功能层（ID 20-29）
```

### EffectType → 层级映射

> 以下为实际枚举值，见 `Shared/Protocol/Enums/EffectType.cs`

| EffectType | ID | 层级 | 名称 |
|------------|----|------|------|
| `Counter` | 1 | Layer 0 | 反制 |
| `Shield` | 2 | Layer 1 | 护盾 |
| `Armor` | 3 | Layer 1 | 护甲 |
| `AttackBuff` | 4 | Layer 1 | 攻击力强化 |
| `AttackDebuff` | 5 | Layer 1 | 攻击力削弱 |
| `Reflect` | 6 | Layer 1 | 反伤 |
| `DamageReduction` | 7 | Layer 1 | 减伤 |
| `Invincible` | 8 | Layer 1 | 无敌 |
| `Damage` | 10 | Layer 2 | 直接伤害 |
| `Lifesteal` | 11 | Layer 2 | 吸血 |
| `Thorns` | 12 | Layer 2 | 荆棘反伤 |
| `ArmorOnHit` | 13 | Layer 2 | 受击获甲 |
| `Pierce` | 14 | Layer 2 | 穿透伤害 |
| `Heal` | 20 | Layer 3 | 治疗 |
| `Stun` | 21 | Layer 3 | 眩晕 |
| `Vulnerable` | 22 | Layer 3 | 易伤 |
| `Weak` | 23 | Layer 3 | 虚弱 |
| `Draw` | 24 | Layer 3 | 抽牌 |
| `Discard` | 25 | Layer 3 | 弃牌 |
| `GainEnergy` | 26 | Layer 3 | 获得能量 |
| `Silence` | 27 | Layer 3 | 沉默 |
| `Slow` | 28 | Layer 3 | 迟缓 |
| `DoubleStrength` | 29 | Layer 3 | 力量翻倍 |

> ⚠️ 不存在 100+/200+/300+/400+ 旧版编号，所有 ID 均为上表所列，跳跃 ID（如 9、15-19）为预留位。

### 详细说明

| 层级 | 内容 | 执行规则 |
|------|------|----------|
| **Layer 0** | 反制效果（`Counter=1`） | 判定成功的反制 → 标记目标卡牌为"已反制" → 执行惩罚效果（如有） |
| **Layer 1** | 防御/修正（ID 2-8） | 同时应用所有护盾、护甲、攻击力修正 |
| **Layer 2** | 伤害+触发（ID 10-14） | 1. 计算所有伤害值 2. 同时扣血 3. 处理触发效果（反伤、吸血） |
| **Layer 3** | 功能效果（ID 20-29） | 控制、资源操作、力量倍增等 |

---

## ⚔️ Layer 0: 反制机制

### 反制判定流程

```
1. 收集所有本回合打出的反制卡
   - EffectType == Counter (ID=1)
   
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

### 代码接口

```csharp
/// <summary>
/// 执行 Layer 0 反制结算（见 SettlementEngine.cs）
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
                
                // 通过 Handler 执行反制效果（EffectType.Counter = 1）
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

### Layer 1 执行内容

| EffectType | ID | 说明 |
|------------|----|------|
| `Shield` | 2 | 添加护盾值到玩家状态 |
| `Armor` | 3 | 增加护甲（减伤） |
| `AttackBuff` | 4 | 增加攻击力（加算） |
| `AttackDebuff` | 5 | 降低攻击力 |
| `Reflect` | 6 | 反伤效果 |
| `DamageReduction` | 7 | 固定减伤 |
| `Invincible` | 8 | 本回合免伤 |

### 同步应用规则

所有 Layer 1 效果**同时计算**，**同时应用**：

```csharp
/// <summary>
/// 执行 Layer 1 防御结算（Handler 模式）
/// </summary>
private void ResolveLayer1_Defense(BattleContext ctx)
{
    var layer1Effects = new List<EffectToResolve>();
    
    foreach (var card in ctx.ValidPlanCards)
    {
        foreach (var effect in card.Config.Effects)
        {
            // ID 2-8 属于 Layer 1
            if (effect.GetSettlementLayer() == 1)
            {
                layer1Effects.Add(new EffectToResolve { Card = card, Effect = effect });
            }
        }
    }
    
    // 同时应用所有效果
    foreach (var item in layer1Effects)
    {
        var handler = HandlerRegistry.GetHandler(item.Effect.EffectType);
        handler?.Execute(ctx, item.Card, item.Effect, ctx.GetPlayer(item.Card.SourcePlayerId));
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
    // Step1: 收集并同步应用所有伤害（ID 10-14）
    foreach (var card in ctx.ValidPlanCards)
    {
        foreach (var effect in card.Config.Effects)
        {
            if (effect.GetSettlementLayer() == 2)
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

| 触发类型 | EffectType | ID | 触发条件 | 效果 |
|----------|------------|----|----------|------|
| 反伤 | `Reflect` | 6 | 受到伤害时 | 对来源造成固定伤害 |
| 荆棘 | `Thorns` | 12 | 受到伤害时 | 额外伤害来源 |
| 吸血 | `Lifesteal` | 11 | 造成伤害时 | 恢复等比生命值 |
| 受击获甲 | `ArmorOnHit` | 13 | 受到伤害时 | 获得护甲 |
| 濒死触发 | - | - | 生命值归零时 | 触发救生效果（如有） |

---

## 🎲 Layer 3: 功能效果

### Layer 3 执行内容

| EffectType | ID | 说明 |
|------------|----|------|
| `Heal` | 20 | 恢复生命值 |
| `Stun` | 21 | 眩晕（下回合无法操作） |
| `Vulnerable` | 22 | 易伤（受伤增加） |
| `Weak` | 23 | 虚弱（攻击力降低） |
| `Draw` | 24 | 抽牌 |
| `Discard` | 25 | 弃牌 |
| `GainEnergy` | 26 | 能量回复 |
| `Silence` | 27 | 沉默（下回合无法抽牌） |
| `Slow` | 28 | 迟缓（能量上限降低） |
| `DoubleStrength` | 29 | 力量翻倍（乘法增益） |

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
│  → GetSettlementLayer()==1  │
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 2: 伤害结算          │
│  → 同时扣血 → 触发效果      │
│  → GetSettlementLayer()==2  │
└───────────────────────────┘
    │
    ▼
┌───────────────────────────┐
│  Layer 3: 功能效果结算       │
│  → 控制/资源/力量倍增        │
│  → GetSettlementLayer()==3  │
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
| V4.0 | 2026-02-28 | 全面修正 EffectType ID（对齐枚举实际值）；删除不存在的旧版兼容编号；补全 Layer3；新增 DoubleStrength(29) |
| V3.1 | 2025-02-25 | 同步 V3.0 EffectType 体系 |
| V1.0 | 初始 | 基础结算规则文档 |

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 卡牌系统 | [CardSystem.md](CardSystem.md) |
| 核心代码解读 | [../TechGuide/BattleCore.md](../TechGuide/BattleCore.md) |
| 代码位置 | `Shared/BattleCore/Settlement/SettlementEngine.cs` |
| Handler 注册 | `Shared/BattleCore/Settlement/Handlers/HandlerRegistry.cs` |