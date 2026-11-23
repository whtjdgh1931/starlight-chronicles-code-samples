using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 요청 적용 여부와 검사 판정을 분리한 실행 기록
/// </summary>
[Serializable]
internal sealed class QuestValidationResult
{
    internal enum RequestState
    {
        NotRequested,
        Applied,
        Unchanged,
        Rejected,
        PartiallyApplied,
        Observed,
        Error
    }

    internal enum CheckState
    {
        NotChecked,
        Passed,
        Failed,
        Inconclusive,
        Blocked,
        Error
    }

    [Serializable]
    internal sealed class Outcome
    {
        public RequestState Request;
        public CheckState Verification;
        public string Summary;
        public string Detail;
        public string RequestSummary;
        public string RequestDetail;
        public string NextAction;

        internal Outcome(RequestState request, CheckState verification, string summary, string detail = "")
        {
            Request = request;
            Verification = verification;
            Summary = summary;
            Detail = detail;
        }

        internal static Outcome Operation(bool success, string error, bool changed) =>
            new Outcome(success ? (changed ? RequestState.Applied : RequestState.Unchanged)
                : (changed ? RequestState.PartiallyApplied : RequestState.Rejected),
                CheckState.NotChecked, success ? "게임 API가 성공을 반환했습니다." : "게임 API가 실패를 반환했습니다.",
                success ? "값 변화와 저장 상태는 표 및 별도 검사에서 확인하세요." : error);

        internal static Outcome Blocked(string reason) =>
            new Outcome(RequestState.NotRequested, CheckState.Blocked, "실행하지 못했습니다.", reason);

        internal static Outcome Pending(string reason) =>
            new Outcome(RequestState.NotRequested, CheckState.Inconclusive, "검사 조건이 준비되지 않았습니다.", reason);

        internal static Outcome Check(bool passed, RequestState request, string summary, string evidence) =>
            new Outcome(request, passed ? CheckState.Passed : CheckState.Failed, summary, evidence);
    }

    [Serializable]
    internal sealed class Change
    {
        public string Label;
        public string Before;
        public string After;

        internal Change(string label, string before, string after)
        {
            Label = label;
            Before = before;
            After = after;
        }
    }

    [Serializable]
    internal sealed class History
    {
        public List<QuestValidationResult> Entries = new List<QuestValidationResult>();
    }

    public int Sequence;
    public string Time;
    public string Action;
    public string PlayerName;
    public int Slot = -1;
    public int QuestId;
    public string QuestName;
    public Outcome Result;
    public List<Change> Changes = new List<Change>();

    internal string ToReport()
    {
        var text = new StringBuilder();
        text.AppendLine($"#{Sequence} {Time} · {Action}");
        text.AppendLine($"대상: {PlayerName} / 슬롯 {Slot} / 퀘스트 {QuestId}");
        text.AppendLine("요청 결과: " + RequestLabel(Result.Request));
        text.AppendLine("검사 판정: " + CheckLabel(Result.Verification));
        text.AppendLine(Result.RequestSummary);
        text.AppendLine(Result.RequestDetail);
        text.AppendLine("다음 조치: " + Result.NextAction);
        text.AppendLine(Result.Summary);
        text.AppendLine(Result.Detail);
        foreach (Change row in Changes)
        {
            text.AppendLine($"{row.Label}: {row.Before} → {row.After}");
        }
        return text.ToString();
    }

    internal static string RequestLabel(RequestState state)
    {
        switch (state)
        {
            case RequestState.Applied: return "적용 확인 · 데이터 변경";
            case RequestState.Unchanged: return "요청 성공 · 메모리 변화 없음";
            case RequestState.Rejected: return "요청 거절 · 메모리 변화 없음";
            case RequestState.PartiallyApplied: return "요청 실패 · 메모리 변경 있음";
            case RequestState.Observed: return "실행 후 관찰 · 성공 미확정";
            case RequestState.Error: return "예외 · 적용 범위 확인 필요";
            default: return "게임 요청 없음 · 설정/기록 또는 실행 차단";
        }
    }

    internal static string CheckLabel(CheckState state)
    {
        switch (state)
        {
            case CheckState.Passed: return "통과";
            case CheckState.Failed: return "실패";
            case CheckState.Inconclusive: return "판정 보류";
            case CheckState.Blocked: return "실행 불가";
            case CheckState.Error: return "오류";
            default: return "미검사 · 버튼 실행과 검사 통과는 별개";
        }
    }
}
