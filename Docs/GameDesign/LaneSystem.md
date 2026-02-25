# 分路系统详解

**文档版本**：V1.0  
**前置阅读**：[Overview.md](Overview.md)  
**阅读时间**：6 分钟

---

## 🎯 设计目标

| 目标 | 实现方式 |
|------|----------|
| **减少混乱** | 3v3 固定分路，避免 6 人同场信息过载 |
| **增加策略** | 分路选择、换路时机、支援配合 |
| **保留 MOBA 感** | Solo 路单挑、双人路配合 |
| **控制复杂度** | 无自由移动，降低操作门槛 |

---

## 🗺️ 分路结构

### 战场布局

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│     [Solo路]          [中枢塔]          [双人路]         │
│                                                         │
│   Player A1  ←─────→  BOSS  ←─────→  Player A2 + A3    │
│      vs                 vs                vs            │
│   Player B1  ←─────→  BOSS  ←─────→  Player B2 + B3    │
│                                                         │
└─────────────────────────────────────────────────────────┘

A阵营: A1(Solo) A2+A3(双人)
B阵营: B1(Solo) B2+B3(双人)
```

### 分路配置

| 分路 | 人数 | 战斗模式 | 特点 |
|------|------|----------|------|
| **Solo路** | 1v1 | 纯单挑 | 考验个人构筑和博弈能力 |
| **双人路** | 2v2 | 双人配合 | 强调卡牌组合和队友沟通 |
| **中枢塔** | 全员 | PVE+BOSS | 发育阶段，资源争夺 |

---

## 🔒 固定分路规则

### 初始分配

```
匹配阶段：
1. 玩家选择偏好位置（Solo/双人/无偏好）
2. 系统根据段位和偏好进行匹配
3. 匹配成功后锁定位置，对局内不可自由切换

锁定规则：
- Solo路：1人固定
- 双人路：2人固定
- 整局内除特定机制外不变
```

### 为什么没有自由移动？

| 问题 | 如果允许自由移动 | 固定分路解决方案 |
|------|------------------|------------------|
| 信息过载 | 6人同时出牌，难以跟踪 | 分路后最多面对2人 |
| 策略扁平 | 全员抱团成为唯一最优解 | 分路各有优势 |
| 弱势方体验 | 被多人围攻无法翻盘 | 保证 1v1 或 2v2 公平 |

---

## 🔄 换路机制

### 永久换路

在**战斗间隙**可申请永久换路：

```
申请条件：
1. 当前处于战斗间隙（非战斗回合）
2. 有可用换路次数（每人每局1次，可通过中枢塔增加）
3. 目标分路有空位或愿意交换的队友

申请流程：
1. 发起方提交换路请求
2. 被交换方收到确认提示（5秒内决定）
3. 双方同意 → 位置互换
4. 拒绝/超时 → 换路失败，不消耗次数

惩罚机制：
- 换路后下一场战斗能量上限 -1
- 冷却期间无法再次申请（3场战斗）
```

### 临时支援

当**队友死亡**时，可申请临时支援：

```
触发条件：
1. 同阵营队友在战斗中死亡
2. 申请者当前战斗已结束或处于空闲

支援规则：
1. 死亡方分路出现空位
2. 存活队友可选择支援该路
3. 支援者临时加入该路战斗
4. 战斗结束后自动返回原分路

限制：
- 每场战斗仅能支援一次
- 支援时自身分路暂时空置（敌方可推进）
```

---

## 🎯 跨路卡牌

### 设计原则

虽然分路固定，但仍保留**有限的跨路影响**：

```
核心限制：
1. 仅定策牌可带「跨路生效」标签
2. 瞬策牌永远不能跨路
3. 跨路卡通常消耗更高或效果打折

跨路效果示例：
- "对任意敌方造成3点伤害"（原伤害5点，跨路减伤40%）
- "支援友方，下回合抽牌+1"（需要友方接受）
```

### 跨路标签处理

```csharp
/// <summary>
/// 判断卡牌是否可以选择跨路目标
/// </summary>
public bool CanSelectCrossLaneTarget(CardConfig card, PlayerState player)
{
    // 必须是定策牌
    if (card.TrackType != CardTrackType.定策牌)
        return false;
    
    // 必须带有跨路标签
    if ((card.Tags & CardTag.CrossLane) == 0)
        return false;
    
    // 检查玩家是否有跨路限制状态（如沉默）
    if (player.HasDebuff(DebuffType.CrossLaneLocked))
        return false;
    
    return true;
}

/// <summary>
/// 获取可选的跨路目标
/// </summary>
public List<int> GetCrossLaneTargets(BattleContext ctx, int playerId, CardConfig card)
{
    var player = ctx.GetPlayer(playerId);
    var targets = new List<int>();
    
    // 获取其他分路的有效目标
    foreach (var lane in ctx.Lanes)
    {
        if (lane.LaneId == player.CurrentLaneId)
            continue; // 跳过本路
        
        foreach (var targetId in lane.GetEnemiesOf(player.TeamId))
        {
            if (IsValidTarget(card, ctx.GetPlayer(targetId)))
            {
                targets.Add(targetId);
            }
        }
    }
    
    return targets;
}
```

---

## 📊 分路状态

### LaneState 数据结构

```csharp
/// <summary>
/// 分路状态
/// </summary>
public class LaneState
{
    /// <summary>分路ID (0=Solo, 1=双人)</summary>
    public int LaneId { get; set; }
    
    /// <summary>分路类型</summary>
    public LaneType LaneType { get; set; }
    
    /// <summary>A阵营玩家ID列表</summary>
    public List<int> TeamAPlayers { get; set; }
    
    /// <summary>B阵营玩家ID列表</summary>
    public List<int> TeamBPlayers { get; set; }
    
    /// <summary>当前回合数</summary>
    public int CurrentRound { get; set; }
    
    /// <summary>分路战斗是否结束</summary>
    public bool IsBattleEnded { get; set; }
    
    /// <summary>获取指定阵营的敌方玩家</summary>
    public List<int> GetEnemiesOf(int teamId)
    {
        return teamId == 0 ? TeamBPlayers : TeamAPlayers;
    }
}

public enum LaneType
{
    Solo = 0,    // 1v1 单人路
    Duo = 1,     // 2v2 双人路
    United = 2,  // 3v3 合并路（决战阶段）
}
```

---

## 🏁 分路合并（决战阶段）

### 触发条件

进入**全局决战死斗期**时，分路自动合并：

```
合并时机：
1. 第二中枢塔阶段结束
2. 任一阵营分路全灭
3. 达到特定回合数阈值

合并流程：
1. 所有存活玩家进入同一战场
2. 分路状态切换为 LaneType.United
3. 解锁所有跨路限制
4. 开始 3v3 终极对抗
```

### 决战特殊规则

| 规则 | 说明 |
|------|------|
| 跨路限制解除 | 所有卡牌可选择任意敌方目标 |
| 死亡即淘汰 | 无复活，无支援 |
| 增强节奏 | 能量上限+1，抽牌+1 |
| 硬上限 | 最多10回合（25回合总上限） |

---

## 🎮 UI 表现建议

### 分路视图

```
┌─────────────────────────────────────────┐
│  [Solo路]                [双人路]        │
│  ┌─────────┐            ┌──────────────┐│
│  │ 敌方 B1 │            │ 敌方 B2  B3  ││
│  │   HP    │            │   HP    HP   ││
│  ├─────────┤            ├──────────────┤│
│  │ 我方 A1 │            │ 队友 A2  A3  ││
│  │   HP    │            │   HP    HP   ││
│  └─────────┘            └──────────────┘│
│                                         │
│  [切换视图]  [跨路支援]  [队伍沟通]       │
└─────────────────────────────────────────┘
```

### 跨路操作 UI

```
选择跨路目标时：
1. 灰显当前分路
2. 高亮可选目标分路
3. 显示跨路效果预览（如减伤提示）
4. 确认后锁定目标
```

---

## 📖 关联文档

| 主题 | 文档 |
|------|------|
| 核心玩法 | [Overview.md](Overview.md) |
| 结算规则 | [SettlementRules.md](SettlementRules.md) |
| 中枢塔系统 | [CentralTower.md](CentralTower.md) |
