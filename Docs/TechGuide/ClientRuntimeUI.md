# 客户端运行时与 UI 解耦

**最后更新**：2026-03-31

## 1. 目标

这轮客户端改造的目标不是删除本地战斗逻辑，而是把 UI 从具体运行时对象上抽离出来。

当前结论：

- 保留本地 `BattleGameManager`
- `BattleUIManager` 不再直接依赖本地战斗数据类型
- 后续联机模式通过新增 `OnlineBattleClientRuntime` 接入
- UI 尽量不因为服务端接入而重写

## 2. 当前分层

```text
BattleUIManager / BuildWindowPanel
    -> IBattleClientRuntime
       -> BattleGameManager（本地）
       -> OnlineBattleClientRuntime（联机）
```

## 3. 核心接口

当前统一入口：

- `Client/Assets/Scripts/GameLogic/Abstractions/IBattleClientRuntime.cs`

它对 UI 暴露：

- 阶段变化
- 战斗快照
- 构筑窗口视图
- 日志输出
- 出牌与锁定接口

## 4. 视图模型

UI 不直接消费 `BattleContext` 或 `BuildWindowState`。

当前通过视图模型承载：

- `BattleSnapshotViewState`
- `PlayerBattleViewState`
- `BuildWindowViewState`
- `PlayerBuildWindowViewState`
- `BuildOpportunityViewState`

这些类型位于：

- `Client/Assets/Scripts/GameLogic/Abstractions/BattleRuntimeViewModels.cs`

## 5. 本地运行时

当前本地实现为：

- `Client/Assets/Scripts/GameLogic/BattleGameManager.cs`

职责：

- 承接本地 `MatchFlow`
- 映射本地战斗与构筑状态到 UI 视图模型
- 继续支持本地 `4+1` 流程测试

## 6. 联机运行时

当前联机实现为：

- `Client/Assets/Scripts/GameLogic/Online/OnlineBattleClientRuntime.cs`
- `Client/Assets/Scripts/Network/Connection/MatchHubConnection.cs`

职责：

- 连接 SignalR Hub
- 发送联机请求
- 接收服务端消息
- 映射协议 DTO 到 UI 视图模型

## 7. BuildWindowPanel

当前构筑界面位于：

- `Client/Assets/Scripts/Presentation/Battle/BuildWindowPanel.cs`

职责：

- 展示当前构筑机会
- 展示升级/删牌/拿牌候选
- 提交构筑选择
- 锁定构筑窗口

当前已支持：

- 强制恢复窗口
- 拿牌二段式揭示
- 构筑日志与基础调试

## 8. 当前已知后补项

- 战斗事件展示内容仍不完整
- 展示可见度仍需系统化
- 更完整的联机错误提示
- UI 细节与动效仍待完善
