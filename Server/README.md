# Server - ASP.NET Core 后台服务

## 技术环境
- ASP.NET Core 7.0 / 8.0
- SignalR（WebSocket长连接管理）
- Entity Framework Core（MySQL ORM）
- StackExchange.Redis（Redis客户端）
- Protobuf-net（协议序列化）

## MVP架构（4层极简架构）
单台2核4G云服务器即可运行，开发周期4-6周。
```
客户端 → 网关层 → 核心逻辑层 → 数据存储层
```

## 目录结构
```
Server/
├── src/
│   ├── CardMoba.Gateway/            # 网关层 - 客户端连接唯一入口
│   │   ├── Hubs/                    # SignalR Hub（WebSocket连接管理）
│   │   ├── Middleware/              # 中间件（心跳检测、协议校验、AES加密）
│   │   └── Filters/                # 过滤器（非法请求拦截、签名校验）
│   │
│   ├── CardMoba.AccountService/     # 账号服务
│   │   ├── Controllers/            # API接口（登录、注册、密码找回）
│   │   ├── Services/               # 业务逻辑（账号管理、卡组CRUD）
│   │   └── Models/                 # 请求/响应模型
│   │
│   ├── CardMoba.MatchService/       # 匹配服务
│   │   ├── Services/               # 匹配逻辑（队列管理、匹配规则、超时放宽）
│   │   └── Models/                 # 匹配相关数据模型
│   │
│   ├── CardMoba.RoomService/        # 房间服务（后台核心模块）
│   │   ├── Services/               # 房间生命周期管理（创建→对局→结束→销毁）
│   │   ├── Models/                 # 房间/对局状态模型
│   │   └── Handlers/              # 玩家操作处理（收集、校验、结算调度）
│   │
│   ├── CardMoba.ConfigService/      # 配置服务
│   │   ├── Services/               # 配置加载、热重载、版本管理
│   │   └── Models/                 # 配置缓存模型
│   │
│   ├── CardMoba.Common/             # 公共模块
│   │   ├── Constants/              # 常量定义
│   │   ├── Utils/                  # 工具类
│   │   ├── Extensions/             # 扩展方法
│   │   └── Middleware/             # 公共中间件
│   │
│   └── CardMoba.DataAccess/         # 数据访问层
│       ├── Repositories/           # 仓储模式（MySQL数据操作）
│       ├── Entities/               # 数据库实体（player_account, player_deck, battle_record）
│       ├── Redis/                  # Redis操作（在线状态、房间状态、匹配队列、配置缓存）
│       └── Migrations/             # EF Core数据库迁移
│
├── tests/                           # 单元测试
│   ├── CardMoba.BattleCore.Tests/   # 结算库测试
│   ├── CardMoba.RoomService.Tests/  # 房间服务测试
│   └── CardMoba.MatchService.Tests/ # 匹配服务测试
│
└── sql/                             # 数据库脚本
    └── init.sql                     # 初始化建表SQL
```

## 核心数据存储
- **MySQL 8.0**：玩家永久数据（账号表、卡组表、对局记录表）
- **Redis 7.0**：高频临时数据（在线状态、房间状态、匹配队列、配置缓存）

## 防作弊设计
1. 操作全量校验：所有操作必须经过服务端合法性校验
2. 信息按需下发：非公开信息绝对不下发到客户端
3. 通信加密：AES对称加密 + 签名校验
4. 异常行为检测：实时检测异常操作频率与数值变化
