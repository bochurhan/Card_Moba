# Config - 游戏配置表

## 目录结构
```
Config/
├── Excel/           # 策划源文件（Excel格式，人工编辑）
│   ├── 卡牌配置表.xlsx
│   ├── 职业配置表.xlsx
│   ├── 遗物配置表.xlsx
│   └── 规则配置表.xlsx
│
└── Json/            # 导出文件（JSON格式，前后端使用）
    ├── Cards/       # 卡牌配置JSON
    ├── Heroes/      # 职业配置JSON
    ├── Relics/      # 遗物配置JSON
    └── Rules/       # 规则配置JSON
```

## 工作流
1. 在 `Excel/` 中编辑配置表
2. 使用 `Tools/ConfigTool` 或 `Tools/ExcelConverter` 转换为JSON
3. JSON文件同时部署到：
   - `Client/Assets/StreamingAssets/Config/`（客户端本地）
   - 服务端配置加载目录
4. 配置变更后，通过版本号机制触发客户端热更新

## 配置版本管理
- 每次导出自动生成版本号（时间戳+MD5）
- 客户端启动时对比版本号，自动拉取最新配置
- 支持回滚到历史版本
