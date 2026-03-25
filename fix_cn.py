import os

BASE = r'd:\Card_Moba'

files = [
    'Shared/BattleCore/Core/RoundManager.cs',
    'Shared/BattleCore/Managers/CardManager.cs',
    'Shared/BattleCore/Managers/TriggerManager.cs',
    'Shared/BattleCore/Foundation/CardZone.cs',
    'Shared/BattleCore/Foundation/TriggerTiming.cs',
    'Shared/BattleCore/Foundation/BattleCard.cs',
    'Shared/BattleCore/Foundation/Entity.cs',
    'Shared/BattleCore/Context/BattleContext.cs',
    'Shared/BattleCore/Core/BattleFactory.cs',
]

replacements = [
    # RoundManager 日志
    ('[RoundManager] Battle initialized.', '[RoundManager] 战斗初始化完成。'),
    ('Round {CurrentRound} begins.', '第 {CurrentRound} 回合开始。'),
    ('Round {CurrentRound} start processing completed.', '第 {CurrentRound} 回合开始处理完毕。'),
    ('Round {CurrentRound} settlement begins.', '第 {CurrentRound} 回合结算开始。'),
    ('No plan cards to resolve this round.', '本回合无定策牌，跳过定策结算。'),
    ('[RoundManager] Player {deadId} died.', '[RoundManager] 玩家 {deadId} 死亡！'),
    ('[RoundManager] Player {deadId} revived.', '[RoundManager] 玩家 {deadId} 被复活！'),
    ('[RoundManager] Battle ended in a draw.', '[RoundManager] 战斗结束：平局！'),
    ('[RoundManager] Battle ended. Winner={WinnerId}.', '[RoundManager] 战斗结束：玩家 {WinnerId} 获胜！'),
    ('Round {CurrentRound} ends.', '第 {CurrentRound} 回合结束。'),
    ('Clear end-of-round shield for {kv.Key}: {shield} -> 0.', '{kv.Key} 回合结束护盾清零（{shield} → 0）。'),
    ('[RoundManager] Missing instant card instance', '[RoundManager] ⚠️ 找不到瞬策牌实例'),
    ('Instant card {cardInstanceId} does not belong to {playerId}.', '⚠️ 瞬策牌 {cardInstanceId} 不属于玩家 {playerId}。'),
    ('Missing card definition for instant card {cardInstanceId} ({card.ConfigId}).', '⚠️ 找不到瞬策牌 {cardInstanceId}（{card.ConfigId}）的卡牌定义。'),
    ('[RoundManager] Player {playerId} plays instant card {cardInstanceId} via legacy effect path.', '[RoundManager] 玩家 {playerId} 通过兼容路径打出瞬策牌 {cardInstanceId}。'),
    ('[RoundManager] Player {playerId} plays instant card {cardInstanceId}.', '[RoundManager] 玩家 {playerId} 打出瞬策牌 {cardInstanceId}。'),
    ('[RoundManager] Missing plan card instance', '[RoundManager] ⚠️ 找不到定策牌实例'),
    ('Plan card {planCard.CardInstanceId} does not belong to {planCard.PlayerId}.', '⚠️ 定策牌 {planCard.CardInstanceId} 不属于玩家 {planCard.PlayerId}。'),
    ('Missing card definition for plan card {planCard.CardInstanceId} ({card.ConfigId}).', '⚠️ 找不到定策牌 {planCard.CardInstanceId}（{card.ConfigId}）的卡牌定义。'),
    ('Plan card {planCard.CardInstanceId} failed validation.', '⚠️ 定策牌 {planCard.CardInstanceId} 校验失败，拒绝提交。'),
    ('Player {planCard.PlayerId} commits plan card {planCard.CardInstanceId} at order {planCard.SubmitOrder}.', '玩家 {planCard.PlayerId} 提交定策牌 {planCard.CardInstanceId}（顺序={planCard.SubmitOrder}）。'),
    # CardManager 英文日志
    ('state card {cardInstanceId} cannot be committed as a plan card.', '状态牌 {cardInstanceId} 不能作为定策牌提交。'),
    ('state card {cardInstanceId} cannot be played directly.', '状态牌 {cardInstanceId} 不能直接打出。'),
    # TriggerManager
    ('Trigger {trigger.TriggerId} expired.', '触发器 {trigger.TriggerId}（{trigger.TriggerName}）已到期，自动注销。'),
    # CardZone 注释
    ('// Legacy compatibility zone. Mainline state-card behavior is driven by BattleCard.IsStatCard plus normal card zones.',
     '// 遗留兼容区域。当前主流程状态牌行为由 BattleCard.IsStatCard 标记驱动，不依赖此区域。'),
    # TriggerTiming 注释
    ('// Fired when CardManager scans hand-held state cards at end of round.',
     '// 由 CardManager 在回合末扫描手牌中的状态牌时触发。'),
    # BattleContext 注释
    ('/// BattleCore 只依赖效果列表和少量生命周期标记。',
     '/// BattleCore 只依赖可执行效果列表和少量生命周期标记。'),
    # BattleFactory 注释
    ('/// 用于 BattleCore 在实例合法后自行解析效果列表和生命周期标记。',
     '/// BattleCore 在实例合法性校验通过后，通过此委托自行解析效果列表和生命周期标记（IsExhaust/IsStatCard）。'),
]

total_changed = 0
for fpath in files:
    full = os.path.join(BASE, fpath.replace('/', os.sep))
    try:
        with open(full, 'r', encoding='utf-8') as f:
            text = f.read()
        original = text
        for old, new in replacements:
            text = text.replace(old, new)
        if text != original:
            with open(full, 'w', encoding='utf-8', newline='') as f:
                f.write(text)
            total_changed += 1
            print(f'Updated: {fpath}')
    except Exception as e:
        print(f'ERROR {fpath}: {e}')

print(f'\nDone. {total_changed} files updated.')
