using UnityEngine;

public sealed class PortfolioDemo : DemoCanvas
{
    private enum Phase { Ready, Tracking, Locked, Volley, Resolved }

    protected override string Title => "Starlight Chronicles";
    protected override string Subtitle => "Move while the boss tracks you, then dodge when the lanes turn gold.";
    protected override Color Accent => new Color32(105, 220, 242, 255);

    private Phase _phase;
    private float _time;
    private int _lane = 2;
    private int _lockedLane = 2;
    private int _hp = 3;
    private string _result = "Ready";

    private static readonly Color Gold = new Color32(245, 204, 114, 255);
    private static readonly Color Danger = new Color32(235, 106, 145, 255);

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void BeginAttack()
    {
        if ((_phase != Phase.Ready && _phase != Phase.Resolved) || _hp == 0) { return; }
        _time = 0;
        _phase = Phase.Tracking;
        _result = "Tracking";
    }

    private void Tick(float delta)
    {
        _time += delta;
        switch (_phase)
        {
            case Phase.Tracking:
                if (_time >= 1.65f)
                {
                    _lockedLane = _lane;
                    _phase = Phase.Locked;
                    _result = "Aim locked";
                    _time -= 1.65f;
                }
                break;
            case Phase.Locked:
                if (_time >= 0.55f)
                {
                    _phase = Phase.Volley;
                    _result = "Volley";
                    _time -= 0.55f;
                }
                break;
            case Phase.Volley:
                if (_time >= 0.65f)
                {
                    bool hit = InVolley(_lane, _lockedLane);
                    if (hit) { _hp--; }
                    _result = hit ? "Hit" : "Dodged";
                    _phase = Phase.Resolved;
                }
                break;
        }
    }

    private static bool InVolley(int lane, int aim) => Mathf.Abs(lane - aim) <= 1;

    private void ResetEncounter()
    {
        _hp = 3;
        _lane = 2;
        _phase = Phase.Ready;
        _result = "Ready";
    }

    protected override void DrawDemo()
    {
        Fill(new Rect(34, 124, 892, 285), Panel);
        Label(new Rect(54, 143, 840, 34), $"HP {_hp}/3    |    {_result}", Pale);
        for (int lane = 0; lane < 5; lane++)
        {
            float x = 74 + lane * 172;
            Fill(new Rect(x, 181, 126, 181), Ink);
            int aim = _phase == Phase.Tracking ? _lane : _lockedLane;
            if (InVolley(lane, aim))
            {
                if (_phase == Phase.Tracking || _phase == Phase.Locked)
                {
                    Color line = _phase == Phase.Locked ? Gold : Danger;
                    Fill(new Rect(x + 61, 183, 4, 173), line);
                }
                else if (_phase == Phase.Volley)
                {
                    float projectileY = Mathf.Lerp(183, 332, _time / 0.65f);
                    Fill(new Rect(x + 55, projectileY, 16, 14), Gold);
                }
            }

            if (_lane == lane) { Fill(new Rect(x + 50, 333, 26, 20), Accent); }
            if (Button(new Rect(x + 5, 367, 116, 34), $"Lane {lane + 1}")) { _lane = lane; }
        }

        bool canStart = (_phase == Phase.Ready || _phase == Phase.Resolved) && _hp > 0;
        if (Button(new Rect(34, 433, 204, 48), "Start boss attack", canStart)) { BeginAttack(); }
        if (Button(new Rect(250, 433, 182, 48), "Reset encounter")) { ResetEncounter(); }
        Label(new Rect(34, 494, 892, 34), "Pink tracks your lane. Gold shows the three lanes that will be hit.", Pale);
    }

    public override void CheckScenario()
    {
        ResetEncounter();
        BeginAttack();
        Tick(1.7f);
        Require(_phase == Phase.Locked && _lockedLane == 2, "Aim failed to lock");
        _lane = 4;
        Tick(0.6f);
        Require(_phase == Phase.Volley && _lockedLane == 2, "Volley changed the locked lane");
        Tick(0.7f);
        Require(_result == "Dodged" && _hp == 3, "Outside lane took damage");
        BeginAttack();
        Tick(1.7f);
        Tick(0.6f);
        Tick(0.7f);
        Require(_result == "Hit" && _hp == 2, "Locked lane did not take exactly one hit");
        ResetEncounter();
    }
}
