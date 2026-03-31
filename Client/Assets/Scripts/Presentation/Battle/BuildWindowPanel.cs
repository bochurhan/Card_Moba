using System;
using System.Collections.Generic;
using System.Linq;
using CardMoba.Client.GameLogic.Abstractions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardMoba.Client.Presentation.Battle
{
    /// <summary>
    /// 构筑阶段面板。
    /// 第一版采用代码动态生成，先验证本地 MatchFlow 与 UI 交互链路。
    /// </summary>
    public sealed class BuildWindowPanel : MonoBehaviour
    {
        private IBattleClientRuntime _runtime;
        private BuildWindowViewState _viewState;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _otherPlayersText;
        private TextMeshProUGUI _hintText;
        private RectTransform _dialogRect;
        private RectTransform _optionsContent;
        private ScrollRect _optionsScrollRect;
        private Button _confirmButton;
        private TextMeshProUGUI _confirmButtonText;
        private Button _lockButton;
        private TextMeshProUGUI _lockButtonText;
        private readonly Dictionary<BuildActionViewType, Button> _actionButtons = new Dictionary<BuildActionViewType, Button>();
        private readonly List<GameObject> _dynamicOptionObjects = new List<GameObject>();
        private readonly Dictionary<int, string> _selectedDraftOfferIdsByGroup = new Dictionary<int, string>();

        private BuildActionViewType _selectedAction = BuildActionViewType.None;
        private string _selectedTargetCardId = string.Empty;

        public bool IsVisible => gameObject.activeSelf;

        public void Bind(IBattleClientRuntime runtime)
        {
            _runtime = runtime;
            EnsureBuilt();
        }

        public void Show(BuildWindowViewState viewState)
        {
            EnsureBuilt();
            _viewState = viewState;
            ResetSelection();
            gameObject.SetActive(true);
            Render();
        }

        public void Hide()
        {
            _viewState = null;
            ResetSelection();
            gameObject.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_titleText != null)
                return;

            var rootRect = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0.72f);

            var dialog = CreateContainer("Dialog", transform, new Color(0.12f, 0.15f, 0.2f, 0.96f));
            _dialogRect = dialog.GetComponent<RectTransform>();
            _dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            _dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            _dialogRect.pivot = new Vector2(0.5f, 0.5f);
            _dialogRect.sizeDelta = new Vector2(920f, 760f);
            _dialogRect.anchoredPosition = Vector2.zero;
            var dialogLayout = dialog.AddComponent<VerticalLayoutGroup>();
            dialogLayout.padding = new RectOffset(24, 24, 24, 24);
            dialogLayout.spacing = 12f;
            dialogLayout.childForceExpandHeight = false;
            dialogLayout.childForceExpandWidth = true;

            _titleText = CreateText("TitleText", dialog.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            SetPreferredHeight(_titleText.gameObject, 44f);

            _statusText = CreateText("StatusText", dialog.transform, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetPreferredHeight(_statusText.gameObject, 80f);

            _otherPlayersText = CreateText("OtherPlayersText", dialog.transform, 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetPreferredHeight(_otherPlayersText.gameObject, 48f);

            var actionRow = new GameObject("ActionRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            actionRow.transform.SetParent(dialog.transform, false);
            var actionLayout = actionRow.GetComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 10f;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childForceExpandWidth = true;
            SetPreferredHeight(actionRow, 52f);

            CreateActionButton(actionRow.transform, BuildActionViewType.Heal, "休息");
            CreateActionButton(actionRow.transform, BuildActionViewType.UpgradeCard, "升级");
            CreateActionButton(actionRow.transform, BuildActionViewType.RemoveCard, "删牌");
            CreateActionButton(actionRow.transform, BuildActionViewType.AddCard, "拿牌");

            var scrollArea = new GameObject("OptionsScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollArea.transform.SetParent(dialog.transform, false);
            scrollArea.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.16f, 0.92f);
            SetFlexibleHeight(scrollArea, 1f, 340f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollArea.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _optionsContent = content.GetComponent<RectTransform>();
            _optionsContent.anchorMin = new Vector2(0f, 1f);
            _optionsContent.anchorMax = new Vector2(1f, 1f);
            _optionsContent.pivot = new Vector2(0.5f, 1f);
            _optionsContent.anchoredPosition = Vector2.zero;
            _optionsContent.sizeDelta = new Vector2(0f, 0f);
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _optionsScrollRect = scrollArea.GetComponent<ScrollRect>();
            _optionsScrollRect.viewport = viewportRect;
            _optionsScrollRect.content = _optionsContent;
            _optionsScrollRect.horizontal = false;
            _optionsScrollRect.movementType = ScrollRect.MovementType.Clamped;

            _hintText = CreateText("HintText", dialog.transform, 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetPreferredHeight(_hintText.gameObject, 60f);

            var bottomRow = new GameObject("BottomRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bottomRow.transform.SetParent(dialog.transform, false);
            var bottomLayout = bottomRow.GetComponent<HorizontalLayoutGroup>();
            bottomLayout.spacing = 12f;
            bottomLayout.childForceExpandHeight = false;
            bottomLayout.childForceExpandWidth = true;
            SetPreferredHeight(bottomRow, 52f);

            _confirmButton = CreateCommandButton(bottomRow.transform, "ConfirmButton", "确认本次选择", OnConfirmClicked);
            _confirmButtonText = _confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            _lockButton = CreateCommandButton(bottomRow.transform, "LockButton", "锁定构筑", OnLockClicked);
            _lockButtonText = _lockButton.GetComponentInChildren<TextMeshProUGUI>();

            gameObject.SetActive(false);
        }

        private void Render()
        {
            if (_viewState == null || _viewState.LocalPlayer == null)
            {
                Hide();
                return;
            }

            var local = _viewState.LocalPlayer;
            var currentOpportunity = local.CurrentOpportunity;
            EnsureDefaultAction(currentOpportunity);

            _titleText.text = _viewState.DisplayText;
            _statusText.text = BuildLocalStatusText(local);
            _otherPlayersText.text = BuildOtherPlayersStatusText(local.PlayerId);
            _hintText.text = BuildHintText(local);

            RenderActionButtons(currentOpportunity);
            RenderOptions(local, currentOpportunity);
            RefreshBottomButtons(local);
            RefreshLayout();
            LogRenderDebug(local, currentOpportunity);
        }

        private void RenderActionButtons(BuildOpportunityViewState currentOpportunity)
        {
            BuildActionViewType committedAction = currentOpportunity != null
                ? currentOpportunity.CommittedActionType
                : BuildActionViewType.None;

            foreach (var pair in _actionButtons)
            {
                bool available = currentOpportunity != null
                    && currentOpportunity.AvailableActions.Contains(pair.Key)
                    && (committedAction == BuildActionViewType.None || committedAction == pair.Key);
                pair.Value.interactable = available;
                pair.Value.gameObject.SetActive(true);

                var image = pair.Value.GetComponent<Image>();
                image.color = !available
                    ? new Color(0.22f, 0.24f, 0.28f, 1f)
                    : pair.Key == _selectedAction
                        ? new Color(0.82f, 0.56f, 0.22f, 1f)
                        : new Color(0.24f, 0.32f, 0.42f, 1f);
            }
        }

        private void RenderOptions(PlayerBuildWindowViewState local, BuildOpportunityViewState currentOpportunity)
        {
            ClearDynamicOptions();

            if (local.IsLocked)
            {
                AddInfoBlock("你已锁定本轮构筑选择，等待流程推进。");
                return;
            }

            if (currentOpportunity == null)
            {
                AddInfoBlock("当前所有构筑机会均已处理完成。你可以直接点击下方“锁定构筑”推进下一场战斗。");
                foreach (var summary in local.ResolvedChoiceSummaries)
                    AddInfoBlock(summary, fontSize: 20);
                return;
            }

            switch (_selectedAction)
            {
                case BuildActionViewType.Heal:
                    AddInfoBlock($"休息：回复 {currentOpportunity.HealAmount} 点生命。");
                    break;

                case BuildActionViewType.UpgradeCard:
                    AddInfoBlock($"选择一张可升级的牌。当前候选：{currentOpportunity.UpgradableCards.Count} 张。");
                    foreach (var card in currentOpportunity.UpgradableCards)
                        AddCardOption(card, selected: string.Equals(card.PersistentCardId, _selectedTargetCardId, StringComparison.Ordinal), () => ToggleTargetSelection(card.PersistentCardId));
                    break;

                case BuildActionViewType.RemoveCard:
                    AddInfoBlock($"选择一张可删除的牌。当前候选：{currentOpportunity.RemovableCards.Count} 张。");
                    foreach (var card in currentOpportunity.RemovableCards)
                        AddCardOption(card, selected: string.Equals(card.PersistentCardId, _selectedTargetCardId, StringComparison.Ordinal), () => ToggleTargetSelection(card.PersistentCardId));
                    break;

                case BuildActionViewType.AddCard:
                    if (!currentOpportunity.DraftGroupsRevealed)
                    {
                        AddInfoBlock("确认“拿牌”后，才会揭示两组选牌。");
                        AddInfoBlock("一旦确认拿牌，本次机会就不能再改成休息、升级或删牌。");
                        AddInfoBlock("揭示后每组可选 0 或 1 张，也可以全部跳过，但这次机会仍会被消耗。", 20);
                        break;
                    }

                    AddInfoBlock($"从两组选牌中各选 1 张，也可以全部跳过。当前牌组数：{currentOpportunity.DraftGroups.Count}。再次点击已选卡牌可取消。");
                    foreach (var group in currentOpportunity.DraftGroups)
                    {
                        AddSectionHeader($"第 {group.GroupIndex + 1} 组选牌");
                        foreach (var offer in group.Offers)
                        {
                            bool selected = _selectedDraftOfferIdsByGroup.TryGetValue(group.GroupIndex, out var offerId)
                                && string.Equals(offerId, offer.OfferId, StringComparison.Ordinal);
                            AddDraftOption(group.GroupIndex, offer, selected);
                        }
                    }
                    break;

                default:
                    AddInfoBlock("请选择一种构筑动作。");
                    break;
            }
        }

        private void RefreshBottomButtons(PlayerBuildWindowViewState local)
        {
            bool hasCurrentOpportunity = local.CurrentOpportunity != null;
            bool canConfirm = hasCurrentOpportunity && CanConfirmCurrentChoice(local.CurrentOpportunity);

            _confirmButton.interactable = canConfirm;
            _confirmButtonText.text = hasCurrentOpportunity
                ? BuildConfirmButtonText(local.CurrentOpportunity)
                : "当前无可确认选择";

            _lockButton.interactable = local.CanLock;
            _lockButtonText.text = local.IsLocked ? "已锁定" : "锁定构筑";
        }

        private bool CanConfirmCurrentChoice(BuildOpportunityViewState currentOpportunity)
        {
            if (currentOpportunity == null)
                return false;

            switch (_selectedAction)
            {
                case BuildActionViewType.Heal:
                    return currentOpportunity.AvailableActions.Contains(BuildActionViewType.Heal);

                case BuildActionViewType.UpgradeCard:
                case BuildActionViewType.RemoveCard:
                    return !string.IsNullOrWhiteSpace(_selectedTargetCardId);

                case BuildActionViewType.AddCard:
                    if (currentOpportunity.CommittedActionType != BuildActionViewType.None
                        && currentOpportunity.CommittedActionType != BuildActionViewType.AddCard)
                    {
                        return false;
                    }

                    return currentOpportunity.AvailableActions.Contains(BuildActionViewType.AddCard);
            }

            return false;
        }

        private void EnsureDefaultAction(BuildOpportunityViewState currentOpportunity)
        {
            if (currentOpportunity == null)
            {
                _selectedAction = BuildActionViewType.None;
                _selectedTargetCardId = string.Empty;
                _selectedDraftOfferIdsByGroup.Clear();
                return;
            }

            if (currentOpportunity.CommittedActionType != BuildActionViewType.None)
            {
                _selectedAction = currentOpportunity.CommittedActionType;
                _selectedTargetCardId = string.Empty;
                _selectedDraftOfferIdsByGroup.Clear();
                return;
            }

            if (currentOpportunity.AvailableActions.Contains(_selectedAction))
                return;

            _selectedAction = currentOpportunity.AvailableActions.Count > 0
                ? currentOpportunity.AvailableActions[0]
                : BuildActionViewType.None;
            _selectedTargetCardId = string.Empty;
            _selectedDraftOfferIdsByGroup.Clear();
        }

        private void ResetSelection()
        {
            _selectedAction = BuildActionViewType.None;
            _selectedTargetCardId = string.Empty;
            _selectedDraftOfferIdsByGroup.Clear();
        }

        private void ToggleTargetSelection(string persistentCardId)
        {
            _selectedTargetCardId = string.Equals(_selectedTargetCardId, persistentCardId, StringComparison.Ordinal)
                ? string.Empty
                : persistentCardId;
            Render();
        }

        private void ToggleDraftOfferSelection(int groupIndex, string offerId)
        {
            if (_selectedDraftOfferIdsByGroup.TryGetValue(groupIndex, out var current) && string.Equals(current, offerId, StringComparison.Ordinal))
                _selectedDraftOfferIdsByGroup.Remove(groupIndex);
            else
                _selectedDraftOfferIdsByGroup[groupIndex] = offerId;

            Render();
        }

        private void OnConfirmClicked()
        {
            if (_runtime == null || _viewState?.LocalPlayer?.CurrentOpportunity == null)
                return;

            var choice = new BuildChoiceViewState
            {
                ActionType = _selectedAction,
                TargetPersistentCardId = _selectedTargetCardId,
            };

            foreach (var pair in _selectedDraftOfferIdsByGroup)
                choice.SelectedDraftOfferIdsByGroup[pair.Key] = pair.Value;

            _runtime.SubmitBuildChoice(choice);
        }

        private void OnLockClicked()
        {
            _runtime?.LockBuildWindow();
        }

        private void SelectAction(BuildActionViewType actionType)
        {
            var currentOpportunity = _viewState?.LocalPlayer?.CurrentOpportunity;
            if (currentOpportunity != null
                && currentOpportunity.CommittedActionType != BuildActionViewType.None
                && currentOpportunity.CommittedActionType != actionType)
            {
                return;
            }

            if (_selectedAction == actionType)
                return;

            _selectedAction = actionType;
            _selectedTargetCardId = string.Empty;
            _selectedDraftOfferIdsByGroup.Clear();
            Debug.Log($"[BuildWindowPanel] 切换动作：{actionType}");
            Render();
        }

        private string BuildLocalStatusText(PlayerBuildWindowViewState local)
        {
            string progressText = local.CurrentOpportunity != null
                ? $"当前机会：第 {local.CurrentOpportunity.OpportunityIndex + 1}/{local.OpportunityCount} 次"
                : $"当前机会：已完成 {local.ResolvedOpportunityCount}/{local.OpportunityCount} 次";
            string restrictionText = string.IsNullOrWhiteSpace(local.RestrictionText)
                ? string.Empty
                : $"\n限制：{local.RestrictionText}";
            string resolvedText = local.ResolvedChoiceSummaries.Count == 0
                ? string.Empty
                : $"\n已选：{string.Join("；", local.ResolvedChoiceSummaries)}";

            return $"{local.DisplayName} 预览生命：{local.PreviewHp}/{local.MaxHp}\n{progressText}{restrictionText}{resolvedText}";
        }

        private string BuildOtherPlayersStatusText(string localPlayerId)
        {
            if (_viewState == null)
                return string.Empty;

            var lines = new List<string>();
            foreach (var player in _viewState.Players.Where(item => !string.Equals(item.PlayerId, localPlayerId, StringComparison.Ordinal)))
            {
                string status = player.IsLocked
                    ? "已锁定"
                    : player.CurrentOpportunity != null
                        ? $"进行中（{player.ResolvedOpportunityCount}/{player.OpportunityCount}）"
                        : $"待锁定（{player.ResolvedOpportunityCount}/{player.OpportunityCount}）";
                lines.Add($"{player.DisplayName}：{status}");
            }

            return lines.Count == 0 ? "当前仅有本地玩家参与构筑。" : string.Join("\n", lines);
        }

        private string BuildHintText(PlayerBuildWindowViewState local)
        {
            if (local.IsLocked)
                return "你的构筑选择已经锁定。";
            if (local.CurrentOpportunity == null)
                return "你可以点击“锁定构筑”进入下一场战斗。";
            if (local.RestrictionMode == BuildWindowRestrictionViewMode.ForcedRecovery)
                return "由于上一场战斗被击败，本次构筑只能选择休息。";
            if (local.CurrentOpportunity.CommittedActionType == BuildActionViewType.AddCard
                && local.CurrentOpportunity.DraftGroupsRevealed)
            {
                return "你已经承诺本次机会用于拿牌。现在请从每组中选择 0 或 1 张，或全部跳过，然后完成本次拿牌。";
            }
            if (_selectedAction == BuildActionViewType.AddCard)
                return "确认“拿牌”后才会揭示两组选牌；一旦确认，本次机会不能再改为其他动作。";

            return "先选择动作，再从候选区域挑选目标，最后点击“确认本次选择”。";
        }

        private string BuildConfirmButtonText(BuildOpportunityViewState currentOpportunity)
        {
            if (currentOpportunity == null)
                return "当前无可确认选择";

            if (_selectedAction == BuildActionViewType.AddCard && !currentOpportunity.DraftGroupsRevealed)
                return $"确认第 {currentOpportunity.OpportunityIndex + 1} 次拿牌";

            if (currentOpportunity.CommittedActionType == BuildActionViewType.AddCard
                && currentOpportunity.DraftGroupsRevealed)
            {
                return $"完成第 {currentOpportunity.OpportunityIndex + 1} 次拿牌";
            }

            return $"确认第 {currentOpportunity.OpportunityIndex + 1} 次选择";
        }

        private void AddInfoBlock(string text, int fontSize = 22)
        {
            var block = CreateText($"Info_{_dynamicOptionObjects.Count:D2}", _optionsContent, fontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            block.text = text;
            SetPreferredHeight(block.gameObject, 56f);
            _dynamicOptionObjects.Add(block.gameObject);
        }

        private void AddSectionHeader(string text)
        {
            var header = CreateText($"Header_{_dynamicOptionObjects.Count:D2}", _optionsContent, 24, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            header.text = text;
            SetPreferredHeight(header.gameObject, 36f);
            _dynamicOptionObjects.Add(header.gameObject);
        }

        private void AddCardOption(BuildCardViewState card, bool selected, Action onClick)
        {
            string label = $"【{card.DisplayName}】 费用 {card.Cost}";
            if (card.UpgradeLevel > 0)
                label += $"  升级+{card.UpgradeLevel}";
            label += $"\n{card.Description}";

            var button = CreateOptionButton($"Card_{card.PersistentCardId}", label, selected, onClick);
            _dynamicOptionObjects.Add(button.gameObject);
        }

        private void AddDraftOption(int groupIndex, BuildDraftOfferViewState offer, bool selected)
        {
            string label = $"【{offer.DisplayName}】[{offer.RarityText}] 费用 {offer.Cost}";
            if (offer.IsUpgraded)
                label += "  升级版";
            label += $"\n{offer.Description}";

            var button = CreateOptionButton(
                $"Draft_{groupIndex}_{offer.OfferId}",
                label,
                selected,
                () => ToggleDraftOfferSelection(groupIndex, offer.OfferId));
            _dynamicOptionObjects.Add(button.gameObject);
        }

        private Button CreateOptionButton(string name, string label, bool selected, Action onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(_optionsContent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = selected
                ? new Color(0.82f, 0.56f, 0.22f, 1f)
                : new Color(0.22f, 0.28f, 0.36f, 1f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 84f;
            layoutElement.preferredHeight = 96f;
            layoutElement.flexibleWidth = 1f;

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var text = CreateText("Label", buttonObject.transform, 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            text.text = label;
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);
            text.enableWordWrapping = true;

            return button;
        }

        private void ClearDynamicOptions()
        {
            foreach (var option in _dynamicOptionObjects)
            {
                if (option != null)
                {
                    option.transform.SetParent(null, false);
                    Destroy(option);
                }
            }

            _dynamicOptionObjects.Clear();
        }

        private void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            if (_optionsContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_optionsContent);
            if (_dialogRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_dialogRect);
            if (_optionsScrollRect != null)
                _optionsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void LogRenderDebug(PlayerBuildWindowViewState local, BuildOpportunityViewState currentOpportunity)
        {
            if (local == null)
            {
                Debug.Log("[BuildWindowPanel] Render: local player view is null.");
                return;
            }

            if (currentOpportunity == null)
            {
                Debug.Log($"[BuildWindowPanel] Render: {local.DisplayName} 当前无可编辑机会。动态节点={_dynamicOptionObjects.Count}");
                return;
            }

            Debug.Log(
                $"[BuildWindowPanel] Render: 玩家={local.DisplayName} 动作={_selectedAction} 已承诺={currentOpportunity.CommittedActionType} 可选动作={string.Join(",", currentOpportunity.AvailableActions)} " +
                $"升级候选={currentOpportunity.UpgradableCards.Count} 删牌候选={currentOpportunity.RemovableCards.Count} 拿牌已揭示={currentOpportunity.DraftGroupsRevealed} 拿牌组={currentOpportunity.DraftGroups.Count} " +
                $"动态节点={_dynamicOptionObjects.Count} contentChild={(_optionsContent != null ? _optionsContent.childCount : 0)} " +
                $"viewportH={(_optionsScrollRect != null && _optionsScrollRect.viewport != null ? _optionsScrollRect.viewport.rect.height : 0f)} " +
                $"contentH={(_optionsContent != null ? _optionsContent.rect.height : 0f)}");

            if (_optionsContent != null && _optionsContent.childCount > 0)
            {
                var child = _optionsContent.GetChild(0) as RectTransform;
                if (child != null)
                {
                    Debug.Log(
                        $"[BuildWindowPanel] 首个节点：name={child.name} pos={child.anchoredPosition} size={child.rect.size} active={child.gameObject.activeSelf}");
                }
            }
        }

        private void CreateActionButton(Transform parent, BuildActionViewType actionType, string label)
        {
            var button = CreateCommandButton(parent, $"{actionType}Button", label, () => SelectAction(actionType));
            _actionButtons[actionType] = button;
        }

        private static Button CreateCommandButton(Transform parent, string name, string label, Action onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = new Color(0.24f, 0.32f, 0.42f, 1f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 52f;
            layoutElement.flexibleWidth = 1f;

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var text = CreateText("Label", buttonObject.transform, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static GameObject CreateContainer(string name, Transform parent, Color color)
        {
            var container = new GameObject(name, typeof(RectTransform), typeof(Image));
            container.transform.SetParent(parent, false);
            container.GetComponent<Image>().color = color;
            return container;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static void SetPreferredHeight(GameObject target, float preferredHeight)
        {
            var layoutElement = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
        }

        private static void SetFlexibleHeight(GameObject target, float flexibleHeight, float minHeight)
        {
            var layoutElement = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = flexibleHeight;
            layoutElement.minHeight = minHeight;
        }
    }
}
