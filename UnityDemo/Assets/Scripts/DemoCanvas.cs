using UnityEngine;

public abstract class DemoCanvas : MonoBehaviour
{
    protected static readonly Color Ink = new Color32(16, 22, 35, 255);
    protected static readonly Color Panel = new Color32(29, 40, 58, 255);
    protected static readonly Color Pale = new Color32(231, 240, 252, 255);

    protected abstract string Title { get; }
    protected abstract string Subtitle { get; }
    protected abstract Color Accent { get; }

    private GUIStyle _title;
    private GUIStyle _label;
    private GUIStyle _button;

    private void OnGUI()
    {
        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true };
        }

        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
        Vector3 offset = new Vector3((Screen.width - 960 * scale) / 2, (Screen.height - 540 * scale) / 2, 0);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
        Fill(new Rect(0, 0, 960, 540), Ink);
        Fill(new Rect(34, 102, 892, 2), Accent);
        Label(new Rect(32, 21, 880, 54), Title, Accent, _title);
        Label(new Rect(34, 71, 880, 30), Subtitle, Pale);
        DrawDemo();
        GUI.matrix = previous;
    }

    protected abstract void DrawDemo();

    protected void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    protected void Label(Rect rect, string text, Color color, GUIStyle style = null)
    {
        GUIStyle active = style ?? _label;
        active.normal.textColor = color;
        GUI.Label(rect, text, active);
    }

    protected bool Button(Rect rect, string text, bool enabled = true)
    {
        bool previous = GUI.enabled;
        GUI.enabled = previous && enabled;
        bool pressed = GUI.Button(rect, text, _button);
        GUI.enabled = previous;
        return pressed;
    }

    protected static void Require(bool passed, string step)
    {
        if (!passed)
        {
            throw new System.InvalidOperationException(step);
        }
    }

    public abstract void CheckScenario();
}
