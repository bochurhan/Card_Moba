# Client - Unity客户端项目

## ⚠️ Unity工程创建步骤（首次使用必读）

本目录 `Client/` 就是 **Unity工程的根目录**。首次使用请按以下步骤操作：

### 步骤1：用Unity Hub创建工程
1. 打开 Unity Hub → 点击 "New Project"
2. 选择 Unity **2022.3 LTS** 版本
3. 模板选 **3D (Core)**
4. 项目路径设置为 `d:\Card_Moba\Client`（⚠️ 如果提示目录已存在，先将 Client/Assets 和 Client/Packages 临时移走）
5. 创建完成后，Unity会自动生成 `ProjectSettings/`、`UserSettings/`、`Library/` 等必需目录

### 步骤2：合并已规划的目录结构
将我们预先创建的 `Assets/` 子目录（Scripts、Textures、Models等）合并到 Unity 创建的 `Assets/` 下。

### 步骤3：确认Shared包引入
打开 `Client/Packages/manifest.json`，确认包含：
```json
{
  "dependencies": {
    "com.cardmoba.shared": "file:../../Shared",
    // ... Unity自动生成的其他依赖 ...
  }
}
```
⚠️ Unity创建工程时会覆盖 manifest.json，需要手动将 `"com.cardmoba.shared": "file:../../Shared"` 加回去。

### 步骤4：验证
打开Unity后，在 Window → Package Manager 中应能看到 "Card Moba Shared Library" 本地包。
Assets/Scripts 下各层的 .asmdef 文件会在 Project 面板中显示为程序集图标。

## 程序集依赖关系图（Assembly Definition）
```
                     ┌──────────────────────┐
                     │  Shared (UPM本地包)   │
                     │  noEngineReferences  │
                     ├──────────────────────┤
                     │ CardMoba.BattleCore  │
                     │ CardMoba.Protocol    │
                     │ CardMoba.ConfigModels│
                     └──────┬───────────────┘
                            │ 引用
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
  ┌──────────┐     ┌──────────────┐    ┌─────────────┐
  │ Network  │     │    Data      │    │  GameLogic  │
  │(通信层)  │     │  (数据层)    │    │(业务逻辑层) │
  └────┬─────┘     └──────┬───────┘    └──────┬──────┘
       │                  │                   │
       └──────────────────┴───────────────────┘
                          │ 引用
                          ▼
                  ┌──────────────┐
                  │ Presentation │
                  │  (表现层)    │
                  └──────────────┘
                          │ 引用
                          ▼
                  ┌──────────────┐
                  │   Editor     │  ← 仅Editor平台
                  │(编辑器扩展)  │
                  └──────────────┘
```
每层仅能引用下层，**不可反向依赖**，由 .asmdef 文件强制约束。

## 技术环境
- Unity 2022.3 LTS
- UI Toolkit（核心UI框架）
- DOTween（动画插值）
- Odin Inspector（编辑器扩展）
- Photon Unity Networking（MVP阶段网络通信，后续切换自建后台）

## 四层架构说明

### Scripts/ 目录结构
```
Scripts/
├── Presentation/        # 表现层 - 视觉、音效、输入，与业务逻辑完全解耦
│   ├── SceneManagement/ # 3D场景管理（固定斜俯视角、分路站位、场景分块加载）
│   ├── CharacterAnimation/ # 角色动画（状态机、动作事件、特写镜头）
│   ├── UI/              # UI界面
│   │   ├── Views/       # 核心界面（对局主界面、卡组编辑、匹配、登录、结算）
│   │   ├── Components/  # 可复用UI组件（手牌卡片、血条、buff图标等）
│   │   └── Styles/      # UI Toolkit样式文件（.uss）
│   ├── VFX/             # 特效管理（卡牌出牌、技能结算、场景氛围）
│   ├── Audio/           # 音效管理（操作反馈、结算提示、背景音、角色语音）
│   └── Input/           # 输入管理（拖拽、选中、放大预览、出牌提交）
│
├── GameLogic/           # 业务逻辑层 - 本地业务逻辑、预表现计算、服务端状态同步
│   ├── RoundFlow/       # 回合流程管理（FSM状态机，7个回合阶段）
│   ├── CardSystem/      # 卡牌系统（数据模型、生命周期、预表现逻辑）
│   ├── PlayerState/     # 玩家状态管理（血量、能量、护盾、buff/debuff）
│   └── TeamComm/        # 队内通信（手牌同步、定策指令同步、快捷聊天）
│
├── Data/                # 数据层 - 本地数据管理、缓存、持久化
│   ├── ConfigData/      # 配置数据管理（卡牌/职业/数值配置缓存、热更新）
│   ├── PlayerData/      # 玩家数据管理（账号信息、卡组、对局记录、本地加密存储）
│   └── BattleCache/     # 对局数据缓存（对局状态、结算日志、掉线重连恢复）
│
├── Network/             # 网络通信层 - 与后台通信
│   ├── Connection/      # 连接管理（WebSocket长连接、心跳、自动重连）
│   ├── MessageDispatch/ # 消息分发（统一收发入口、消息优先级）
│   ├── Security/        # 安全加密（AES对称加密、签名校验）
│   └── Proto/           # Protobuf生成的C#协议文件
│
├── Common/              # 公共模块
│   ├── Constants/       # 常量定义（魔法数、游戏规则常量）
│   ├── Utils/           # 工具类（加密、格式转换等）
│   ├── Extensions/      # C#扩展方法
│   └── Events/          # 事件系统（表现层与逻辑层解耦通信）
│
└── Editor/              # Unity编辑器扩展（仅编辑器环境生效）
    ├── CardEditor/      # 卡牌编辑器（可视化配置、预览、校验、批量导出）
    ├── BattleSimulator/ # 单机对局模拟器（无需启动后台，模拟完整对局）
    └── Tools/           # 其他编辑器工具
```

### 资源目录结构
```
Assets/
├── Resources/
│   └── Prefabs/         # 预制体
│       ├── Cards/       # 卡牌预制体
│       ├── Characters/  # 角色预制体
│       ├── UI/          # UI预制体
│       ├── VFX/         # 特效预制体
│       └── Scene/       # 场景物件预制体
├── Textures/            # 贴图（移动端≤2048, PC端≤4096）
│   ├── Cards/           # 卡牌贴图
│   ├── UI/              # UI贴图（图集打包，控制DrawCall）
│   ├── Characters/      # 角色贴图
│   └── Scene/           # 场景贴图
├── Models/              # 3D模型（单角色≤5000面，骨骼≤30根）
│   ├── Characters/      # 角色模型
│   └── Scene/           # 场景模型
├── Animations/          # 动画资源（Mixamo动作库）
│   ├── Characters/      # 角色动画（待机、施法、攻击、受击、死亡）
│   └── UI/              # UI动画
├── Audio/               # 音频资源
│   ├── BGM/             # 背景音乐（OGG格式）
│   ├── SFX/             # 音效（WAV格式，单音效≤10s）
│   └── Voice/           # 角色语音
├── Scenes/              # Unity场景文件
├── Fonts/               # 字体文件
├── Plugins/             # 第三方插件（DOTween、Odin等）
├── StreamingAssets/
│   └── Config/          # 运行时配置文件（热更新）
└── ScriptableObjects/   # ScriptableObject资源文件
    ├── Cards/           # 卡牌数据（每张卡牌一个SO文件）
    ├── Heroes/          # 职业数据
    └── GameConfig/      # 游戏配置数据
```

## 性能指标
- 同屏DrawCall ≤ 300
- 常驻内存 ≤ 2GB
- 移动端锁30帧，PC端锁60帧
- 首包体 ≤ 500MB
