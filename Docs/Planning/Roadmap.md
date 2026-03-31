# 开发路线图

**最后更新**：2026-03-31

## 阶段 A：BattleCore 基础能力

已完成：

- 单场战斗主流程
- 回合推进
- 出牌与结算
- `BattleRuleset`
- 队伍共享目标
- `BattleSummary`

对应核心目录：

- `Shared/BattleCore/`

## 阶段 B：MatchFlow 整局流程

已完成骨架：

- 多场战斗 `4+1`
- 跨战斗状态继承
- 场间构筑窗口
- 构筑动作与奖励池基础
- 装备被动基础

对应核心目录：

- `Shared/MatchFlow/`

## 阶段 C：localhost 1v1 联机 MVP

当前阶段：

- 服务端 `ASP.NET Core + SignalR`
- 权威 `MatchSession`
- 共享协议 DTO
- 客户端联机 Runtime
- UI 通过 `IBattleClientRuntime` 同时支持本地与联机

当前目标：

- 跑通双客户端联调
- 收口构筑与日志展示
- 收口回合锁定与可见度

## 阶段 D：联机能力补强

计划内容：

- 服务端权威事件流
- 操作期倒计时与超时推进
- 更完整的断线处理
- 重连恢复
- 房间生命周期清理

## 阶段 E：2v2 与长期系统

计划内容：

- 2v2 同场战斗
- 团队可见度规则
- 多职业与更多装备
- 匹配
- 持久化
- 回放
- 公网部署
