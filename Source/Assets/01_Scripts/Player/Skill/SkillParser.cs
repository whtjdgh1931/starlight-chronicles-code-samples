using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 스킬 데이터 변환
/// </summary>
public static class SkillParser
{
    /// <summary>
    /// int 문자열 (1*2)형식을[1,1]형식으로
    /// </summary>
    /// <param Name="rawInput"></param>
    /// <returns></returns>
    public static int[] ParseIntArray(string rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            Debug.LogWarning("스킬 데이터 int 배열 변환 실패");

            return new int[] { 1 };
        }

        List<int> list = new List<int>();
        string[] groups = rawInput.Split(',');

        foreach (var group in groups)
        {
            string token = group.Trim();
            if (token.Contains("*"))
            {
                string[] parts = token.Split('*');
                int value = int.Parse(parts[0]);
                int count = int.Parse(parts[1]);
                for (int i = 0; i < count; i++)
                {
                    list.Add(value);
                }
            }
            else
            {
                list.Add(int.Parse(token));
            }
        }
        return list.ToArray();
    }

    /// <summary>
    /// float 배열 변화
    /// </summary>
    /// <param Name="rawInput"></param>
    /// <returns></returns>
    public static float[] ParseFloatArray(string rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            Debug.LogWarning("스킬 데이터 float 배열 변환 실패");

            return new float[] { 1.0f };
        }

        List<float> list = new List<float>();
        string[] groups = rawInput.Split(',');

        foreach (var group in groups)
        {
            string token = group.Trim();
            if (token.Contains("*"))
            {
                string[] parts = token.Split('*');
                float value = float.Parse(parts[0]);
                int count = int.Parse(parts[1]);
                for (int i = 0; i < count; ++i)
                {
                    list.Add(value);
                }
            }
            else
            {
                list.Add(float.Parse(token));
            }
        }
        return list.ToArray();
    }

    /// <summary>
    /// Vector2 배열 변환
    /// </summary>
    /// <param Name="rawInput"></param>
    /// <returns></returns>
    public static Vector2[] ParseVector2Array(string rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
        {
            Debug.LogWarning("스킬 데이터 백터 배열 변환 실패");
            return new Vector2[] { Vector2.one };
        }
        List<Vector2> list = new List<Vector2>();

        List<string> groups = new List<string>();
        int bracketDepth = 0;
        int startIndex = 0;
        for (int i = 0; i < rawInput.Length; i++)
        {
            char c = rawInput[i];
            if (c == '(')
            {
                bracketDepth++;
            }
            else if (c == ')')
            {
                bracketDepth--;
            }
            else if (c == ',' && bracketDepth == 0)
            {
                groups.Add(rawInput.Substring(startIndex, i - startIndex));
                startIndex = i + 1;
            }

        }
        if (startIndex < rawInput.Length)
        {
            groups.Add(rawInput.Substring(startIndex));
        }

        foreach (var group in groups)
        {
            string token = group.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }
            if (token.Contains("*"))
            {
                string[] parts = token.Split("*");
                string vectorToken = parts[0].Trim();
                int count = int.Parse(parts[1]);

                Vector2 parseVector = ParseSingleVector(vectorToken);
                for (int i = 0; i < count; ++i)
                {
                    list.Add(parseVector);
                }
            }

            else
            {
                list.Add(ParseSingleVector(token));
            }

        }
        return list.ToArray();
    }

    /// <summary>
    /// 벡터 조합
    /// </summary>
    /// <param Name="vectorToken"></param>
    /// <returns></returns>
    private static Vector2 ParseSingleVector(string vectorToken)
    {
        //괄화 제거
        string cleanToken = vectorToken.Replace("(", "").Replace(")", "").Trim();
        string[] xy = cleanToken.Split(',');
        if (xy.Length == 2)
        {
            float x = float.Parse(xy[0]);
            float y = float.Parse(xy[1]);
            return new Vector2(x, y);
        }
        Debug.LogWarning("벡터 변환 실패");
        return Vector2.one;
    }
}


