using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인터넷 방 이름 입력·목록 조회·참가와 출발·종료 요청 표시
/// 세션 변경 이벤트 기반 화면 갱신
/// </summary>
public sealed class HostSessionView : MonoBehaviour
{
    // 기존 씬 참조를 유지하면서 주소 입력란과 참가 버튼을 인터넷 UI로 재사용
    [SerializeField] private TMP_InputField _addressInput;
    [SerializeField] private Button _createButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _departButton;
    [SerializeField] private Button _leaveButton;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private string _defaultRoomName = "별빛 모험 파티";
    [SerializeField] private Vector2 _roomListSize = new(420, 280);
    [SerializeField] private Vector2 _roomListPosition = new(0, -50);

    private NetworkSessionController _session;
    private InternetRoomListView _roomList;

    /// <summary>
    /// 맵과 로컬 캐릭터 준비 이후 인터넷 접속 UI 연결
    /// 재초기화 전 이전 구독 해제
    /// </summary>
    public void Initialize(NetworkSessionController session)
    {
        Shutdown();
        _session = session;
        _addressInput.characterLimit = InternetRoomService.MaximumRoomNameLength;
        _statusText.richText = false;
        _addressInput.SetTextWithoutNotify(_defaultRoomName);
        _roomList ??= new InternetRoomListView((RectTransform)_createButton.transform.parent, _joinButton,
            _roomListSize, _roomListPosition, HandleJoinRoom);
        _session.Changed += Refresh;
        _createButton.onClick.AddListener(HandleCreateRoom);
        _joinButton.onClick.AddListener(HandleRefreshRooms);
        _departButton.onClick.AddListener(HandleDepart);
        _leaveButton.onClick.AddListener(HandleLeave);
        _addressInput.onValueChanged.AddListener(HandleNameChanged);
        Refresh();
    }

    /// <summary>
    /// 씬 종료 또는 재초기화 시 이벤트 연결 해제
    /// </summary>
    public void Shutdown()
    {
        if (_session == null) { return; }
        _session.Changed -= Refresh;
        _createButton.onClick.RemoveListener(HandleCreateRoom);
        _joinButton.onClick.RemoveListener(HandleRefreshRooms);
        _departButton.onClick.RemoveListener(HandleDepart);
        _leaveButton.onClick.RemoveListener(HandleLeave);
        _addressInput.onValueChanged.RemoveListener(HandleNameChanged);
        _session = null;
    }

    private void OnDisable() => Shutdown();

    private void Refresh()
    {
        bool active = _session.HasSession;
        bool canConnect = _session.CanConnect;
        _addressInput.gameObject.SetActive(!active);
        _createButton.gameObject.SetActive(!active);
        _joinButton.gameObject.SetActive(!active);
        _addressInput.interactable = canConnect;
        _createButton.interactable = canConnect && !string.IsNullOrWhiteSpace(_addressInput.text);
        _joinButton.interactable = canConnect;
        _departButton.gameObject.SetActive(active && _session.IsHost);
        _departButton.interactable = _session.CanStartBossEncounter();
        _leaveButton.gameObject.SetActive(active || _session.IsInternetBusy);
        _leaveButton.interactable = !_session.SceneTransition.IsTransitioning;
        _roomList.Refresh(_session.InternetRooms, !active, canConnect);

        if (_session.LastStartError.Length > 0) { _statusText.text = _session.LastStartError; }
        else if (_session.SceneTransition.IsTransitioning) { _statusText.text = "보스맵으로 이동 중"; }
        else if (_session.IsInternetBusy) { _statusText.text = _session.InternetStatus; }
        else if (_session.IsJoined)
        {
            string state = _session.IsHost
                ? _departButton.interactable ? "파티 준비 완료 / 출발 가능" : "참가자 준비 중"
                : "접속 완료 / 호스트 출발 대기";
            _statusText.text = _session.InternetRoomName + "\n" + state;
        }
        else { _statusText.text = active ? "접속 및 캐릭터 준비 중 / 나가기로 취소" : _session.InternetStatus; }
    }

    private void HandleNameChanged(string name) => Refresh();

    private async void HandleCreateRoom()
    {
        NetworkSessionController session = _session;
        try { await session.CreateInternetRoomAsync(_addressInput.text); }
        catch (Exception exception) { if (this != null) { Debug.LogException(exception, this); } }
    }

    private async void HandleRefreshRooms()
    {
        NetworkSessionController session = _session;
        try { await session.RefreshInternetRoomsAsync(); }
        catch (Exception exception) { if (this != null) { Debug.LogException(exception, this); } }
    }

    private async void HandleJoinRoom(string roomId)
    {
        NetworkSessionController session = _session;
        try { await session.JoinInternetRoomAsync(roomId); }
        catch (Exception exception) { if (this != null) { Debug.LogException(exception, this); } }
    }

    private async void HandleDepart()
    {
        NetworkSessionController session = _session;
        try
        {
            bool started = await session.TryStartBossEncounterAsync();
            if (!started && this != null && _session == session) { Refresh(); }
        }
        catch (Exception exception)
        {
            if (this != null) { Debug.LogException(exception, this); }
        }
    }

    private void HandleLeave() => _session.StopSession();
}
