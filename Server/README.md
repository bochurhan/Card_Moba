# Server

当前服务端是一个 `ASP.NET Core + SignalR` 的本机联机 MVP 骨架，目标是先把 `MatchFlow + BattleCore` 放到服务端做权威托管，而不是一开始就做完整后台。

## 当前定位

- 只支持 `localhost 1v1`
- 只支持内存房间
- 只托管整局 `4+1`
- 不包含账号、数据库、匹配、重连恢复

## 当前结构

```text
Server/
├─ CardMoba.Server.sln
├─ README.md
├─ sql/
│  └─ init.sql
└─ src/
   └─ CardMoba.Server.Host/
      ├─ Program.cs
      ├─ Config/
      ├─ Hubs/
      ├─ Services/
      ├─ Sessions/
      └─ Snapshots/
```

## 目录职责

### Config

- `ServerCardCatalog`
  - 读取卡牌配置
- `ServerBuildCatalogFactory`
  - 为服务端生成 `IBuildCatalog`
- `ServerBattleFactoryFactory`
  - 装配 `BattleFactory`
- `LocalMatchTemplateFactory`
  - 生成当前本地联机测试用的 `4+1` 模板

### Hubs

- `MatchHub`
  - SignalR 入口
  - 接收客户端请求并转发给会话层

### Services

- `LocalMatchRegistry`
  - 管理未开始房间
- `MatchSessionManager`
  - 管理已开始对局
- `MatchConnectionRegistry`
  - 管理连接与玩家映射

### Sessions

- `MatchSession`
  - 权威对局宿主
- `MatchCommandDispatcher`
  - 处理战斗命令与构筑命令
- `MatchBroadcaster`
  - 负责阶段消息、快照与拒绝消息广播

### Snapshots

- `BattleSnapshotBuilder`
- `BuildWindowSnapshotBuilder`

职责是把运行时对象裁剪成协议 DTO，不直接向客户端暴露 `BattleContext` 或 `BuildWindowState`。

## 启动方式

在仓库根目录执行：

```powershell
dotnet run --project Server\src\CardMoba.Server.Host\CardMoba.Server.Host.csproj
```

默认 Hub 地址：

```text
http://127.0.0.1:5000/hubs/match
```

## 当前规则

- 服务端权威推进整局 `MatchFlow`
- 双方都锁定时可提前推进
- 当前已支持锁定/取消锁定
- 当前已支持构筑窗口提交与锁定
- 当前断线先按默认动作/默认锁定兜底

## 当前已知后补项

- 操作期倒计时与超时推进
- 更完整的断线恢复
- 服务端权威战斗事件流
- 房间生命周期清理细化
- 2v2 扩展
