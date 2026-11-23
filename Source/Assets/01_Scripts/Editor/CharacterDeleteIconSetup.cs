using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면의 삭제 버튼에 쓰레기통 이미지 연결
/// </summary>
public static class CharacterDeleteIconSetup
{
    private const string IconPath = "Assets/Art/UI/CharacterDeleteTrash.png";
    private const string ScenePath = "Assets/00_Scenes/SelectScene.unity";
    private static readonly string[] SlotProperties = { "_slot1", "_slot2", "_slot3" };
    private static readonly Vector2 ButtonSize = new Vector2(64f, 64f);

    /// <summary>
    /// 이미지의 Sprite 임포트와 기존 삭제 버튼 외형 교체
    /// </summary>
    [MenuItem("Tools/UI/Apply Character Delete Icon")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 삭제 아이콘 적용 필요");
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (wasLoaded && scene.isDirty)
        {
            throw new InvalidOperationException("SelectScene의 기존 변경 사항 저장 후 실행 필요");
        }

        Sprite sprite = ImportIcon();
        if (!wasLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        bool saved = false;
        try
        {
            CharacterSelectionUI selection = FindSelection(scene);
            var selectionSettings = new SerializedObject(selection);
            var buttons = new Button[SlotProperties.Length];
            var images = new Image[SlotProperties.Length];
            for (int index = 0; index < SlotProperties.Length; index++)
            {
                var slot = (CharacterSlotUI)selectionSettings.FindProperty(SlotProperties[index]).objectReferenceValue;
                if (slot == null)
                {
                    throw new MissingReferenceException("캐릭터 슬롯 참조 필요: " + SlotProperties[index]);
                }
                var slotSettings = new SerializedObject(slot);
                var buttonObject = (GameObject)slotSettings.FindProperty("_deleteButton").objectReferenceValue;
                if (buttonObject == null || !buttonObject.TryGetComponent(out buttons[index]) ||
                    !buttonObject.TryGetComponent(out images[index]))
                {
                    throw new MissingReferenceException(slot.name + ": 삭제 Button과 Image 참조 필요");
                }
            }

            for (int index = 0; index < buttons.Length; index++)
            {
                Image image = images[index];
                Undo.RecordObject(image, "삭제 아이콘 적용");
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                EditorUtility.SetDirty(image);

                RectTransform rect = image.rectTransform;
                Undo.RecordObject(rect, "삭제 버튼 크기 변경");
                rect.sizeDelta = ButtonSize;
                EditorUtility.SetDirty(rect);

                foreach (TMP_Text label in buttons[index].GetComponentsInChildren<TMP_Text>(true))
                {
                    Undo.RecordObject(label.gameObject, "삭제 글자 숨김");
                    label.gameObject.SetActive(false);
                    EditorUtility.SetDirty(label.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("삭제 아이콘을 적용한 SelectScene 저장 실패");
            }
            saved = true;
            Debug.Log("캐릭터 슬롯 3개의 삭제 아이콘 적용 완료. 기존 삭제 확인과 클릭 이벤트 유지", selection);
        }
        finally
        {
            // 저장 실패 또는 부분 적용 시 열린 씬을 유지하여 검토 가능
            if (!wasLoaded && saved)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static Sprite ImportIcon()
    {
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("쓰레기통 PNG 임포트 확인 필요: " + IconPath);
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        if (sprite == null)
        {
            throw new InvalidOperationException("쓰레기통 Sprite 로드 실패: " + IconPath);
        }
        return sprite;
    }

    private static CharacterSelectionUI FindSelection(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CharacterSelectionUI selection = root.GetComponentInChildren<CharacterSelectionUI>(true);
            if (selection != null)
            {
                return selection;
            }
        }
        throw new MissingReferenceException("SelectScene에 CharacterSelectionUI 필요");
    }
}
