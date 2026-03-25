# BattleCore V2 閺嬭埖鐎拠瀛樻

**閺傚洦銆傞悧鍫熸拱**: 2026-03-25  
**閻樿埖鈧?*: 瑜版挸澧犳總鎴犲  
**闁倻鏁ら懠鍐ㄦ纯**: `Shared/BattleCore` 瑜版挸澧犳潻鎰攽閺冭泛鐤勯悳?
---

## 1. 閺傚洦銆傞惄顔界垼

閺堫剚鏋冨锝呭涧閹诲繗鍫鑼病閽€钘夋勾閻?BattleCore V2 鏉╂劘顢戦弮鑸电仸閺嬪嫨鈧?
- 鐎瑰啩绗夐弰顖氬坊閸欏弶鏌熷鍫熺湽閹?- 鐎瑰啩绗夐弰顖涙弓閺夈儴顫夐崚?- 婵″倹鐏夐弬鍥ㄣ€傛稉搴濆敩閻礁鍟跨粣渚婄礉鎼存柧绱崗鍫滄叏濮濓絾鏋冨锝嗗灗缂佈呯敾閺€璺哄經娴狅絿鐖滈敍灞肩瑝閸忎浇顔忛梹鎸庢埂閸欏矁寤?

閺堫剛澧楅柌宥囧仯缂佺喍绔存禒銉ょ瑓閸欙絽绶為敍?
- `BuffManager` 閺?Buff 閸烆垯绔存潻鎰攽閺冨墎婀″┃?- 閻樿埖鈧胶澧濋弰顖涙珮闁?`BattleCard` 鐎圭偘绶ラ敍灞肩瑝娓氭繆绂嗘０婵嗩樆娑撴挾鏁ら崠杞板瘜濞翠胶鈻?
- 閻剛鐡ラ悧灞芥嫲鐎规氨鐡ラ悧宀勫厴閸╄桨绨?`cardInstanceId` 娑撳骸宕遍悧灞界暰娑斿澧界悰?- Buff 鐎涙劖鏅ラ弸婊堚偓姘崇箖 `TriggerManager -> PendingEffectQueue -> SettlementEngine -> Handler` 閹笛嗩攽
- Layer 2 鐎规氨鐡ユ导銈咁唺娴ｈ法鏁ら梼鎻掑敖韫囶偆鍙?

---

## 2. 閹缍嬬紒鎾寸€?

BattleCore 瑜版挸澧犻崣顖欎簰閹峰棙鍨?4 鐏炲偊绱?

1. 鐠嬪啫瀹崇仦?   - `RoundManager`
   - `SettlementEngine`
2. 娑撴艾濮熺粻锛勬倞鐏?   - `CardManager`
   - `BuffManager`
   - `TriggerManager`
   - `ValueModifierManager`
3. 閸欘亣顕扮憴锝嗙€界仦?   - `TargetResolver`
   - `ConditionChecker`
   - `DynamicParamResolver`
4. 閺佺増宓佺仦?   - `BattleContext`
   - `Entity`
   - `BattleCard`
   - `EffectUnit`
   - `TriggerUnit`
   - `BuffUnit`

娓氭繆绂嗛弬鐟版倻娣囨繃瀵旈崡鏇炴倻閿?
- `RoundManager` 鐠嬪啰鏁?`SettlementEngine` 閸滃苯鎮?Manager
- `TriggerManager` 娑撳秶娲块幒銉ㄧ殶閻?`SettlementEngine`
- `TriggerManager` 閸欘亣绀嬬拹锝嗗Ω閺佸牊鐏夐崗銉╂Е
- `EventBus` 閸欘亜顕径鏍х畭閹绢叏绱濇稉宥呭棘娑撳孩鍨弬妤€鍨界€?
---

## 3. 閸烆垯绔撮惇鐔哥爱

### 3.1 BattleContext

`BattleContext` 閺勵垯绔寸仦鈧幋妯绘灍閻ㄥ嫬鏁稉鈧潻鎰攽閺冭泛顔愰崳顭掔礉閹镐焦婀侀敍?
- `BattleId`
- `CurrentRound`
- `CurrentPhase`
- 閹碘偓閺堝甯虹€硅泛鎷伴懟閬嶆碂鐎圭偘缍?
- `PendingEffectQueue`
- `RoundLog`
- 鏉╂劘顢戦弮鏈电贩鐠ф牜娈戦崥?Manager
- 閸楋紕澧濈€规矮绠熼幓鎰返閸?`CardDefinitionProvider`

### 3.2 Buff 閻喐绨?

Buff 瑜版挸澧犻惃鍕暜娑撯偓鏉╂劘顢戦弮鍓佹埂濠ф劖妲?`BuffManager` 閸愬懘鍎寸€涙ê鍋嶉妴?
婢舵牠鍎撮懟銉洣鐠囪褰?Buff閿涘苯绻€妞ゆ槒铔嬮敍?
- `IBuffManager.GetBuffs(entityId)`
- `IBuffManager.HasBuff(...)`

娴犮儰绗呴崑姘《闁垝绗夐崘宥嗘Ц瑜版挸澧犳總鎴犲閿?
- 娴?`Entity.ActiveBuffs` 鐠囪褰?Buff
- 娓氭繆绂嗛悳鈺侇啀閹存牕鐤勬担鎾叉櫠閻?Buff 闂€婊冨剼娴ｆ粈璐熺紒鎾剁暬娓氭繃宓?

`Entity.ActiveBuffs` 瀹歌弓绮犳潻鎰攽閺冭埖膩閸ㄥ些闂勩們鈧?
### 3.3 瀹告彃鍨归梽銈囨畱闁鏆€閻樿埖鈧?
娴犮儰绗呴柆妤冩殌妞ょ懓鍑℃稉宥呭晙鐏炵偘绨ぐ鎾冲鐎圭偟骞囬敍?
- `BattleContext.HistoryLog`
- `BattleContext.ArchiveRoundLog()`
- `TriggerUnit.InlineExecute`
- `Entity.ActiveBuffs`

---

## 4. 閸楋紕澧濆Ο鈥崇€?

### 4.1 BattleCard

閹碘偓閺堝澧濋崷銊﹀灛閺傛鍞撮柈鎴掍簰 `BattleCard` 鐎圭偘绶ョ€涙ê婀妴?
閸忔娊鏁€涙顔岄敍?
- `InstanceId`
- `ConfigId`
- `OwnerId`
- `Zone`
- `TempCard`
- `IsExhaust`
- `IsStatCard`

### 4.2 閸楋紕澧濈€规矮绠?

BattleCore 娑撳秶娲块幒銉ょ贩鐠ф牔绗傜仦鍌氱暚閺佹挳鍘ょ悰銊δ侀崹瀣ㄢ偓?
鏉╂劘顢戦弮鍫曗偓姘崇箖 `BattleCardDefinition` 閸欐牗娓剁亸蹇撶箑鐟曚椒淇婇幁顖ょ窗

- `ConfigId`
- `IsExhaust`
- `IsStatCard`
- `Effects`

鐠嬪啰鏁ら柧鎾呯窗

- `RoundManager` 闁俺绻?`card.ConfigId`
- 閸?`BattleContext.CardDefinitionProvider` 閺屻儴顕楃€规矮绠?
- 閸愬秶鏁?`EffectUnitCloner` 閸忓娈曢崣顖涘⒔鐞涘本鏅ラ弸婊冨灙鐞?
### 4.3 閸楋紕澧濋崠杞扮秴

瑜版挸澧犻崠杞扮秴閺嬫矮濡囨穱婵堟殌閿?
- `Deck`
- `Hand`
- `StrategyZone`
- `Discard`
- `Consume`

娴ｅ棗缍嬮崜宥勫瘜濞翠胶鈻奸崣顏冪贩鐠ф牕澧?5 娑擃亗鈧?
---

## 5. 閻樿埖鈧胶澧濆Ο鈥崇€?

閻樿埖鈧胶澧濊ぐ鎾冲鐎规矮绠熸稉鐚寸窗

- 閺咁噣鈧?`BattleCard` 鐎圭偘绶?
- `IsStatCard = true`
- 閸欘垯浜掗崙铏瑰箛閸︺劎澧濋崼鍡愨偓浣瑰閻楀被鈧礁绱旈悧灞界垻

瑜版挸澧犳潻鎰攽閺冩儼顫夐崚娆欑窗

- 閻樿埖鈧胶澧濇稉宥堝厴娑撹濮╅幍鎾茶礋閻剛鐡ラ悧?- 閻樿埖鈧胶澧濇稉宥堝厴娑撹濮╅幓鎰唉娑撳搫鐣剧粵鏍
- 閸ョ偛鎮庣紒鎾存将閺冨墎鏁?`CardManager.ScanStatCards()` 閹殿偅寮块幍瀣娑擃厾娈戦悩鑸碘偓浣哄
- 閹殿偅寮块弮鎯靶曢崣?`OnStatCardHeld`
- 閹殿偅寮跨紒鎾存将閸氬函绱濋悩鑸碘偓浣哄閸滃苯鍙炬禒鏍ㄥ閻楀奔绔寸挧宄版躬閸ョ偛鎮庨張顐ョ箻閸忋儱绱旈悧灞界垻

閸ョ姵顒濋敍宀€濮搁幀浣哄瑜版挸澧犻惃鍕獓閸濅浇顕㈡稊澶嬫Ц閿?
- 鐎瑰啯妲告稉鈧粔宥囧濞堝﹤宕遍悧宀€琚崹?- 鐎瑰啴鈧俺绻冮垾婊勫瘮閺堝鍩岄崶鐐叉値閺堫偀鈧繄鏁撻弫?- 鐎瑰啩绗夐弰顖滃缁斿灏担宥夆攳閸斻劎娈戠€涙劗閮寸紒?
---

## 6. 閸ョ偛鎮庢す鍗炲З

### 6.1 InitBattle

`RoundManager.InitBattle()` 鐠愮喕鐭楅敍?
- 闁插秶鐤嗛崶鐐叉値閸?- 濞撳懐鈹栧鍛波缁犳鐣剧粵鏍Е閸?- 闁插秶鐤嗛懗婊嗙閻樿埖鈧?- 閸欐垵绔?`BattleStartEvent`

### 6.2 BeginRound

`RoundManager.BeginRound()` 瑜版挸澧犳い鍝勭碍閿?
1. `CurrentRound += 1`
2. 閸愭瑥鍙?`BattleContext.CurrentRound`
3. `CurrentPhase = RoundStart`
4. 閸欐垵绔?`RoundStartEvent`
5. 鐟欙箑褰?`OnRoundStart`
6. 濞戝牆瀵?`PendingEffectQueue`
7. 閹笛嗩攽 `CardManager.OnRoundStart()`
8. 閸愬秵顐煎☉鍫濆 `PendingEffectQueue`
9. `CurrentPhase = PlayerAction`

`CardManager.OnRoundStart()` 瑜版挸澧犵拹鐔荤煑閿?
- 閸ョ偞寮ч懗浠嬪櫤
- 濮ｅ繋缍呴悳鈺侇啀閹?5 瀵姷澧?

### 6.3 PlayerAction

閻溾晛顔嶇悰灞藉З闂冭埖顔岄崗浣筋啅閿?
- 閹垫挸鍤惉顒傜摜閻?- 閹绘劒姘︾€规氨鐡ラ悧?
娑撱倛鈧懘鍏樿箛鍛淬€忛崺杞扮艾閻喎鐤?`cardInstanceId`閵?
### 6.4 EndRound

`RoundManager.EndRound()` 瑜版挸澧犳い鍝勭碍閿?
1. `CurrentPhase = Settlement`
2. 閹殿偅寮块幍瀣娑擃厾娈戦悩鑸碘偓浣哄
3. 濞戝牆瀵查悩鑸碘偓浣哄鐟欙箑褰傛禍褏鏁撻惃鍕Е閸?4. 濮濊楠稿Λ鈧弻?5. 缂佹挾鐣婚幍鈧張澶婄暰缁涙牜澧?
6. 閸愬秵顐煎璁抽濡偓閺?7. 鐟欙箑褰?`OnRoundEnd`
8. 濞戝牆瀵查梼鐔峰灙
9. 閹笛嗩攽 `BuffManager.OnRoundEnd()`
10. 閸愬秵顐煎璁抽濡偓閺?11. 濞撳懐鈹栭崜鈺€缍戦幎銈囨禈
12. 閹笛嗩攽 `TriggerManager.TickDecay()`
13. 閹笛嗩攽 `CardManager.OnRoundEnd()`
14. 閹笛嗩攽 `CardManager.DestroyTempCards()`
15. `CurrentPhase = RoundEnd`
16. 閸欐垵绔?`RoundEndEvent`

---

## 7. 閻剛鐡ラ悧宀冪熅瀵?
閻剛鐡ラ悧灞界秼閸撳秳寮楅弽鑹拌泲鐎圭偘绶ョ捄顖氱窞閿?
1. `RoundManager.PlayInstantCard(ctx, playerId, cardInstanceId)`
2. 閺嶏繝鐛欓崡鈥崇摠閸?3. 閺嶏繝鐛欒ぐ鎺戠潣
4. 闁俺绻?`card.ConfigId` 鐟欙絾鐎?`BattleCardDefinition`
5. 缂佹瑦鏅ラ弸婊冨晸閸忋儲娼靛┃鎰帗閺佺増宓?
6. 鏉╂稑鍙?`SettlementEngine.ResolveInstantFromCard(...)`

`SettlementEngine.ResolveInstantFromCard(...)` 瑜版挸澧犳い鍝勭碍閿?
1. 鐟欙箑褰?`BeforePlayCard`
2. 濞戝牆瀵查梼鐔峰灙
3. 濡偓閺屻儲鐭囨?4. `CardManager.PrepareInstantCard()` 鐎瑰本鍨氶崥鍫熺《閹勭墡妤犲奔绗岄崠杞扮秴鏉╀胶些
5. 闁劒閲滈幍褑顢戦弫鍫熺亯
6. 濮ｅ繋閲滈弫鍫熺亯閸氬海鐝涢崡铏Х閸栨牠妲﹂崚?7. 鐟欙箑褰?`AfterPlayCard`
8. 閸愬秵顐煎☉鍫濆闂冪喎鍨?
9. 閸欐垵绔?`CardPlayedEvent`

瑜版挸澧犲鎻掑灩闂勩倗娈戦弮褑鐭惧鍕剁窗

- 閻╁瓨甯撮幎濠咃紭 `List<EffectUnit>` 閸犲倻绮?`RoundManager.PlayInstantCard(...)`
- 閻╁瓨甯撮幎濠咃紭 `List<EffectUnit>` 閸犲倻绮?`SettlementEngine.ResolveInstant(...)`

---

## 8. 鐎规氨鐡ラ悧宀冪熅瀵?
鐎规氨鐡ラ悧灞界秼閸撳秳绡冭箛鍛淬€忛崺杞扮艾閻喎鐤?`cardInstanceId`閵?
閹绘劒姘﹂弮璁圭窗

1. `RoundManager.CommitPlanCard(...)`
2. 閺嶏繝鐛欓崡鈥崇摠閸︺劋绗岃ぐ鎺戠潣
3. 闁俺绻?`card.ConfigId` 鐟欙絾鐎介崡锛勫鐎规矮绠?
4. 缂佹瑦鏅ラ弸婊冨晸閸忋儲娼靛┃鎰帗閺佺増宓?
5. 鐠嬪啰鏁?`CardManager.CommitPlanCard(...)`
6. 閸愭瑥鍙嗗鍛波缁犳妲﹂崚?
缂佹挾鐣婚弮璁圭窗

- `SettlementEngine.ResolvePlanCards(...)`
- 缂佺喍绔撮幐澶夌安鐏炲倻绮ㄧ粻?
### 娴滄柨鐪扮紒鎾剁暬

1. `Counter`
2. `Defense`
3. `Damage`
4. `Resource`
5. `BuffSpecial`

閸忔湹鑵?Layer 2 閸撳秴鎮楅張澶夎⒈娑擃亪娈ｅ蹇旑劄妤犮倧绱?

- `TakeDefenseSnapshots()`
- `ClearDefenseSnapshots()`

---

## 9. Layer 2 闂冩彃灏借箛顐ゅ弾

Layer 2 韫囶偆鍙庨惃鍕窗閺嶅洦妲搁梾鏃傤瀲閳ユ粍婀版潪顔肩暰缁涙牔婵€鐎瑰啿鐔€缁惧潡妲诲鈾€鈧縿鈧?
瑜版挸澧犵憴鍕灟閿?
- 閸?Layer 2 瀵偓婵澧犻敍灞艰礋濮ｅ繋缍呴悳鈺侇啀閹峰秳绗?`Shield / Armor / IsInvincible`
- `DamageHandler` 閸︺劌鐣剧粵?Layer 2 娑擃叀顕伴崣鏍ф彥閻撗囨Щ瀵扳€斥偓?- 濮ｅ繑顐奸崨鎴掕厬閸氬本妞傞柅鎺戝櫤韫囶偆鍙庨崐鐓庢嫲鐎圭偞妞傞崐?- 闂冨弶顒涢崥灞界湴婢舵碍顐奸崨鎴掕厬闁插秴顦插☉鍫ｅ瀭閸氬奔绔存禒鑺ュБ閻╃偓鍨ㄩ幎銈囨暢

瑜版挸澧犻弰搴ｂ€樼痪锕€鐣鹃敍?
- Layer 2 閺堢喖妫块弬鎷屽箯瀵版娈戦幎銈囨禈閿涘苯褰ч崘娆忕杽閺冭泛鈧?- 娑撳秳绱伴崶鐐插晸閸掓澘缍嬮崜宥呮彥閻?- 閸ョ姵顒濇稉宥呭棘娑撳孩婀版潪?Layer 2 閸氬海鐢婚梼鎻掑敖
- 娴兼艾婀稉瀣╃鏉烆喖绱戞慨瀣娴ｆ粈璐熼弬鎵畱鐎圭偞妞傞梼鎻掑敖閸欏倷绗岀紒鎾剁暬

---

## 10. Buff 娑撳氦袝閸欐垶澧界悰宀勬懠

### 10.1 BuffManager 閼卞矁鐭?

`BuffManager` 鐠愮喕鐭楅敍?
- Buff 閻ㄥ嫬顤冮崚鐘虫暭閺?- Buff 鐟欙箑褰傞崳銊︽暈閸愬奔绗屽▔銊╂敘
- Buff 閺佹澘鈧棿鎱ㄥ锝呮珤濞夈劌鍞芥稉搴㈡暈闁库偓
- Buff 閸ョ偛鎮庣悰鏉垮櫤

鐎瑰啩绗夐惄瀛樺复閸嬫岸鈧帒缍婄紒鎾剁暬閵?
### 10.2 TriggerManager 閼卞矁鐭?

`TriggerManager` 鐠愮喕鐭楅敍?
- 缂佸瓨濮?`TriggerUnit`
- 娓氭繃宓?`TriggerTiming` 閹垫儳鍩岄崣顖澬曢崣鎴︺€?
- 閸嬫碍娼禒璺哄灲閺?- 閸忓娈?`EffectUnit`
- 閹跺﹥鏅ラ弸婊冾敚鏉?`PendingEffectQueue`

鐎瑰啩绗夐崘宥嗘暜閹?`InlineExecute`閵?
### 10.3 缂佺喍绔撮幍褑顢戦柧?
瑜版挸澧犻幍鈧張?Buff 鐎涙劖鏅ラ弸婊堝厴鎼存棁铔嬬紒鐔剁闁炬崘鐭鹃敍?
`TriggerManager.Fire()`  
-> `PendingEffectQueue.Enqueue()`  
-> `SettlementEngine.DrainPendingQueue()`  
-> `HandlerPool.Execute()`  
-> 鐎电懓绨?Handler 閺€鐟板晸鏉╂劘顢戦弮鍓佸Ц閹?
鏉╂瑩鈧倻鏁ゆ禍搴窗

- DoT
- Regeneration
- Buff Lifesteal
- Thorns
- 娴犮儱寮烽崗鏈电铂閻?Trigger 濞插墽鏁撻惃鍕摍閺佸牊鐏?

---

## 11. 閺佹澘鈧棿鎱ㄥ?
鏉╂劘顢戦弮鑸垫殶閸婇棿鎱ㄥ锝夆偓姘崇箖 `ValueModifierManager` 鐎瑰本鍨氶妴?
瑜版挸澧犻弨顖涘瘮娑撱倓閲滈弬鐟版倻閿?
- `OutgoingDamage`
- `IncomingDamage`

瑜版挸澧犻弰鐘茬殸鐟欏嫬鍨敍?
- `Strength` / `Weak` -> 閸戣桨婵€娣囶喗顒?
- `Armor` / `Vulnerable` -> 閸忋儰婵€娣囶喗顒?
- `Armor` 閸欘亜濂栭崫?`Damage`
- `Vulnerable` 閸氬本妞傝ぐ鍗炴惙 `Damage` 娑?`Pierce`

鎼存梻鏁ゆい鍝勭碍閸ュ搫鐣鹃敍?
- `Add`
- `Mul`
- `Set`

---

## 12. 鐟欙箑褰傞弮鑸垫簚閻滄壆濮?

### 瀹稿弶甯寸痪璺ㄦ畱閺冭埖婧€

- `OnRoundStart`
- `OnRoundEnd`
- `BeforePlayCard`
- `AfterPlayCard`
- `AfterDealDamage`
- `AfterTakeDamage`
- `OnShieldBroken`
- `OnNearDeath`
- `OnDeath`
- `OnBuffAdded`
- `OnBuffRemoved`
- `OnCardDrawn`
- `OnStatCardHeld`
- `OnHealed`
- `OnGainShield`

### 瀹告彃鐣炬稊澶夌稻瑜版挸澧犻張顏呭复缁?
- `BeforeDealDamage`
- `BeforeTakeDamage`

鏉╂瑤琚辨稉顏呯亣娑撳墽娲伴崜宥勭箽閻ｆ瑱绱濇担鍡氱箥鐞涘本妞傚▽鈩冩箒閹跺﹤鐣犳禒顒佸复閸?`DamageHandler` 娑撶粯绁︾粙瀣剁礉娑旂喍绗夐弨顖涘瘮閳ユ粍鏁奸崘娆忕秼閸撳秳婵€鐎规枼鈧繄娈戦懗钘夊閵?
---

## 13. 娴滃娆㈤幀鑽ゅ殠

`EventBus` 閸欘亣绀嬬拹锝咁嚠婢舵牕绠嶉幘顓溾偓?
鐎瑰啫缍嬮崜宥嗗鏉炴枻绱?

- 閹存ɑ鏋熷鈧慨?缂佹挻娼?
- 閸ョ偛鎮庡鈧慨?缂佹挻娼?
- 閸楋紕澧濋幍鎾冲毉
- 閹剁晫澧?
- 娴笺倕顔?
- 濞岃崵鏋?
- 閹躲倗娴?
- Buff 婢х偛鍨?
- 閸楁洑缍呭璁抽
- 閻溾晛顔嶅璁抽

鐎瑰啩绗夌拹鐔荤煑閿?
- 鐟欙箑褰傞崘鍛村劥缂佹挾鐣?
- 閺€鐟板晸鏉╂劘顢戦弮鍓佸Ц閹?- 娴ｆ粈璐熼幋妯绘灍闁槒绶崚銈嗘焽閸忋儱褰?

---

## 14. 瑜版挸澧犳稉宥呭晙闁插洨鏁ら惃鍕＋閺傝顢?

娴犮儰绗呴崘鍛啇娑撳秴鍟€鐏炵偘绨?BattleCore 瑜版挸澧犳總鎴犲閿?
- 閸╄桨绨０婵嗩樆娑撴挾鏁ら崠铏规畱閻樿埖鈧胶澧濇稉缁樼ウ缁?- 閸╄桨绨€圭偘缍嬫笟?Buff 闂€婊冨剼閻?Buff 閻喐绨?
- 閻剛鐡ラ悧宀冿紭閺佸牊鐏夐崗銉ュ經
- `TriggerUnit.InlineExecute`
- 鏉╂劘顢戦弮?`HistoryLog`

婵″倹鐏夐崥搴ｇ敾鐎圭偟骞囬柌宥嗘煀瀵洖鍙嗘潻娆庣昂閺堝搫鍩楅敍宀勬付鐟曚礁鎮撳銉ゆ叏閺€瑙勬拱閺傚洦銆傛稉搴㈢ゴ鐠囨洩绱濋懓灞肩瑝閺勵垰鐪柈銊ㄋ夋稉浣碘偓?
