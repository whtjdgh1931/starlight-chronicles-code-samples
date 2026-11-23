using System;
using UnityEngine;

/// <summary>
/// 서버 캐릭터 초기화에 사용할 상태와 접속 직전 위치
/// 로컬 저장 파일과 분리된 네트워크 요청 데이터
/// </summary>
[Serializable]
public sealed class CharacterJoinRequest
{
    public SaveData CharacterState;
    public Vector3 Position;
}
