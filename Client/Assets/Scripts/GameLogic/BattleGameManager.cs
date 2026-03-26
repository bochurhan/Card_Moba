#pragma warning disable CS8632

using System;
using System.Collections.Generic;
using CardMoba.BattleCore.Buff;
using CardMoba.BattleCore.Context;
using CardMoba.BattleCore.Core;
using CardMoba.BattleCore.EventBus;
using CardMoba.BattleCore.Foundation;
using CardMoba.ConfigModels.Card;
using CardMoba.Protocol.Enums;
using CardMoba.Client.Data.ConfigData;

namespace CardMoba.Client.GameLogic
{
    /// <summary>
    /// 鎴樻枟娴佺▼绠＄悊鍣紙V2锛夆€斺€?杩炴帴 UI 灞傚拰 BattleCore V2 鐨勬ˉ姊併€?
    ///
    /// 鑱岃矗锛?
    ///   - 閫氳繃 BattleFactory 鍒涘缓骞堕┍鍔ㄤ竴灞€鎴樻枟鐨勫畬鏁寸敓鍛藉懆鏈?
    ///   - 鎻愪緵 UI 灞傝皟鐢ㄧ殑鎿嶄綔鎺ュ彛锛堝嚭鐗屻€佺粨鏉熷洖鍚堢瓑锛?
    ///   - 鍐呯疆绠€鍗?AI 鎺у埗瀵规墜琛屼负
    ///   - 閫氳繃 C# 浜嬩欢閫氱煡 UI 灞傚埛鏂版樉绀?
    ///
    /// 鏋舵瀯锛?
    ///   BattleUIManager (Presentation)
    ///     鈫?BattleGameManager (GameLogic)   鈫?鏈被
    ///       鈫?BattleFactory / RoundManager (BattleCore V2)
    /// </summary>
    public class BattleGameManager
    {
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // UI 灞傝闃呬簨浠?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>瀵瑰眬鐘舵€佸彂鐢熷彉鍖栨椂瑙﹀彂锛圚P / 鎶ょ浘 / 鎵嬬墝绛夊彉鍖栵級</summary>
        public event Action OnStateChanged;

        /// <summary>鏂板鏃ュ織娑堟伅鏃惰Е鍙戯紙鍙傛暟锛氬彲鍚?TMP RichText 鐫€鑹叉爣绛剧殑瀛楃涓诧級</summary>
        public event Action<string> OnLogMessage;

        /// <summary>
        /// 瀵瑰眬缁撴潫鏃惰Е鍙戙€?
        /// 鍙傛暟 winnerCode锛? = 鐜╁鑳滐紝2 = AI 鑳滐紝-1 = 骞冲眬銆?
        /// </summary>
        public event Action<int> OnGameOver;

        /// <summary>鍥炲悎闃舵鍒囨崲鏃惰Е鍙戯紙鍙傛暟锛氶樁娈垫弿杩版枃瀛楋紝鐢ㄤ簬鏇存柊 phaseText 鍜岄┍鍔ㄨ鏃跺櫒锛?/summary>
        public event Action<string> OnPhaseChanged;

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鐜╁ ID 甯搁噺
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        public const string HumanPlayerId = "player1";
        public const string AiPlayerId    = "player2";

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // V2 鏍稿績瀵硅薄
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private BattleContext _ctx;
        private RoundManager  _roundManager;

        // configId 鈫?CardConfig 鏄犲皠锛圔attleFactory 鍒濆鍖栧悗鐢?RebuildCardConfigMap 濉厖锛?
        private readonly Dictionary<string, CardConfig> _cardConfigMap
            = new Dictionary<string, CardConfig>();

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍏紑鐘舵€佸睘鎬э紙UI 灞傚彧璇伙級
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>瑜版挸澧犻幋妯绘灍娑撳﹣绗呴弬?/summary>
        public BattleContext Context => _ctx;

        /// <summary>鏄惁澶勪簬鐜╁鎿嶄綔闃舵</summary>
        public bool IsPlayerTurn { get; private set; }

        /// <summary>瀵瑰眬鏄惁宸茬粨鏉?/summary>
        public bool IsGameOver => _roundManager?.IsBattleOver ?? false;

        /// <summary>褰撳墠鍥炲悎鏁?/summary>
        public int CurrentRound => _roundManager?.CurrentRound ?? 0;

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鎴樻枟鍚姩
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 寮€濮嬩竴鍦烘柊鐨?1v1 瀵规垬锛堜娇鐢ㄩ粯璁ゆ垬澹祴璇曞崱缁勶級銆?
        /// </summary>
        public void StartBattle()
        {
            StartBattleWithDeck(DefaultWarriorDeckIds, DefaultWarriorDeckIds);
        }

        /// <summary>
        /// 浣跨敤鎸囧畾鍗＄墝 ID 鍒楄〃寮€濮嬪鎴樸€?
        /// </summary>
        public void StartBattleWithDeck(int[] playerDeckIds, int[] aiDeckIds)
        {
            EnsureConfigLoaded();
            _cardConfigMap.Clear();

            // 鈹€鈹€ 鏋勫缓 configId 鏄犲皠琛紙渚涘悗缁?instanceId 鏌ユ壘锛?鈹€鈹€
            BuildCardConfigMap();

            // 鈹€鈹€ 鏋勫缓 DeckConfig 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            var humanDeck = BuildDeckConfig(playerDeckIds);
            var aiDeck    = BuildDeckConfig(aiDeckIds);

            // 鈹€鈹€ 鍒涘缓 EventBus 閫傞厤鍣?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            var eventBus = new InternalEventBus(this);

            // 閳光偓閳光偓 BattleFactory 閸掓稑缂撻幋妯绘灍 閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓閳光偓
            var factory = new BattleFactory
            {
                BuffConfigProvider = ResolveRuntimeBuffConfig,
                CardDefinitionProvider = configId =>
                {
                    if (!_cardConfigMap.TryGetValue(configId, out var cardConfig))
                        return null;

                    string defaultTarget = CardConfigToEffectAdapter.CardTargetTypeToString(cardConfig.TargetType);
                    return new BattleCardDefinition
                    {
                        ConfigId = configId,
                        IsExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust),
                        IsStatCard = cardConfig.Tags.HasFlag(CardTag.Status),
                        Effects = CardConfigToEffectAdapter.ConvertEffects(cardConfig, defaultTarget),
                    };
                },
            };
            var result  = factory.CreateBattle(
                battleId:   "local-battle",
                randomSeed: 42,
                players: new List<PlayerSetupData>
                {
                    new PlayerSetupData
                    {
                        PlayerId     = HumanPlayerId,
                        MaxHp        = 30,
                        InitialHp    = 30,
                        InitialArmor = 0,
                        DeckConfig   = humanDeck,
                    },
                    new PlayerSetupData
                    {
                        PlayerId     = AiPlayerId,
                        MaxHp        = 30,
                        InitialHp    = 30,
                        InitialArmor = 0,
                        DeckConfig   = aiDeck,
                    },
                },
                eventBus: eventBus);

            _ctx          = result.Context;
            _roundManager = result.RoundManager;

            // 杈撳嚭 setup 鏃ュ織
            foreach (var log in result.SetupLog)
                OnLogMessage?.Invoke(ColorizeLog(log));

            // 鈹€鈹€ 寮€濮嬬涓€鍥炲悎 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"绗?{_roundManager.CurrentRound} 鍥炲悎 路 浣犵殑鎿嶄綔鏈?);
            OnStateChanged?.Invoke();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鐜╁鎿嶄綔鎺ュ彛
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 鐜╁鎵撳嚭涓€寮犵灛绛栫墝锛堢珛鍗崇粨绠楋級銆?
        /// </summary>
        /// <param name="handIndex">鍦ㄤ汉绫荤帺瀹舵墜鐗屽垪琛ㄤ腑鐨勪綅缃?/param>
        /// <returns>鎿嶄綔缁撴灉鎻忚堪</returns>
        public string PlayerPlayInstantCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "瑜版挸澧犳稉宥嗘Ц閹垮秳缍旈梼鑸殿唽";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: true);
        }

        /// <summary>
        /// 鐜╁鎻愪氦涓€寮犲畾绛栫墝锛堢瓑寰?EndRound 缁撶畻锛夈€?
        /// </summary>
        public string PlayerCommitPlanCard(int handIndex)
        {
            if (!IsPlayerTurn || IsGameOver) return "瑜版挸澧犳稉宥嗘Ц閹垮秳缍旈梼鑸殿唽";
            return PlayCardInternal(HumanPlayerId, handIndex, instant: false);
        }

        /// <summary>
        /// 鐜╁缁撴潫鍥炲悎锛欰I 鎿嶄綔 鈫?瀹氱瓥缁撶畻 鈫?涓嬩竴鍥炲悎寮€濮嬨€?
        /// </summary>
        public void PlayerEndTurn()
        {
            if (!IsPlayerTurn || IsGameOver) return;

            IsPlayerTurn = false;

            // 鈹€鈹€ AI 鎿嶄綔闃舵 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            OnPhaseChanged?.Invoke($"绗?{_roundManager.CurrentRound} 鍥炲悎 路 瀵规墜鎿嶄綔涓?..");
            OnStateChanged?.Invoke();
            ExecuteAiTurn();
            FlushLogs();

            if (IsGameOver) { NotifyGameOver(); return; }

            // 鈹€鈹€ 瀹氱瓥浜斿眰缁撶畻 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            OnPhaseChanged?.Invoke($"绗?{_roundManager.CurrentRound} 鍥炲悎 路 缁撶畻涓?..");
            _roundManager.EndRound(_ctx);
            FlushLogs();
            OnStateChanged?.Invoke();

            if (IsGameOver) { NotifyGameOver(); return; }

            // 鈹€鈹€ 涓嬩竴鍥炲悎 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            _roundManager.BeginRound(_ctx);
            FlushLogs();

            IsPlayerTurn = true;
            OnPhaseChanged?.Invoke($"绗?{_roundManager.CurrentRound} 鍥炲悎 路 浣犵殑鎿嶄綔鏈?);
            OnStateChanged?.Invoke();
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鏁版嵁璁块棶锛圲I 灞傝皟鐢級
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>鑾峰彇浜虹被鐜╁鏁版嵁锛圴2 PlayerData锛?/summary>
        public PlayerData GetHumanPlayer() => _ctx?.GetPlayer(HumanPlayerId);

        /// <summary>鑾峰彇 AI 鐜╁鏁版嵁锛圴2 PlayerData锛?/summary>
        public PlayerData GetAiPlayer() => _ctx?.GetPlayer(AiPlayerId);

        /// <summary>
        /// 鑾峰彇浜虹被鐜╁鎵嬬墝锛堝惈瀵瑰簲 CardConfig 鏄剧ず淇℃伅锛夈€?
        /// 杩斿洖鍒楄〃椤哄簭涓?PlayerData.Hand 涓殑 BattleCard 椤哄簭涓€鑷淬€?
        /// </summary>
        public List<(BattleCard Card, CardConfig Config)> GetHumanHandCards()
        {
            var list   = new List<(BattleCard, CardConfig)>();
            var player = _ctx?.GetPlayer(HumanPlayerId);
            if (player == null) return list;

            foreach (var bc in player.GetCardsInZone(CardZone.Hand))
            {
                _cardConfigMap.TryGetValue(bc.ConfigId, out var cfg);
                list.Add((bc, cfg));
            }
            return list;
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 璋冭瘯
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>鎵撳嵃鎴樺満瀹屾暣鐘舵€佸揩鐓э紝閫氳繃 OnLogMessage 鎺ㄩ€佺粰 UI銆?/summary>
        public void PrintBattleStatus()
        {
            if (_ctx == null)
            {
                OnLogMessage?.Invoke("<color=#ff4444>[鐘舵€佸揩鐓 BattleContext 涓虹┖锛屽灞€灏氭湭寮€濮?/color>");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#ffffff>鈺斺晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺?[鎴樺満鐘舵€佸揩鐓 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺?/color>");
            sb.AppendLine($"<color=#aaaaaa>  绗?{_roundManager.CurrentRound} 鍥炲悎</color>");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(HumanPlayerId), "鎴戞柟");
            sb.AppendLine();
            AppendPlayerSnapshot(sb, _ctx.GetPlayer(AiPlayerId),    "瀵规墜");
            sb.AppendLine("<color=#ffffff>鈺氣晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨暆</color>");

            foreach (var line in sb.ToString().Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    OnLogMessage?.Invoke(line.TrimEnd('\r'));
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍐呴儴锛氬嚭鐗屾牳蹇冮€昏緫
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private string PlayCardInternal(string playerId, int handIndex, bool instant)
        {
            var player = _ctx.GetPlayer(playerId);
            if (player == null) return "鐜╁涓嶅瓨鍦?;

            var hand = player.GetCardsInZone(CardZone.Hand);
            if (handIndex < 0 || handIndex >= hand.Count)
                return $"鎵嬬墝绱㈠紩瓒婄晫锛坽handIndex}/{hand.Count}锛?;

            var battleCard = hand[handIndex];
            if (!_cardConfigMap.TryGetValue(battleCard.ConfigId, out var cardConfig))
                return $"鎵句笉鍒板崱鐗岄厤缃?configId={battleCard.ConfigId}";

            if (!_roundManager.CanPlayCard(_ctx, playerId, battleCard.ConfigId, out var playRestrictionReason))
            {
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {playRestrictionReason}</color>");
                return playRestrictionReason;
            }

            // 鈹€鈹€ 鑳介噺鏍￠獙 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            int cost = cardConfig.EnergyCost;
            if (player.Energy < cost)
            {
                string reason = $"鑳介噺涓嶈冻锛堝綋鍓?{player.Energy}锛岄渶瑕?{cost}锛?;
                OnLogMessage?.Invoke($"<color=#ff8866>[!] {reason}</color>");
                return reason;
            }

            // 鈹€鈹€ 娑堣€楄兘閲?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            player.Energy -= cost;

            string cardName = cardConfig.CardName;

            // 鈹€鈹€ 鍑虹墝 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
            if (instant)
            {
                // 鐬瓥鐗岋細鍏堢Щ鍑烘墜鐗屽尯锛屽啀缁撶畻锛屾渶鍚庢牴鎹爣绛惧喅瀹氬幓鍚?
                _roundManager.PlayInstantCard(_ctx, playerId, battleCard.InstanceId);
                FlushLogs();
                OnLogMessage?.Invoke($"<color=#aaffaa>{(playerId == HumanPlayerId ? "浣? : "瀵规墜")} 鈫?鎵撳嚭鐬瓥鐗屻€恵cardName}銆戯紙鑺辫垂 {cost} 鐐硅兘閲忥級</color>");
            }
            else
            {
                // 瀹氱瓥鐗岋細绉诲叆绛栫暐鍖猴紝绛夊緟 EndRound 缁熶竴缁撶畻
                _roundManager.CommitPlanCard(_ctx, new CommittedPlanCard
                {
                    PlayerId       = playerId,
                    CardInstanceId = battleCard.InstanceId,
                });
                FlushLogs();
                OnLogMessage?.Invoke($"<color=#aaddff>{(playerId == HumanPlayerId ? "浣? : "瀵规墜")} 鈫?鎻愪氦瀹氱瓥鐗屻€恵cardName}銆戯紙鑺辫垂 {cost} 鐐硅兘閲忥級</color>");
            }

            OnStateChanged?.Invoke();
            if (IsGameOver) NotifyGameOver();
            return cardName;
        }

        /// <summary>
        /// 鍑虹墝鍚庢牴鎹崱鐗屾爣绛惧喅瀹氬崱鐗屽幓鍚戙€?
        /// Exhaust 鏍囩 鈫?浠庢父鎴忎腑绉婚櫎锛涙櫘閫氱墝 鈫?杩涘純鐗屽爢銆?
        /// </summary>
        private void MoveCardAfterPlay(BattleCard battleCard, CardConfig cardConfig)
        {
            bool isExhaust = cardConfig.Tags.HasFlag(CardTag.Exhaust);
            if (isExhaust)
            {
                // 娑堣€楃墝锛氫粠 AllCards 涓交搴曠Щ闄?
                var owner = _ctx.GetPlayer(battleCard.OwnerId);
                owner?.AllCards.Remove(battleCard);
                _ctx.RoundLog.Add($"[BattleGameManager] 鍗＄墝銆恵cardConfig.CardName}銆戝凡娑堣€楋紙Exhaust锛夈€?);
            }
            else
            {
                // 鏅€氱墝锛氱Щ鍏ュ純鐗屽爢
                _ctx.CardManager.MoveCard(_ctx, battleCard, CardZone.Discard);
            }
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍐呴儴锛欰I 閫昏緫
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void ExecuteAiTurn()
        {
            var player = _ctx.GetPlayer(AiPlayerId);
            if (player == null || !player.HeroEntity.IsAlive) return;

            // 绠€鍗曠瓥鐣ワ細鎶婃墍鏈夋墜鐗岄兘鎵?鎻愪氦锛堟祴璇曠敤锛屾棤鑳介噺闄愬埗锛?
            var hand = player.GetCardsInZone(CardZone.Hand);
            var snapshot = new List<BattleCard>(hand); // 闃叉閬嶅巻鏃跺垪琛ㄥ彉鍖?

            foreach (var battleCard in snapshot)
            {
                if (!player.HeroEntity.IsAlive || IsGameOver) break;
                if (!_cardConfigMap.TryGetValue(battleCard.ConfigId, out var cfg)) continue;

                bool isInstant = cfg.TrackType == CardTrackType.Instant;
                PlayCardInternal(AiPlayerId, 0, isInstant);
            }
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍐呴儴锛氬崱缁勬瀯寤?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 鍔涢噺鎴樻祴璇曞崱缁勶紙13寮狅級锛?
        ///   鎵撳嚮 脳4 + 闃插尽 脳3 + 瑙傚療寮辩偣 脳2 + 椋炲墤鍥炴棆闀?脳2 + 鎴樻枟涓撴敞 脳1 + 绐佺牬鏋侀檺 脳1
        /// </summary>
        /* Legacy V1 warrior demo deck removed from active use.
        {
            2001, 2001, 2001, 2001,   // 鎵撳嚮 脳4        (1璐癸紝瀹氱瓥锛岄€犳垚6浼ゅ)
            2002, 2002, 2002,         // 闃插尽 脳3        (1璐癸紝瀹氱瓥锛岃幏寰?鎶ょ浘)
            2003, 2003,               // 瑙傚療寮辩偣 脳2    (1璐癸紝瀹氱瓥锛屾潯浠惰幏寰?鍔涢噺)
            2005, 2005,               // 椋炲墤鍥炴棆闀?脳2  (1璐癸紝瀹氱瓥锛?浼っ?娆?
            2006,                     // 全力一击 x1
            2007, 2007,               // 撕裂 x2
            2008, 2008,               // 愤怒 x2
            1001,                     // 鎴樻枟涓撴敞 脳1    (0璐癸紝鐬瓥锛屾娊3寮?
            1002,                     // 绐佺牬鏋侀檺 脳1    (1璐癸紝鐬瓥锛岃幏寰?鐐瑰姏閲徛锋秷鑰?
        };

        */
        private static readonly int[] DefaultWarriorDeckIds = new int[]
        {
            2001, 2001, 2001,         // 鏃犳儏杩炴墦 x3
            1001, 1001, 1001,         // 鎸佺浘鍚戝墠 x3
            1002, 1002,               // 閿讳綋 x2
            1003, 1003,               // 鏀捐 x2
            1004, 1004,               // 鐤插姵琛屽啗 x2
            1005,                     // 绔辰鑰屾笖 x1
            2002, 2002,               // 浠ヨ杩樿 x2
            2003, 2003,               // 椴滆鎶ょ浘 x2
            2004,                     // 姝讳骸鏀跺壊 x1
            2005, 2005,               // 鎶ょ浘鐚涘嚮 x2
            2006,                     // 全力一击 x1
            2007, 2007,               // 撕裂 x2
            2008, 2008,               // 愤怒 x2
        };

        private static BuffConfig? ResolveRuntimeBuffConfig(string buffId)
        {
            return buffId switch
            {
                "strength" => new BuffConfig
                {
                    BuffId = "strength",
                    BuffName = "鍔涢噺",
                    Description = "澧炲姞閫犳垚鐨勪激瀹?,
                    BuffType = BuffType.Strength,
                    IsBuff = true,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 0,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "weak" => new BuffConfig
                {
                    BuffId = "weak",
                    BuffName = "铏氬急",
                    Description = "閫犳垚鐨勪激瀹抽檷浣?25%",
                    BuffType = BuffType.Weak,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 99,
                    DefaultDuration = 1,
                    DefaultValue = 25,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_draw_this_turn" => new BuffConfig
                {
                    BuffId = "no_draw_this_turn",
                    BuffName = "閺堫剙娲栭崥鍫㈩洣濮濄垺濞婇悧?,
                    Description = "鏈洖鍚堝墿浣欐椂闂村唴鏃犳硶鍐嶆娊鐗?,
                    BuffType = BuffType.NoDrawThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                "no_damage_card_this_turn" => new BuffConfig
                {
                    BuffId = "no_damage_card_this_turn",
                    BuffName = "鏈洖鍚堢姝激瀹崇墝",
                    Description = "鏈洖鍚堝墿浣欐椂闂村唴鏃犳硶鍐嶆墦鍑轰激瀹崇墝",
                    BuffType = BuffType.NoDamageCardThisTurn,
                    IsBuff = false,
                    StackRule = BuffStackRule.RefreshDuration,
                    MaxStacks = 1,
                    DefaultDuration = 1,
                    DefaultValue = 0,
                    IsDispellable = true,
                    IsPurgeable = true,
                },
                _ => null,
            };
        }

        private List<(string configId, int count)> BuildDeckConfig(int[] cardIds)
        {
            var countMap = new Dictionary<int, int>();
            foreach (var id in cardIds)
            {
                if (!countMap.ContainsKey(id)) countMap[id] = 0;
                countMap[id]++;
            }

            var deck = new List<(string, int)>();
            foreach (var kv in countMap)
            {
                if (CardConfigManager.Instance.GetCard(kv.Key) != null)
                    deck.Add((kv.Key.ToString(), kv.Value));
                else
                    OnLogMessage?.Invoke($"<color=#ffaa00>[璀﹀憡] 鍗＄墝閰嶇疆涓嶅瓨鍦? {kv.Key}锛屽凡璺宠繃</color>");
            }
            return deck;
        }

        /// <summary>
        /// 鏋勫缓 configId锛堝瓧绗︿覆褰㈠紡鐨?CardId锛夆啋 CardConfig 鐨勬槧灏勮〃锛?
        /// 渚?PlayCardInternal 鍦ㄦ嬁鍒?BattleCard.ConfigId 鍚庡揩閫熸煡鎵鹃厤缃€?
        /// </summary>
        private void BuildCardConfigMap()
        {
            var all = CardConfigManager.Instance.AllCards;
            if (all == null) return;
            foreach (var kv in all)
                _cardConfigMap[kv.Key.ToString()] = kv.Value;
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍐呴儴锛氳緟鍔?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        private void EnsureConfigLoaded()
        {
            if (!CardConfigManager.Instance.IsLoaded)
                CardConfigManager.Instance.LoadAll();
        }

        private void FlushLogs()
        {
            if (_ctx == null) return;
            foreach (var raw in _ctx.RoundLog)
                OnLogMessage?.Invoke(ColorizeLog(raw));
            _ctx.RoundLog.Clear();
        }

        private void NotifyGameOver()
        {
            string? winner = _roundManager?.WinnerId;
            int code = winner == null       ? -1
                     : winner == HumanPlayerId ? 1
                     : 2;
            OnGameOver?.Invoke(code);
        }

        private static string ColorizeLog(string log)
        {
            if (log.Contains("<color=")) return log;
            string lower = log.ToLower();

            if (lower.Contains("浼ゅ") || lower.Contains("鍑讳腑") || lower.Contains("鎵ｉ櫎"))
                return $"<color=#ff8866>{log}</color>";
            if (lower.Contains("鎶ょ浘") || lower.Contains("shield"))
                return $"<color=#66aaff>{log}</color>";
            if (lower.Contains("娌荤枟") || lower.Contains("鍥炶") || lower.Contains("鎭㈠"))
                return $"<color=#66ee88>{log}</color>";
            if (lower.Contains("鍔涢噺") || lower.Contains("buff") || lower.Contains("鎶ょ敳"))
                return $"<color=#ffdd55>{log}</color>";
            if (lower.Contains("鍥炲悎") && (log.Contains("鈺愨晲") || log.Contains("鈹€鈹€")))
                return $"<color=#888888><size=85%>{log}</size></color>";

            return log;
        }

        private void AppendPlayerSnapshot(System.Text.StringBuilder sb, PlayerData? p, string label)
        {
            if (p == null) { sb.AppendLine($"  [{label}]: 鏁版嵁涓嶅瓨鍦?); return; }

            var hero    = p.HeroEntity;
            var hand    = p.GetCardsInZone(CardZone.Hand);
            var deck    = p.GetCardsInZone(CardZone.Deck);
            var discard = p.GetCardsInZone(CardZone.Discard);

            string hpColor = hero.Hp <= hero.MaxHp / 3 ? "#ff4444"
                           : hero.Hp <= hero.MaxHp * 2 / 3 ? "#ffaa33"
                           : "#66ee88";

            sb.AppendLine($"  <color=#ddddff>[{label}]</color>");
            sb.AppendLine($"    HP    : <color={hpColor}>{hero.Hp}/{hero.MaxHp}</color>"
                + (hero.Shield > 0 ? $"   鎶ょ浘: <color=#66aaff>{hero.Shield}</color>" : "")
                + (hero.Armor  > 0 ? $"   鎶ょ敳: <color=#88ccff>{hero.Armor}</color>" : ""));
            sb.AppendLine($"    鑳介噺  : <color=#ffdd55>{p.Energy}/{p.MaxEnergy}</color>");
            sb.AppendLine($"    鎵嬬墝  : {hand.Count} 寮?  鐗屽簱: {deck.Count}   寮冪墝: {discard.Count}");
        }

        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
        // 鍐呴儴锛欵ventBus 閫傞厤鍣?
        // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲

        /// <summary>
        /// 灏?V2 BattleCore 鍐呴儴浜嬩欢杞彂缁?BattleGameManager 鐨?C# 浜嬩欢 / UI 鏃ュ織銆?
        /// </summary>
        private sealed class InternalEventBus : IEventBus
        {
            private readonly BattleGameManager _mgr;
            public InternalEventBus(BattleGameManager mgr) => _mgr = mgr;

            public void Subscribe<T>(Action<T> handler)   where T : BattleEventBase { }
            public void Unsubscribe<T>(Action<T> handler) where T : BattleEventBase { }

            public void Publish<T>(T evt) where T : BattleEventBase
            {
                switch (evt)
                {
                    case DamageDealtEvent dmg:
                        if (dmg.RealHpDamage > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#ff6666>[浼ゅ] {dmg.SourceEntityId} -> {dmg.TargetEntityId}锛? +
                                $"{dmg.RealHpDamage} 鐐? +
                                (dmg.ShieldAbsorbed > 0 ? $"锛堟姢鐩惧惛鏀?{dmg.ShieldAbsorbed}锛? : "") +
                                "</color>");
                        else if (dmg.ShieldAbsorbed > 0)
                            _mgr.OnLogMessage?.Invoke(
                                $"<color=#66aaff>[鎶ょ浘] 鍚告敹 {dmg.ShieldAbsorbed} 鐐逛激瀹?/color>");
                        break;

                    case HealEvent heal:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66ee88>[娌荤枟] {heal.TargetEntityId} 鎭㈠ {heal.RealHealAmount} 鐐圭敓鍛?/color>");
                        break;

                    case ShieldGainedEvent sg:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#66aaff>[鎶ょ浘] {sg.TargetEntityId} 鑾峰緱 {sg.ShieldAmount} 鐐规姢鐩?/color>");
                        break;

                    case RoundStartEvent rs:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 绗?{rs.Round} 鍥炲悎寮€濮?---</size></color>");
                        break;

                    case RoundEndEvent re:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#888888><size=85%>--- 绗?{re.Round} 鍥炲悎缁撴潫 ---</size></color>");
                        break;

                    case PlayerDeathEvent death:
                        _mgr.OnLogMessage?.Invoke(
                            $"<color=#ff4444>[鍊掍笅] {death.PlayerId}</color>");
                        break;

                    case BattleEndEvent end:
                        _mgr.OnLogMessage?.Invoke(end.IsDraw
                            ? "<color=#ffdd55>[缁撴潫] 骞冲眬锛?/color>"
                            : $"<color=#ffdd55>[缁撴潫] 鑳滆€咃細{end.WinnerId}</color>");
                        break;
                }
            }
        }
    }
}
