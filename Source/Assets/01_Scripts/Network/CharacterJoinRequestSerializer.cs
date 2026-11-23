using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 캐릭터 접속 JSON 변환과 필수 필드 검증
/// 게임 데이터와 생성 영역 검증은 서버 초기화 단계에서 처리
/// </summary>
public sealed class CharacterJoinRequestSerializer
{
    public string Serialize(CharacterJoinRequest request) => JsonUtility.ToJson(request);

    /// <summary>
    /// 상태와 모든 위치 좌표가 명시된 요청만 복원
    /// 누락된 Vector3 필드의 원점 자동 대체 방지
    /// </summary>
    public bool TryDeserialize(string json, out CharacterJoinRequest request, out string errorMessage)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "접속 요청 데이터 누락";
            return false;
        }

        JObject envelope;
        try
        {
            envelope = JObject.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
        }
        catch (JsonReaderException)
        {
            errorMessage = "접속 요청 JSON 형식 오류";
            return false;
        }

        if (envelope[nameof(CharacterJoinRequest.CharacterState)] is not JObject ||
            envelope[nameof(CharacterJoinRequest.Position)] is not JObject position ||
            !IsNumber(position["x"]) || !IsNumber(position["y"]) || !IsNumber(position["z"]))
        {
            errorMessage = "접속 요청의 캐릭터 상태 또는 위치 누락";
            return false;
        }

        CharacterJoinRequest parsedRequest;
        try
        {
            parsedRequest = JsonUtility.FromJson<CharacterJoinRequest>(json);
        }
        catch (ArgumentException)
        {
            errorMessage = "접속 요청 데이터 형식 오류";
            return false;
        }

        Vector3 coordinates = parsedRequest.Position;
        if (!IsFinite(coordinates.x) || !IsFinite(coordinates.y) || !IsFinite(coordinates.z))
        {
            errorMessage = "접속 위치의 유효 숫자 필요";
            return false;
        }

        request = parsedRequest;
        errorMessage = string.Empty;
        return true;
    }

    private static bool IsNumber(JToken token)
    {
        return token is JValue value &&
            (value.Type == JTokenType.Integer || value.Type == JTokenType.Float);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
