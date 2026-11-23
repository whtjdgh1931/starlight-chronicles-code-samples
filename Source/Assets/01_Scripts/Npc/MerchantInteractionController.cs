using UnityEngine;

/// <summary>
/// 상인 대화 시작·종료와 로컬 캐릭터의 조작 제한
/// 퀘스트 상태 계산과 화면 상세 구성 제외
/// </summary>
public sealed class MerchantInteractionController : MonoBehaviour
{
    [SerializeField] private MerchantNpc _npc;
    [SerializeField] private int[] _questIds;
    [SerializeField] private int[] _shopItemIds;
    [SerializeField, Range(0f, 1f)] private float _sellRate = 0.5f;
    private Player _player;
    private MerchantQuestPresenter _presenter;
    private MerchantShopPresenter _shopPresenter;

    /// <summary>
    /// 준비된 캐릭터와 공통 화면을 연결하고 상인 입력 구독
    /// </summary>
    public void Bind(Player player, DataManager data, MerchantView view)
    {
        Unbind();
        _player = player;
        _presenter = new MerchantQuestPresenter(player, data, view, _questIds);
        _presenter.Initialize();
        ItemData[] products = System.Array.ConvertAll(_shopItemIds, id => data.ItemTable[id]);
        var trade = new ShopTradeService(player.PlayerInventory, player.GoldWallet, _sellRate);
        var icons = Resources.Load<Assets.HeroEditor4D.Common.Scripts.Collections.IconCollection>(Constants.STR_IconCollection);
        _shopPresenter = new MerchantShopPresenter(player, view, trade, products, icons);
        _shopPresenter.Initialize();
        _npc.Bind(player);
        EventBus.OnNpcInteractInput += HandleInteract;
        _npc.RangeChanged += HandleRangeChanged;
        _player.Died += HandlePlayerDied;
        _presenter.CloseRequested += Close;
    }

    /// <summary>
    /// 자기 이벤트와 퀘스트 화면 담당자의 구독 해제
    /// </summary>
    public void Unbind()
    {
        if (_presenter is null)
        {
            return;
        }
        Close();
        EventBus.OnNpcInteractInput -= HandleInteract;
        _npc.RangeChanged -= HandleRangeChanged;
        _player.Died -= HandlePlayerDied;
        _presenter.CloseRequested -= Close;
        _shopPresenter.Shutdown();
        _shopPresenter = null;
        _presenter.Shutdown();
        _npc.Unbind();
        _presenter = null;
        _player = null;
    }

    private void HandleInteract()
    {
        if (_presenter.IsOpen)
        {
            Close();
            return;
        }
        if (!_npc.IsInRange || !_player.CanTalk)
        {
            return;
        }
        _player.SetDialogueOpen(true);
        _presenter.ShowMenu();
    }

    /// <summary>
    /// 대화 화면과 대화 전용 조작 제한 해제
    /// 사망·씬 이동으로 먼저 제거된 캐릭터는 제외
    /// </summary>
    public void Close()
    {
        _presenter.Close();
        if (_player != null)
        {
            _player.SetDialogueOpen(false);
        }
    }

    private void HandleRangeChanged(bool isInRange)
    {
        if (!isInRange)
        {
            Close();
        }
    }

    private void HandlePlayerDied(Player player) => Close();
    private void OnDisable() => Unbind();
}
