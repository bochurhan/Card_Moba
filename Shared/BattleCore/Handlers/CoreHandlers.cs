
#pragma warning disable CS8632

using System.Collections.Generic;
using CardMoba.BattleCore.Foundation;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Managers;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Resolvers;
using CardMoba.Protocol.Enums;

namespace CardMoba.BattleCore.Handlers
{
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // DamageHandler 鈥斺€?澶勭悊 Damage / PierceDamage 鏁堟灉
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 浼ゅ Handler 鈥斺€?澶勭悊 EffectType.Damage 鍜?EffectType.PierceDamage銆?
    ///
    /// 鎵ц娴佺▼锛圓鈫払鈫扖 涓夐樁娈碉紝姣忔瀵瑰崟涓€鐩爣锛夛細
    ///   Phase A锛堝彧璇伙級锛氳绠椾慨姝ｅ悗浼ゅ鍊硷紝妫€鏌ユ棤鏁屻€?
    ///   Phase B锛堝啓鍏ワ級锛氭姢鐩惧惛鏀?鈫?HP 鎵ｅ噺锛岃褰曞疄闄呬激瀹抽噺銆?
    ///   Phase C锛堣Е鍙戯級锛欶ire(AfterDealDamage) + Fire(AfterTakeDamage)锛屾帹鍏?PendingQueue銆?
    ///
    /// 鈿狅笍 瀹氱瓥鐗?Layer 2 缁撶畻鏃讹紝姝?Handler 琚€愬紶璋冪敤锛堟寜鍑虹墝椤哄簭锛夛紝
    ///    姣忓紶鐗屽畬鏁磋蛋瀹?A-B-C 鍐嶅鐞嗕笅涓€寮狅紙宸辨柟椤哄簭渚濊禆璇箟锛夈€?
    /// </summary>
    public class DamageHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };
            bool isPierce = effect.Type == EffectType.Pierce;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);
            bool isDot = effect.Params.TryGetValue("isDot", out var isDotValue)
                && bool.TryParse(isDotValue, out var parsedIsDot)
                && parsedIsDot;
            bool isThorns = effect.Params.TryGetValue("isThorns", out var isThornsValue)
                && bool.TryParse(isThornsValue, out var parsedIsThorns)
                && parsedIsThorns;

            // 鍩虹浼ゅ鍊肩敱 HandlerPool 棰勮В鏋愬～鍏?effect.ResolvedValue锛堟敮鎸佸姩鎬佽〃杈惧紡锛?
            int baseDamage = effect.ResolvedValue;

            // 鈹€鈹€ 鏂藉鏂瑰嚭浼や慨姝ｏ紙鍔涢噺 Add / 铏氬急 Mul锛夆攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            // 淇锛氫紶 source.OwnerPlayerId锛屼笌 BuffManager 娉ㄥ唽淇鍣ㄦ椂浣跨敤鐨?ownerPlayerId 涓€鑷?
            int modifiedDamage = ctx.ValueModifierManager.Apply(
                effect.Type, source.OwnerPlayerId, ModifierScope.OutgoingDamage, baseDamage);

            foreach (var target in targets)
            {
                // 鈹€鈹€ Phase A锛氬彧璇绘牎楠?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
                if (!target.IsAlive) continue;

                // 鏃犳晫妫€鏌?
                if (target.IsInvincible)
                {
                    ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} is invincible; damage ignored.");
                    continue;
                }

                // 鈹€鈹€ Phase B锛氬啓鍏?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
                // 鍙楀嚮鏂瑰叆浼や慨姝ｏ紙鎶ょ敳 Add 璐熷€?/ 鏄撲激 Mul锛?
                // 淇锛氬崟鐙互 target.OwnerPlayerId 鏌ュ彈鍑绘柟淇锛屼笌鏂藉鏂逛慨姝ｅ垎寮€璺敱
                int incomingDamage = ctx.ValueModifierManager.Apply(
                    effect.Type, target.OwnerPlayerId, ModifierScope.IncomingDamage, modifiedDamage);

                int remaining = incomingDamage;
                int shieldAbsorbed = 0;
                int armorReduced = 0;

                // 鈹€鈹€ 闃插尽蹇収闅旂锛歀ayer 2 瀹氱瓥缁撶畻鏃惰蹇収锛屽叾浠栧満鏅紙鐬瓥绛夛級璇诲疄鏃跺€?鈹€鈹€
                // 蹇収鍦?Pre-Layer 2 鎷嶆憚锛屼唬琛?鏈疆瀹氱瓥寮€濮嬪墠"鐨勯槻寰＄姸鎬侊紝
                // 浣垮弻鏂瑰悇鑷殑鍙椾激璁＄畻涓嶅彈瀵规柟鍑虹墝椤哄簭褰卞搷銆?
                // 淇锛氭鍓嶇洿鎺ヨ target.Shield/Armor锛堝疄鏃跺€硷級锛屽鑷村揩鐓ф満鍒跺舰鍚岃櫄璁俱€?
                var targetPlayer = ctx.GetPlayer(target.OwnerPlayerId);
                var snapshot     = targetPlayer?.CurrentDefenseSnapshot;

                // 蹇収闃插尽鍊硷細Layer 2 瀹氱瓥鏃惰蹇収锛堜唬琛ㄦ湰杞?Layer 2 寮€濮嬪墠鐨勭姸鎬侊級锛?
                // 鏃犲揩鐓ф椂锛堢灛绛栫瓑鍦烘櫙锛夎瀹炴椂鍊笺€?
                // 鈿狅笍 蹇収鍊煎繀椤婚殢姣忔鍛戒腑閫掑噺锛屽惁鍒欏寮犱激瀹崇墝浼氶噸澶嶆秷璐瑰悓涓€浠芥姢鐩俱€?
                //    蹇収鍜屽疄鏃跺€煎悓姝ユ墸鍑忥紝淇濇寔璇箟涓€鑷淬€?
                //
                // 璁捐绾﹀畾锛堣 SettlementRules.md 搂Layer2 蹇収闅旂锛夛細
                //   Layer 2 鏈熼棿 AfterTakeDamage 绛夎Е鍙戝櫒鍔ㄦ€佺敓鎴愮殑鎶ょ浘浼氬啓鍏ュ疄鏃?target.Shield锛?
                //   浣嗗揩鐓т笉鏇存柊锛屽洜姝よ繖閮ㄥ垎鎶ょ浘鏈洖鍚堜笉鐢熸晥锛屼笅鍥炲悎鎵嶅弬涓庨槻寰°€?
                //   杩欐槸鏈夋剰涓轰箣鐨勮璁★細鍙椾激鍚庤幏寰楃殑鎶ょ浘浠ｈ〃"鎴樻枟缁忛獙绉疮"锛屼笅鍥炲悎鎵嶈浆鍖栦负闃插尽鍔涖€?
                int snapshotShield = snapshot != null ? snapshot.Shield : target.Shield;
                int snapshotArmor  = snapshot != null ? snapshot.Armor  : target.Armor;

                // 鎶ょ浘鐮磋鏍囪锛氭彁鍓嶅０鏄庯紝渚?Phase C 缁熶竴骞挎挱浣跨敤
                bool shieldBroken = false;

                // 鎶ょ浘鍚告敹锛堢┛閫忎激瀹充笉璺宠繃鎶ょ浘锛屽彧璺宠繃鎶ょ敳锛?
                if (snapshotShield > 0)
                {
                    shieldAbsorbed = remaining < snapshotShield ? remaining : snapshotShield;
                    // 鍚屾閫掑噺蹇収鍜屽疄鏃跺€硷紝闃叉鍚庣画鍛戒腑閲嶅娑堣垂
                    if (snapshot != null) snapshot.Shield -= shieldAbsorbed;
                    target.Shield -= shieldAbsorbed;
                    if (target.Shield < 0) target.Shield = 0;
                    remaining -= shieldAbsorbed;

                    // 鎶ょ浘鐮磋鍒ゆ柇锛氭湰娆″懡涓墠蹇収鏈夌浘锛屼笖鏈鍚告敹閲忚€楀敖浜嗗叏閮ㄥ揩鐓х浘鍊?
                    // snapshotShield 鏄€掑噺鍓嶇殑鍊硷紝shieldAbsorbed == snapshotShield 璇存槑鐩捐鎵撳厜
                    shieldBroken = shieldAbsorbed > 0 && shieldAbsorbed == snapshotShield;
                    if (shieldBroken)
                    {
                        ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} shield broken.");
                        ctx.TriggerManager.Fire(ctx, TriggerTiming.OnShieldBroken, new TriggerContext
                        {
                            SourceEntityId = source.EntityId,
                            TargetEntityId = target.EntityId,
                            Value          = shieldAbsorbed,
                        });
                        // 娉ㄦ剰锛氫笉鍦ㄦ澶勫箍鎾?DamageDealtEvent锛岀粺涓€鍦?Phase C 鏈熬鍚堝苟骞挎挱锛堥伩鍏嶉噸澶嶏級
                    }
                }

                // 鎶ょ敳鍑忎激锛堢┛閫忎激瀹宠烦杩囨姢鐢诧級
                if (!isPierce && snapshotArmor > 0 && remaining > 0)
                {
                    armorReduced = remaining < snapshotArmor ? remaining : snapshotArmor;
                    // 鍚屾閫掑噺蹇収鍜屽疄鏃跺€?
                    if (snapshot != null) snapshot.Armor -= armorReduced;
                    target.Armor -= armorReduced;
                    if (target.Armor < 0) target.Armor = 0;
                    remaining -= armorReduced;
                }

                // HP 鎵ｅ噺
                int realHpDamage = remaining > 0 ? remaining : 0;
                if (realHpDamage > 0)
                {
                    target.Hp -= realHpDamage;
                    result.TotalRealHpDamage += realHpDamage;
                    result.PerTargetValues[target.EntityId] = realHpDamage;
                }

                ctx.RoundLog.Add($"[DamageHandler] {source.EntityId} -> {target.EntityId}: base={baseDamage}, modified={modifiedDamage}, shieldAbsorbed={shieldAbsorbed}, armorReduced={armorReduced}, realHpDamage={realHpDamage}, hp={target.Hp}");

                // Phase C 缁熶竴骞挎挱锛堝惈 ShieldBroken 鏍囪锛岄伩鍏嶆姢鐩剧牬瑁傛椂閲嶅鍙戜袱鏉′簨浠讹級
                ctx.EventBus.Publish(new DamageDealtEvent
                {
                    SourceEntityId   = source.EntityId,
                    TargetEntityId   = target.EntityId,
                    BaseDamage       = modifiedDamage,
                    RealHpDamage     = realHpDamage,
                    ShieldAbsorbed   = shieldAbsorbed,
                    ArmorReduced     = armorReduced,
                    ShieldBroken     = shieldBroken,
                    IsDot            = isDot,
                    IsThorns         = isThorns,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                // 鈹€鈹€ Phase C锛氳Е鍙戝櫒 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
                // AfterDealDamage锛堟柦瀹虫柟瑙嗚锛?
                ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterDealDamage, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = realHpDamage,
                });

                // AfterTakeDamage锛堝彈瀹虫柟瑙嗚锛屾敞鎰?Source/Target 鏂瑰悜绾﹀畾锛?
                // SourceEntityId = 鍙楀鏂癸紝TargetEntityId = 鏂藉鏂癸紙璇﹁鏂囨。 搂4.5 绾﹀畾锛?
                ctx.TriggerManager.Fire(ctx, TriggerTiming.AfterTakeDamage, new TriggerContext
                {
                    SourceEntityId = target.EntityId,
                    TargetEntityId = source.EntityId,
                    Value          = realHpDamage,
                });

                // 濒死标记：HP <= 0 时仅标记，不在此处触发 OnNearDeath/OnDeath。
                // 死亡链路统一由 RoundManager.CheckDeathAndBattleOver 处理，
                // 避免同一次击杀重复触发濒死/复活/OnDeath 及战斗结束判定。
                if (!target.IsAlive && !target.DeathEventFired)
                {
                    ctx.RoundLog.Add($"[DamageHandler] {target.EntityId} HP <= 0, waiting for RoundManager death resolution.");
                }
            }

            return result;
        }

    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // HealHandler 鈥斺€?澶勭悊 Heal 鏁堟灉
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 娌荤枟 Handler 鈥斺€?澶勭悊 EffectType.Heal銆?
    /// 娌荤枟閲忎笉寰楄秴杩囩洰鏍?MaxHp锛屼笉瑙﹀彂浼ゅ鐩稿叧鐨勮Е鍙戝櫒銆?
    /// </summary>
    public class HealHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 娌荤枟閲忕敱 HandlerPool 棰勮В鏋愬～鍏?effect.ResolvedValue锛堟敮鎸佸姩鎬佽〃杈惧紡锛?
            int baseHeal = effect.ResolvedValue;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;

                int canHeal = target.MaxHp - target.Hp;
                int realHeal = baseHeal < canHeal ? baseHeal : canHeal;
                if (realHeal <= 0) continue;

                target.Hp += realHeal;
                result.TotalRealHeal += realHeal;
                result.PerTargetValues[target.EntityId] = realHeal;

                ctx.RoundLog.Add($"[HealHandler] {source.EntityId} 閳�?{target.EntityId}閿涙碍涓嶉悿?{realHeal}閿涘苯缍嬮崜宀篜={target.Hp}");

                ctx.EventBus.Publish(new HealEvent
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    RealHealAmount = realHeal,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnHealed, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = realHeal,
                });
            }

            return result;
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // ShieldHandler 鈥斺€?澶勭悊 Shield 鏁堟灉
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 鎶ょ浘 Handler 鈥斺€?澶勭悊 EffectType.Shield銆?
    /// 鎶ょ浘鍙犲姞鍒板綋鍓嶆姢鐩惧€硷紙涓嶈涓婇檺锛夛紝鍥炲悎缁撴潫鏃舵竻闆躲€?
    /// </summary>
    public class ShieldHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 鎶ょ浘閲忕敱 HandlerPool 棰勮В鏋愬～鍏?effect.ResolvedValue锛堟敮鎸佸姩鎬佽〃杈惧紡锛?
            int shieldAmount = effect.ResolvedValue;
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;

                target.Shield += shieldAmount;
                result.TotalRealShield += shieldAmount;
                result.PerTargetValues[target.EntityId] = shieldAmount;

                ctx.RoundLog.Add($"[ShieldHandler] {source.EntityId} 鈫?{target.EntityId}锛氳幏寰楁姢鐩?{shieldAmount}锛屽綋鍓嶆姢鐩?{target.Shield}");

                ctx.EventBus.Publish(new ShieldGainedEvent
                {
                    TargetEntityId = target.EntityId,
                    ShieldAmount   = shieldAmount,
                    SourceCardInstanceId = sourceCardInstanceId,
                });

                ctx.TriggerManager.Fire(ctx, TriggerTiming.OnGainShield, new TriggerContext
                {
                    SourceEntityId = source.EntityId,
                    TargetEntityId = target.EntityId,
                    Value          = shieldAmount,
                });
            }

            return result;
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // AddBuffHandler 鈥斺€?澶勭悊 AddBuff 鏁堟灉锛堥鏋讹級
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 闄勫姞 Buff Handler 鈥斺€?澶勭悊 EffectType.AddBuff銆?
    /// 浠?effect.Params["buffConfigId"] 璇诲彇閰嶇疆 ID锛岃皟鐢?BuffManager.AddBuff銆?
    /// </summary>
    public class AddBuffHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue("buffConfigId", out var buffConfigId))
            {
                ctx.RoundLog.Add("[AddBuffHandler] missing buffConfigId.");
                result.Success = false;
                return result;
            }

            // Buff 灞傛暟鐢?HandlerPool 棰勮В鏋愶紙鏀寔鍔ㄦ€佽〃杈惧紡锛夛紝duration 浠嶄粠 Params 璇诲彇闈欐€佸€?
            int value = effect.ResolvedValue;
            effect.Params.TryGetValue("duration", out var durationStr);
            int.TryParse(durationStr, out int duration);
            if (duration == 0) duration = -1; // 榛樿姘镐箙

            foreach (var target in targets)
            {
                if (!target.IsAlive) continue;
                ctx.BuffManager.AddBuff(ctx, target.EntityId, buffConfigId, source.EntityId, value, duration);
            }

            return result;
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // DrawCardHandler 鈥斺€?澶勭悊 DrawCard 鏁堟灉锛堥鏋讹級
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 鎶界墝 Handler 鈥斺€?澶勭悊 EffectType.DrawCard銆?
    /// </summary>
    public class DrawCardHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            // 鎶界墝鏁伴噺鐢?HandlerPool 棰勮В鏋愬～鍏?effect.ResolvedValue锛堟敮鎸佸姩鎬佽〃杈惧紡锛?
            int drawCount = effect.ResolvedValue;
            if (drawCount <= 0)
            {
                ctx.RoundLog.Add($"[DrawCardHandler] invalid drawCount={drawCount}, skipped.");
                result.Success = false;
                return result;
            }

            var drawn = ctx.CardManager.DrawCards(ctx, source.OwnerPlayerId, drawCount);
            result.Extra["drawnCount"] = drawn.Count;
            ctx.RoundLog.Add($"[DrawCardHandler] {source.OwnerPlayerId} drew {drawn.Count} card(s).");
            return result;
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // GenerateCardHandler 鈥斺€?澶勭悊 GenerateCard 鏁堟灉锛堥鏋讹級
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 鐢熸垚鍗＄墝 Handler 鈥斺€?澶勭悊 EffectType.GenerateCard銆?
    /// </summary>
    public class GainEnergyHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            int gainAmount = effect.ResolvedValue;
            if (gainAmount <= 0)
            {
                ctx.RoundLog.Add($"[GainEnergyHandler] 閺冪姵鏅ラ懗浠嬪櫤閸?{gainAmount}閿涘矁鐑︽潻鍥モ偓?");
                result.Success = false;
                return result;
            }

            int totalGained = 0;
            foreach (var target in targets)
            {
                var player = ctx.GetPlayer(target.OwnerPlayerId);
                if (player == null)
                    continue;

                player.Energy += gainAmount;
                totalGained += gainAmount;
                result.PerTargetValues[target.EntityId] = gainAmount;
                ctx.RoundLog.Add($"[GainEnergyHandler] {player.PlayerId} 閼惧嘲绶?{gainAmount} 閻愮鍏橀柌蹇ョ礉瑜版挸澧犻懗浠嬪櫤={player.Energy}/{player.MaxEnergy}");
            }

            result.Extra["gainedEnergy"] = totalGained;
            return result;
        }

    }

    public class GenerateCardHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult { EffectId = effect.EffectId, Type = effect.Type, Success = true };

            if (!effect.Params.TryGetValue("configId", out var configId))
            {
                ctx.RoundLog.Add("[GenerateCardHandler] missing configId.");
                result.Success = false;
                return result;
            }

            effect.Params.TryGetValue("targetZone", out var zoneStr);
            var zone = string.IsNullOrWhiteSpace(zoneStr)
                ? CardZone.Hand
                : zoneStr.Trim().ToLowerInvariant() switch
                {
                    "deck" => CardZone.Deck,
                    "discard" => CardZone.Discard,
                    "consume" => CardZone.Consume,
                    _ => CardZone.Hand,
                };
            effect.Params.TryGetValue("count", out var countStr);
            int count = int.TryParse(countStr, out var parsedCount) && parsedCount > 0 ? parsedCount : 1;
            bool tempCard = effect.Params.TryGetValue("tempCard", out var tempCardStr)
                && bool.TryParse(tempCardStr, out var parsedTempCard)
                && parsedTempCard;

            if (!string.IsNullOrWhiteSpace(effect.ValueExpression) && effect.ResolvedValue <= 0)
            {
                ctx.RoundLog.Add($"[GenerateCardHandler] {effect.EffectId} gate resolved to {effect.ResolvedValue}, skip generate {configId}.");
                result.Success = false;
                return result;
            }

            var effectiveTargets = targets.Count > 0 ? targets : new List<Entity> { source };
            var generatedIds = new List<string>();
            foreach (var target in effectiveTargets)
            {
                for (int i = 0; i < count; i++)
                {
                    var generatedCard = ctx.CardManager.GenerateCard(ctx, target.OwnerPlayerId, configId, zone, tempCard);
                    generatedIds.Add(generatedCard.InstanceId);
                }
            }

            result.Extra["generatedCount"] = generatedIds.Count;
            if (generatedIds.Count > 0)
            {
                result.Extra["generatedInstanceId"] = generatedIds[0];
                result.Extra["generatedInstanceIds"] = string.Join(",", generatedIds);
            }
            ctx.RoundLog.Add($"[GenerateCardHandler] generated {generatedIds.Count} x {configId} to {zone}, temp={tempCard}.");
            return result;
        }
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    // LifestealHandler 鈥斺€?澶勭悊 Lifesteal 鏁堟灉
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

    /// <summary>
    /// 鍚歌 Handler 鈥斺€?澶勭悊 EffectType.Lifesteal銆?
    ///
    /// 璇诲彇鍚屼竴寮犵墝鍓嶇疆 Damage / Pierce 鏁堟灉鐨勫疄闄?HP 浼ゅ鎬婚噺锛坧riorResults锛夛紝
    /// 鎸夐厤缃櫨鍒嗘瘮锛坋ffect.ResolvedValue锛変负鏂芥硶鑰呭洖澶嶇敓鍛藉€笺€?
    ///
    /// 璁捐绾﹀畾锛?
    ///   - Value=100 琛ㄧず 100% 鍚歌锛堝嵆"鏈鎶ょ浘鏍兼尅鐨勪激瀹冲叏閮ㄥ洖澶?锛夛紱
    ///   - 鍥炶閲忎笂闄愪负鏂芥硶鑰呭綋鍓嶇己澶辩敓鍛斤紝涓嶄細瓒呰繃 MaxHp锛?
    ///   - 浠呯疮璁″墠缃晥鏋滀腑绫诲瀷涓?Damage / Pierce 鐨?TotalRealHpDamage锛?
    ///     鎶ょ浘鍚告敹閮ㄥ垎涓嶈鍏ワ紙浣撶幇"鏈鎶ょ浘鏍兼尅"璇箟锛夈€?
    /// </summary>
    public class LifestealHandler : IEffectHandler
    {
        public EffectResult Execute(
            BattleContext ctx,
            EffectUnit effect,
            Entity source,
            List<Entity> targets,
            List<EffectResult> priorResults,
            TriggerContext? triggerContext)
        {
            var result = new EffectResult
            {
                EffectId = effect.EffectId,
                Type     = effect.Type,
                Success  = true,
            };

            // 鈹€鈹€ 绱鍓嶇疆 Damage / Pierce 鏁堟灉閫犳垚鐨勫疄闄?HP 浼ゅ 鈹€鈹€鈹€鈹€鈹€鈹€
            effect.Params.TryGetValue("sourceCardInstanceId", out var sourceCardInstanceId);
            int totalHpDamage = 0;
            foreach (var prior in priorResults)
            {
                if (prior.Success &&
                    (prior.Type == EffectType.Damage || prior.Type == EffectType.Pierce))
                {
                    totalHpDamage += prior.TotalRealHpDamage;
                }
            }

            if (totalHpDamage <= 0)
            {
                ctx.RoundLog.Add("[LifestealHandler] no prior HP damage; skipped.");
                result.Success = false;
                return result;
            }

            // 鈹€鈹€ 鎸夌櫨鍒嗘瘮璁＄畻鍥炶閲忥紝涓嶈秴杩囩己澶辩敓鍛?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            int healAmount = totalHpDamage * effect.ResolvedValue / 100;
            int missing    = source.MaxHp - source.Hp;
            healAmount     = healAmount < missing ? healAmount : missing;

            if (healAmount <= 0)
            {
                ctx.RoundLog.Add($"[LifestealHandler] {source.EntityId} is already at full HP.");
                return result;
            }

            source.Hp           += healAmount;
            result.TotalRealHeal = healAmount;

            ctx.RoundLog.Add(
                $"[LifestealHandler] {source.EntityId} 鍚歌 {healAmount} HP" +
                $"锛堝疄闄呬激瀹?{totalHpDamage}脳{effect.ResolvedValue}%锛夛紝褰撳墠HP={source.Hp}/{source.MaxHp}");

            ctx.EventBus.Publish(new HealEvent
            {
                SourceEntityId = source.EntityId,
                TargetEntityId = source.EntityId,
                RealHealAmount = healAmount,
                SourceCardInstanceId = sourceCardInstanceId,
            });

            // 瑙﹀彂 OnHealed 鏃舵満锛堜笌 HealHandler 淇濇寔涓€鑷达紝浣夸緷璧栨鏃舵満鐨?Buff 鑳藉搷搴斿惛琛€鍥炶锛?
            ctx.TriggerManager.Fire(ctx, TriggerTiming.OnHealed, new TriggerContext
            {
                SourceEntityId = source.EntityId,
                TargetEntityId = source.EntityId,
                Value          = healAmount,
            });

            return result;
        }
    }
}
