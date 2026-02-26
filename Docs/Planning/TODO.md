# Card_Moba 待办事项 (TODO)

**更新日期**：2026年02月27日

---

## 📌 当前 Sprint - 核心结算引擎完善

### 优先级 P0（必须）

- [x] **实现 SettlementEngine 四层结算**
  - [x] Layer 0: 反制结算 (`ResolveLayer0_Counter`)
  - [x] Layer 1: 防御/修正结算 (`ResolveLayer1_Defense`)
  - [x] Layer 2: 伤害结算 (`ResolveLayer2_Damage` + Step1/Step2)
  - [x] Layer 3: 功能效果结算 (`ResolveLayer3_Utility` + 普通/传说两个子阶段)
  - 参考文档: [SettlementRules.md](../GameDesign/SettlementRules.md)

- [x] **完善 BattleContext 数据结构**
  - [x] `PlayerBattleState` 完整实现（护盾、护甲、Buff列表）
  - [x] `LaneState` 分路状态管理
  - [x] `PlayedCard` 运行时卡牌实例

- [x] **实现 SeededRandom**
  - [x] Fisher-Yates 洗牌算法（`Shuffle<T>` 泛型重载）
  - [x] 确保客户端/服务端结果一致（Seed 传入构造，纯确定性计算）

### 优先级 P1（重要）

- [x] **卡牌效果系统（Handler 机制）**
  - [x] `IEffectHandler` 接口定义（替代原 `ICardEffect`）
  - [x] `DamageHandler` 伤害效果
  - [x] `ShieldHandler` 护盾效果
  - [x] `DrawHandler` 抽牌效果
  - [x] `StunHandler` 晕眩效果
  - [x] `HealHandler` 治疗效果
  - [x] `CounterHandler` 反制效果
  - [x] `HandlerRegistry` 注册表（21种 EffectType 全部注册）

- [x] **目标解析器 (`TargetResolver`)**
  - [x] 解析 `EffectRange` 枚举（12种范围类型全覆盖）
  - [x] 支持跨路目标判定（`AdjacentLanes`、`SpecifiedLane`）
  - [x] 分路内目标筛选（`CurrentLaneEnemies`、`CurrentLaneAllies`）
  - [x] 随机目标支持（`RandomEnemy`、`RandomAlly`，使用 SeededRandom）

---

## 📋 Sprint 2.2 - 配置与数据

- [ ] **ExcelConverter 工具优化**
  - [x] 基础 CSV → JSON 转换
  - [ ] 支持多 Sheet 导出
  - [ ] 数据校验（ID唯一性、枚举合法性）
  - [ ] 批量导出命令

- [ ] **Unity 配置加载器**
  - [ ] `ConfigManager` 单例
  - [ ] JSON 反序列化到 `CardConfig`
  - [ ] 运行时配置热更新支持

- [ ] **卡牌数据填充**
  - [ ] 设计 10 张基础瞬策牌（抽牌、能量）
  - [ ] 设计 20 张基础定策牌（伤害、防御、控制）
  - [ ] 设计 3 张反制牌原型

---

## 🚀 Sprint 2.3 - 回合流程

- [ ] **RoundStateMachine 回合状态机**
  - [ ] 7 阶段流转实现
  - [ ] 阶段超时处理
  - [ ] 服务端推送阶段变更

- [ ] **操作窗口期**
  - [ ] 瞬策牌即时执行
  - [ ] 定策牌提交/修改/取消
  - [ ] 操作锁定与超时自动锁定

- [ ] **客户端预测与校正**
  - [ ] 本地预计算结算
  - [ ] 服务端结果校正机制
  - [ ] 动画播放与校正回滚

---

## 🔮 未来规划

### 分路系统（Sprint 3.x）
- [ ] 分路状态管理
- [ ] 换路申请与确认流程
- [ ] 死亡支援机制
- [ ] 决战阶段分路合并

### 中枢塔系统（Sprint 4.x）
- [ ] 小怪配置与 AI
- [ ] BOSS 多阶段战斗
- [ ] 奖励系统
- [ ] 商店系统（可选）

### 网络同步（Sprint 5.x）
- [ ] SignalR 消息定义
- [ ] 心跳与重连机制
- [ ] 战斗状态同步协议

---

## 🔧 技术债 - 待处理（优先级高）

### TD-01/TD-04（合并）：Buff / Trigger 协作架构重构（优先级：中，1v1玩法验证后执行）

**问题根源**：  
TD-01 和 TD-04 本质是同一个问题——`BuffManager` 的职责边界不清晰，导致两个层面的混乱：
1. `BuffManager.TriggerBuffEffect()` 自己直接执行触发逻辑（扣血/反伤），与 `TriggerManager` 功能重叠
2. `BuffInstance` 数据存放在 `BuffManager._buffs` 内部而非 `PlayerBattleState`，导致序列化困难

**目标架构**（详见 [SystemArchitecture.md](../TechGuide/SystemArchitecture.md) Buff/Trigger 协作架构节）：

```
PlayerBattleState.ActiveBuffs    ← Buff 纯数据，住在玩家状态（可序列化）
BuffManager（薄中介）             ← 只做 CRUD + 向 TriggerSystem 注册/注销回调
TriggerSystem（事件总线）         ← 维护 Fire 节点 + 按 BuffType 分派静态处理函数
```

**具体改造步骤**：

1. **迁移数据**：将 `BuffManager._buffs` 移入 `PlayerBattleState.ActiveBuffs`，`BuffManager` 改为操作 `PlayerBattleState` 的工具类。

2. **瘦身 BuffManager**：只保留：
   - `AddBuff()` — CRUD + 向 `TriggerSystem` 注册回调
   - `RemoveBuff()` — CRUD + 向 `TriggerSystem` 注销回调
   - `TickDecay()` — 回合衰减（调用 `RemoveBuff` 而非直接删 list）
   - 删除 `OnDamageTaken()`、`OnDamageDealt()`、`TriggerBuffEffect()` 等触发逻辑

3. **扩展 TriggerSystem**：增加 `BuffHandlerTable`（`Dictionary<BuffType, Action<BuffInstance, TriggerEvent, BattleContext>>`），将原 `BuffManager` 里的触发逻辑迁移为静态函数。

4. **确认 Fire 节点完整**：确保 `SettlementEngine` 和 `RoundStateMachine` 中的 Fire 节点清单（约 11 个）全部到位。

5. **加入触发深度保护**：`TriggerSystem.Fire()` 中加入 `_triggerDepth` 计数，超过 `MaxTriggerDepth = 8` 时截断并记录警告。

6. **清理冗余字段**：移除 `StunnedRounds`、`SilencedRounds` 等字段（与 TD-02 合并处理）。

**执行时机**：1v1 对战玩法跑通、第一批测试卡牌设计完成后。  
**影响范围**：`BuffManager.cs`、`PlayerBattleState.cs`、`TriggerSystem.cs`、`SettlementEngine.cs`、所有 Handler。

---

### TD-02：PlayerBattleState 三套衰减机制并存（优先级：高）

**问题**：状态衰减逻辑分散在三处，互不统一：
1. `PlayerBattleState.OnRoundStart()` — 直接 `SilencedRounds--`、`StunnedRounds--`
2. `PlayerBattleState.ActiveBuffs` — 独立的 Buff 列表，`OnRoundStart` 里 `RemainingRounds--`
3. `BuffManager._buffs` — 又一套独立 Buff 列表，`OnRoundEnd` 里做衰减

**改造方向**：  
以 `BuffManager` 为唯一权威，移除 `PlayerBattleState` 里的 `*Rounds` 字段和 `ActiveBuffs` 列表，所有状态读写统一走 `ctx.GetBuffManager(playerId)`。

**影响范围**：`PlayerBattleState.cs`、`BattleContext.cs`、所有直接读写 `*Rounds` 字段的代码。

---

### TD-03：CardEffect 缺少显式 Buff 声明字段（优先级：高）

**问题**：`CardEffect` 里没有显式声明"是否附加 Buff"，Handler 只能靠 `Duration > 0` 猜测，导致效果与 BuffManager 的关联方式不明确，卡牌配置时缺乏强制约束。

**改造方向**：  
在 `CardEffect` 中添加 `AppliesBuff`、`BuffType`、`BuffStackRule` 字段，卡牌配置时必须显式声明；Handler 根据 `AppliesBuff` 字段决定是直写字段还是走 `BuffManager.AddBuff()`。

**影响范围**：`CardEffect.cs`、所有 Handler、卡牌配置数据。

**当前状态**：✅ 已完成（字段已添加，Handler 已按 `AppliesBuff` 分派）

---

## 🏗️ 架构演进 - 未来可能需要

### 多目标选择系统 (优先级: 低)

**场景**：
- 选择 2 个友方进行治疗
- 选择 1 敌 1 友进行位置交换
- 条件选择（血量最低的敌人）

**当前限制**：
- `CardConfig.TargetType` 只支持单一目标类型

**解决方案草案**：
```csharp
public class TargetSelection
{
    public TargetType TargetType { get; set; }
    public int Count { get; set; } = 1;
    public string Condition { get; set; } = string.Empty;
}
```

**决策**：暂不实施，等有具体卡牌需求时再设计。

---

## 📚 文档补充

### TechGuide 待补充
- [ ] Architecture.md — 项目架构与分层设计
- [ ] ClientDev.md — 客户端开发规范
- [ ] ServerDev.md — 服务端架构
- [ ] ConfigSystem.md — 配置系统使用
- [ ] Tools.md — 开发工具手册

### API 文档待补充
- [ ] Enums.md — 枚举定义汇总
- [ ] Protocol.md — 通信协议规范

---

## ✅ 已完成

### 2026-02-27 核心结算引擎（Sprint 当前）
- [x] **SettlementEngine 四层结算完整实现**（Layer 0/1/2/3 + 子阶段）
- [x] **BattleContext 数据结构完善**（`PlayerBattleState`、`LaneState`、`PlayedCard`）
- [x] **SeededRandom 实现**（Fisher-Yates 洗牌，纯确定性）
- [x] **Handler 机制完整搭建**（`IEffectHandler` + 21种 EffectType 注册）
  - DamageHandler、HealHandler、ShieldHandler、StunHandler、CounterHandler
  - ArmorHandler、StrengthHandler、VulnerableHandler、WeakHandler、SilenceHandler
  - SlowHandler、InvincibleHandler、DamageReductionHandler
  - LifestealHandler、ThornsHandler、ArmorOnHitHandler
  - DrawHandler、DiscardHandler、EnergyHandler
- [x] **TargetResolver 完整实现**（12种 EffectRange 全覆盖，含跨路/随机目标）
- [x] **CardEffect 显式 Buff 声明字段**（`AppliesBuff`、`BuffType`、`BuffStackRule`、`IsBuffDispellable`）
- [x] **Handler 层全面改造为走 BuffManager**（Buff 类效果不再直写 PlayerBattleState）
- [x] **SettlementEngine 集成 BuffManager 回调**（`OnDamageTaken`/`OnDamageDealt`）
- [x] **TechGuide/SystemArchitecture.md** — Buff/Trigger 协作架构定稿文档

### 2026-02-25 文档重构
- [x] 文档体系深度重构（Scheme C）
- [x] GameDesign/Overview.md — 核心玩法概述
- [x] GameDesign/CardSystem.md — 卡牌系统详解
- [x] GameDesign/SettlementRules.md — 结算规则详解
- [x] GameDesign/LaneSystem.md — 分路系统详解
- [x] GameDesign/CentralTower.md — 中枢塔系统详解
- [x] TechGuide/QuickStart.md — 5分钟快速入门
- [x] TechGuide/BattleCore.md — 核心代码解读

### 2026-02-24 配置工具
- [x] ExcelConverter 基础实现
- [x] CardEditorWindow Unity 编辑器窗口
- [x] Excel 模板创建（Cards.xlsx）
- [x] `E` 前缀 ID 格式避免日期问题

### 架构决策
- [x] **CardSubType 合并到 CardTag** — 统一使用 `Tags` 字段
- [x] **EffectType 决定结算层** — 100-199/200-299/300-399/400-499
- [x] CardEffect.TargetOverride 支持效果级目标覆盖