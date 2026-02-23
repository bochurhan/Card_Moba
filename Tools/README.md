# Tools - 辅助工具

## 工具列表

### 1. ConfigTool - 配置表生成与校验工具
- **技术栈**：.NET Core 控制台应用 + EPPlus
- **核心功能**：
  - Excel转JSON：一键将策划Excel配置转换为前后端通用JSON
  - 配置一致性校验：对比前后端配置版本号/MD5值
  - 配置版本管理：自动生成版本号，支持回滚
  - 热更包生成：一键生成配置热更包
  - 自动生成C#结构体代码
- **目录结构**：
  ```
  ConfigTool/
  ├── src/          # 工具源代码
  └── Templates/    # 代码生成模板
  ```

### 2. ExcelConverter - Excel转换工具
- **技术栈**：.NET Core + EPPlus
- **核心功能**：
  - 读取Excel多Sheet
  - 支持复杂表格结构
  - 内置校验规则，自动拦截非法配置
- **目录结构**：
  ```
  ExcelConverter/
  └── src/          # 工具源代码
  ```

### 3. Unity内置工具（位于Client/Assets/Scripts/Editor/）
- **卡牌编辑器**：可视化卡牌配置、预览、校验、批量导出
- **单机对局模拟器**：无需启动后台，复用结算库模拟完整对局

## 开发周期
辅助工具总开发周期：1-2周
