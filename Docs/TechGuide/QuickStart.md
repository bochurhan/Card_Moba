# 快速开始

## 1. 打开项目

Unity 项目目录：

```text
Client/
```

服务端项目目录：

```text
Server/src/CardMoba.Server.Host/
```

## 2. 本地编译检查

客户端可使用脚本：

```powershell
powershell -ExecutionPolicy Bypass -File "Tools\Validate-ClientCompile.ps1"
```

服务端可使用：

```powershell
dotnet build Server\src\CardMoba.Server.Host\CardMoba.Server.Host.csproj
```

## 3. 启动服务端

在仓库根目录执行：

```powershell
dotnet run --project Server\src\CardMoba.Server.Host\CardMoba.Server.Host.csproj
```

默认 Hub 地址：

```text
http://127.0.0.1:5000/hubs/match
```

## 4. Unity 场景

当前测试场景：

- `Client/Assets/BattleScene.unity`

主要入口脚本：

- `Client/Assets/Scripts/Presentation/Battle/BattleUIManager.cs`

## 5. 本地模式运行

在 `BattleUIManager` 中选择：

- `RuntimeMode = Local`

用途：

- 验证本地 `MatchFlow`
- 验证构筑界面
- 验证 `4+1` 流程

## 6. 联机模式运行

### Host 端

在 `BattleUIManager` 中设置：

- `RuntimeMode = OnlineHost`
- `OnlineHubUrl = http://127.0.0.1:5000/hubs/match`
- `OnlineDisplayName = Host`

运行后会自动：

- 连接服务端
- 创建房间
- 自动 Ready

### Join 端

在 `BattleUIManager` 中设置：

- `RuntimeMode = OnlineJoin`
- `OnlineHubUrl = http://127.0.0.1:5000/hubs/match`
- `OnlineDisplayName = Guest`
- `JoinMatchId = Host 日志里创建出的房间号`

运行后会自动：

- 连接服务端
- 加入房间
- 自动 Ready

## 7. 当前联调重点

优先验证：

1. 建房 / 入房 / Ready
2. 出牌同步
3. 锁定回合 / 取消锁定
4. 场间构筑
5. 整局结束

## 8. 当前已知限制

- 仅支持 `localhost 1v1`
- 暂未实现重连恢复
- 操作期倒计时与超时推进仍待补
- 战斗日志与可见度规则仍需继续完善
