using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GoldTester))]
public class GoldTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. GoldToAdd 변수 등 원래 인스펙터에 보이던 것들을 그려줌
        base.OnInspectorGUI();

        GoldTester tester = (GoldTester)target;

        EditorGUILayout.Space(10); // UI 사이 여백 띄우기

        // 2. 높이 40짜리 커다란 테스트 버튼 생성
        if (GUILayout.Button($"테스트용 골드 추가 (+{tester.GoldToAdd} G)", GUILayout.Height(40)))
        {
            if (Application.isPlaying)
            {
                // 씬의 플레이어 지갑에 테스트 골드 추가
                Player player = FindAnyObjectByType<Player>();

                if (player != null && player.GoldWallet != null)
                {
                    player.GoldWallet.Add(tester.GoldToAdd);
                    Debug.Log($"<color=yellow>[테스트]</color> 골드가 추가되었습니다. 현재 골드: {player.GoldWallet.CurrentGold}");
                }
                else
                {
                    Debug.LogWarning("플레이어 지갑을 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning("게임을 실행 중(Play Mode)일 때만 골드를 추가할 수 있습니다!");
            }
        }
    }
}




