using System;
using System.Linq;
using System.Reflection;


/// <summary>
/// 필드값과 캐싱한 데이터를 매핑하는 유틸리티 클래스
/// </summary>
public static class DataBinder
{
    /// <summary>
    /// 이름을 매칭하여 자동 복사
    /// </summary>
    /// <param Name="data"></param>
    /// <param Name="field"></param>
    public static void Bind(object data, object field)
    {
        // 데이터 변수 목록
        FieldInfo[] dataFields = data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        //필드 목록
        FieldInfo[] fields = field.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (FieldInfo dField in dataFields)
        {
            //매칭
            FieldInfo tField = fields.FirstOrDefault(f =>
            f.Name.Replace("_", "").Equals(dField.Name, StringComparison.OrdinalIgnoreCase));

            //대입
            if (tField != null && tField.FieldType == dField.FieldType)
            {
                tField.SetValue(field, dField.GetValue(data));
            }
        }

    }
}
