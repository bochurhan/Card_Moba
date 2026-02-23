# Shared - 前后端共享代码

## 核心定位
完全独立、无状态、与业务逻辑解耦的共享类库，前后端共用同一套代码，保证结算逻辑100%一致。

## 项目类型
- .NET Standard 2.1 类库（兼容Unity 2022.3 + ASP.NET Core）
- 无任何Unity/ASP.NET Core依赖，纯逻辑代码

## 目录结构
```
Shared/
├── BattleCore/              # 权威结算库（后台的灵魂）
│   ├── RoundStateMachine/   # 回合状态机（7个阶段严格流转）
│   │                        #   回合开始结算→操作窗口期→指令锁定→
│   │                        #   统一结算→濒死判定→回合结束
│   ├── CardEffects/         # 卡牌效果系统
│   │   ├── Components/      # 效果组件（造成伤害、抽牌、叠buff、反制等）
│   │   └── Pipeline/        # 结算优先级引擎（按优先级顺序结算）
│   ├── Settlement/          # 结算核心逻辑（全量结算、结算日志生成）
│   ├── Context/             # 对局上下文（管理所有玩家状态、场景状态、回合信息）
│   └── Random/              # 固定种子伪随机数生成器（结算可复现）
│
├── Protocol/                # 通信协议（Protobuf定义 + 生成代码）
│   ├── Messages/            # 消息定义（请求/响应/通知）
│   └── Enums/               # 枚举定义（操作类型、卡牌类型、回合阶段等）
│
└── ConfigModels/            # 配置数据模型（前后端通用）
    ├── Card/                # 卡牌配置模型
    ├── Hero/                # 职业配置模型
    ├── Relic/               # 遗物配置模型
    └── Rule/                # 规则配置模型
```

## Unity客户端引用方式（UPM本地包）
本目录已配置为 Unity Package Manager (UPM) 本地包：
- `package.json` — UPM包描述文件
- 每个子目录包含 `.asmdef`（Assembly Definition）文件

Unity客户端通过 `Client/Packages/manifest.json` 中的相对路径引用：
```json
{
  "dependencies": {
    "com.cardmoba.shared": "file:../../Shared"
  }
}
```
Unity打开工程后，会自动识别 Shared 为本地包，代码即刻可用。

## ASP.NET Core后台引用方式
后台通过 .csproj 项目引用：
```xml
<ProjectReference Include="..\..\Shared\BattleCore\CardMoba.BattleCore.csproj" />
<ProjectReference Include="..\..\Shared\Protocol\CardMoba.Protocol.csproj" />
<ProjectReference Include="..\..\Shared\ConfigModels\CardMoba.ConfigModels.csproj" />
```

## 关键设计约束
1. **零外部依赖**：不依赖Unity引擎、ASP.NET Core框架，仅使用.NET Standard标准库
2. **无状态**：结算逻辑通过传入对局上下文（Context）进行计算，不持有任何全局状态
3. **组件化设计**：卡牌效果拆分为独立组件，通过配置表绑定，新增卡牌无需修改代码
4. **可复现**：固定种子随机数，保证结算逻辑可复现，方便排查问题
5. **noEngineReferences: true**：所有 .asmdef 均设置不引用Unity引擎，确保纯逻辑代码
