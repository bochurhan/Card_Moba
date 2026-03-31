# 前后端通信协议

**最后更新**：2026-03-31  
**当前范围**：`localhost 1v1` 联机 MVP  
**传输方式**：`ASP.NET Core + SignalR + JSON`

## 1. 当前协议范围

当前协议只覆盖本机联机 MVP，对应实现位于：

- `Shared/Protocol/`
- `Server/src/CardMoba.Server.Host/`
- `Client/Assets/Scripts/Network/`

当前暂不覆盖：

- 重连恢复
- 匹配系统
- 账号系统
- 2v2 同场同步
- 战斗事件流回放

## 2. Hub 地址与事件名

当前 Hub 路径：

```text
/hubs/match
```

共享事件名定义位于：

- `Shared/Protocol/Hub/MatchHubEventNames.cs`

客户端和服务端必须使用同一套事件名，不允许各自维护字符串常量。

## 3. 客户端 -> 服务端请求

当前请求 DTO 位于：

- `Shared/Protocol/Messages/Requests/MatchRequests.cs`

### 房间与对局

- `CreateLocalMatchRequest`
  - 创建本地测试房间
- `JoinLocalMatchRequest`
  - 通过 `MatchId` 加入房间
- `ReadyRequest`
  - 玩家确认就绪，满员且双方都就绪后开始对局

### 战斗阶段

- `PlayInstantCardRequest`
  - 提交瞬策牌
- `CommitPlanCardRequest`
  - 提交定策牌
- `SetBattleTurnLockRequest`
  - 设置本回合锁定状态
  - `IsLocked = true` 表示锁定回合
  - `IsLocked = false` 表示取消锁定

### 构筑阶段

- `SubmitBuildChoiceRequest`
  - 提交一次构筑选择
- `LockBuildWindowRequest`
  - 当前构筑窗口全部选择完成后锁定

## 4. 服务端 -> 客户端消息

当前消息 DTO 位于：

- `Shared/Protocol/Messages/Messages/MatchMessages.cs`

### 房间与开局

- `MatchCreatedMessage`
- `MatchStartedMessage`

### 阶段与快照

- `PhaseChangedMessage`
- `BattleSnapshotMessage`
- `BuildWindowOpenedMessage`
- `BuildWindowUpdatedMessage`
- `BuildWindowClosedMessage`

### 结束与拒绝

- `BattleEndedMessage`
- `MatchEndedMessage`
- `ActionRejectedMessage`

## 5. 阶段消息约定

`PhaseChangedMessage` 现在只传语义字段，不再下发展示文案：

- `MatchId`
- `PhaseKind`
- `BattleIndex`
- `TotalBattleCount`
- `DeadlineUnixMs`

客户端需要自行根据 `PhaseKind` 决定展示文本、倒计时样式和按钮状态。

当前 `PhaseKind` 只表达阶段语义，不表达 UI 文案。

## 6. 错误码约定

`ActionRejectedMessage` 当前包含：

- `ErrorCode`
- `ActionName`
- `Reason`

规则：

- 客户端逻辑判断只依赖 `ErrorCode`
- `Reason` 仅用于日志和调试
- 不允许客户端依赖 `Reason` 文案做流程控制

当前协议错误码位于：

- `Shared/Protocol/Messages/Common/MatchProtocolDtos.cs`

## 7. 战斗快照约定

战斗快照由服务端构建，不能直接序列化 `BattleContext`。

快照构建器位于：

- `Server/src/CardMoba.Server.Host/Snapshots/BattleSnapshotBuilder.cs`

当前快照包含的核心信息：

- 对局标识
- 当前战斗索引
- 当前回合数
- 当前本地玩家视角
- 我方状态
- 敌方公开状态
- 手牌详情
- 弃牌详情
- 定策区状态
- 当前锁定状态
- 是否需要弃牌选择

可见度原则：

- 我方手牌详情可见
- 敌方只暴露公开信息
- 敌方隐藏手牌具体内容

## 8. 构筑快照约定

构筑窗口快照由服务端构建，不能直接序列化 `BuildWindowState`。

快照构建器位于：

- `Server/src/CardMoba.Server.Host/Snapshots/BuildWindowSnapshotBuilder.cs`

当前快照覆盖：

- 当前窗口状态
- 本地玩家预览血量
- 当前可执行动作
- 升级候选
- 删牌候选
- 拿牌草案组
- 当前机会序号
- 是否已锁定

当前业务规则：

- `拿牌` 为二段式
- 先承诺执行 `AddCard`
- 承诺后才揭示两组候选
- 揭示后不能改回其他动作

## 9. 锁定回合规则

当前联机 MVP 的回合结束按钮语义是：

- `锁定回合`
- `取消锁定`

规则：

- 玩家锁定后，本回合不能继续操作手牌区
- 玩家可以在本回合内取消锁定
- 双方都锁定时可提前推进
- 当前已实现客户端和服务端双重 1 秒冷却保护

当前暂未实现：

- 操作期倒计时
- 超时自动推进

## 10. 当前已知后补项

- 服务端权威战斗事件流
- 客户端统一日志展示协议
- 更细的可见度规则
- 断线重连后的快照恢复
- 2v2 同场协议扩展
