# Card_Moba 待办事项 (TODO)

**更新日期**：2026年02月25日

---

## 📌 当前 Sprint - 核心结算引擎完善

### 优先级 P0（必须）

- [ ] **实现 SettlementEngine 四层结算**
  - [ ] Layer 0: 反制结算 (`ExecuteCounterPhase`)
  - [ ] Layer 1: 防御/修正结算 (`ExecuteDefensePhase`)
  - [ ] Layer 2: 伤害结算 (`ExecuteDamagePhase`) — 已有基础实现
  - [ ] Layer 3: 功能效果结算 (`ExecuteUtilityPhase`)
  - 参考文档: [SettlementRules.md](../GameDesign/SettlementRules.md)

- [ ] **完善 BattleContext 数据结构**
  - [ ] `PlayerState` 完整实现（护盾、护甲、Buff列表）
  - [ ] `LaneState` 分路状态管理
  - [ ] `PlayedCard` 运行时卡牌实例

- [ ] **实现 SeededRandom**
  - [ ] Fisher-Yates 洗牌算法
  - [ ] 确保客户端/服务端结果一致

### 优先级 P1（重要）

- [ ] **卡牌效果系统**
  - [ ] `ICardEffect` 接口定义
  - [ ] `DamageEffect` 伤害效果
  - [ ] `ShieldEffect` 护盾效果
  - [ ] `DrawEffect` 抽牌效果
  - [ ] `StunEffect` 晕眩效果

- [ ] **目标解析器 (TargetResolver)**
  - [ ] 解析 `TargetType` 枚举
  - [ ] 支持跨路目标判定 (`CardTag.CrossLane`)
  - [ ] 分路内目标筛选

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