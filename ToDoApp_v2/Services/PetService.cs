using SlimeTodo.Models;

namespace SlimeTodo.Services;

public class PetService
{
    private readonly TaskService _taskService;
    private readonly Random _random = new();

    // ============================================
    // 가지 캐릭터 대사 (50개)
    // 핵심: 응원하지 않고, 옆에 있을 뿐
    // ============================================

    // A. 하루 첫 실행 (10개)
    private static readonly string[] GreetingMessages =
    [
        "왔네.",
        "잠깐 들른 거지?",
        "그냥 켜도 돼.",
        "오늘은 어떤 날이려나.",
        "아무 일 없어도 괜찮아.",
        "여기 조용하지.",
        "천천히 해.",
        "한 번 보고 가.",
        "들렀다 가도 돼.",
        "그래, 여기."
    ];

    // B. 오늘 첫 완료 (10개) - 가장 중요
    private static readonly string[] FirstCompletionMessages =
    [
        "오... 했네.",
        "시작했네.",
        "이게 제일 어려운 거잖아.",
        "좋네.",
        "이 정도면 충분해.",
        "한 번 움직이면, 그걸로 돼.",
        "괜찮은 선택이었어.",
        "오늘은 여기서 이미 이김.",
        "음. 좋다.",
        "해냈네, 하나."
    ];

    // C. 완료할 때 일반 (8개) - 20% 확률로만 표시
    private static readonly string[] CompletionMessages =
    [
        "또 하나 지나갔네.",
        "깔끔.",
        "좋아.",
        "그렇게 하는 거지.",
        "부담 덜었네.",
        "작게라도, 됐어.",
        "응.",
        "나쁘지 않네."
    ];

    // D. Today 0개 (8개) - 비움 칭찬
    private static readonly string[] AllDoneMessages =
    [
        "오늘은 이대로도 괜찮아.",
        "이제 쉬자.",
        "할 일 없는 날도 필요해.",
        "여기까지면 됐어.",
        "가벼워졌네.",
        "오늘은 비어도 좋아.",
        "끝.",
        "응, 휴식."
    ];

    // E. 연체 있음 (6개) - 10% 확률로만, 절대 압박 X
    private static readonly string[] OverdueMessages =
    [
        "오늘은 좀 그런 날이지.",
        "내일 해도 돼.",
        "괜찮아. 미뤄도 돼.",
        "지금은 쉬자.",
        "한 번에 다 안 해도 돼.",
        "천천히 다시 오면 돼."
    ];

    // F-1. 어제 Today 올클리어 후 접속 (4개)
    private static readonly string[] YesterdayPraiseMessages =
    [
        "어제는 좀 괜찮았던 것 같더라.",
        "어제, 좋았어.",
        "어제 잘 지나갔네.",
        "어제는 가벼웠지."
    ];

    // F-2. 2일 이상 미접속 후 접속 (4개)
    private static readonly string[] WelcomeBackMessages =
    [
        "오랜만이다.",
        "다시 왔네.",
        "여기 있었지.",
        "그냥 와도 돼."
    ];

    // G. 연속 완료 (4개)
    private static readonly string[] StreakMessages =
    [
        "오 {0}개째.",
        "{0}연속이네.",
        "계속 하네... 대단.",
        "... {0}개?"
    ];

    // H. 레벨업 (4개)
    private static readonly string[] LevelUpMessages =
    [
        "Lv.{0}. 축하.",
        "레벨 올랐네. {0}.",
        "오... Lv.{0}.",
        "{0}레벨."
    ];

    // I. 클릭했을 때 (10개) - 툭 던지는 반응
    private static readonly string[] PokeMessages =
    [
        "뭐.",
        "왜.",
        "...?",
        "심심해?",
        "건드리지 마.",
        "...",
        "뭔데.",
        "그래.",
        "음.",
        "할 일이나 해."
    ];

    // J. 앱 종료할 때 (6개) - 짧은 작별
    private static readonly string[] GoodbyeMessages =
    [
        "나중에 봐.",
        "또 와.",
        "응.",
        "잘 가.",
        "...",
        "다음에."
    ];

    public PetService(TaskService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>
    /// 앱 시작 시 호출 - 기억 기반 인사 반환
    /// </summary>
    public (string? message, bool shouldShow) GetAppStartGreeting()
    {
        var status = _taskService.GetPetStatus();
        var today = DateTime.Today;

        // 이미 오늘 인사했으면 스킵
        if (status.LastAppOpenDate.Date == today && status.HasShownGreetingToday)
        {
            return (null, false);
        }

        // 날짜 변경 시 어제 올클리어 여부 저장
        if (status.LastAppOpenDate.Date != today)
        {
            // 어제 Today가 비었는지 체크 (어제 마지막 상태)
            var yesterdayAllDone = status.TodayCompleted > 0 &&
                                   !_taskService.GetTodayTasks().Any();
            status.YesterdayWasAllDone = yesterdayAllDone;
            status.HasShownGreetingToday = false;
        }

        var daysSinceLastOpen = (today - status.LastAppOpenDate.Date).Days;

        string message;

        // 우선순위: WelcomeBack > YesterdayPraise > Greeting
        if (daysSinceLastOpen >= 2)
        {
            message = WelcomeBackMessages[_random.Next(WelcomeBackMessages.Length)];
        }
        else if (status.YesterdayWasAllDone && daysSinceLastOpen >= 1)
        {
            message = YesterdayPraiseMessages[_random.Next(YesterdayPraiseMessages.Length)];
        }
        else
        {
            message = GreetingMessages[_random.Next(GreetingMessages.Length)];
        }

        // 상태 업데이트
        status.LastAppOpenDate = today;
        status.HasShownGreetingToday = true;
        _taskService.UpdatePetStatus();

        return (message, true);
    }

    public PetMood GetCurrentMood()
    {
        var todayTasks = _taskService.GetTodayTasks().ToList();

        if (!todayTasks.Any())
        {
            return PetMood.Resting;
        }

        if (_taskService.HasOverdueTasks())
        {
            return PetMood.Worried;
        }

        return PetMood.Normal;
    }

    public string GetCompletionMessage(bool isFirstToday, bool isAllDone)
    {
        if (isAllDone)
        {
            return AllDoneMessages[_random.Next(AllDoneMessages.Length)];
        }

        if (isFirstToday)
        {
            return FirstCompletionMessages[_random.Next(FirstCompletionMessages.Length)];
        }

        return CompletionMessages[_random.Next(CompletionMessages.Length)];
    }

    /// <summary>
    /// 일반 완료 시 반응 여부 (20% 확률)
    /// </summary>
    public bool ShouldShowNormalCompletionReaction()
    {
        return _random.Next(100) < 20;
    }

    public string GetStreakMessage(int streak)
    {
        var template = StreakMessages[_random.Next(StreakMessages.Length)];
        return string.Format(template, streak);
    }

    /// <summary>
    /// 연체 메시지 (10% 확률로만 표시)
    /// </summary>
    public string? GetOverdueMessage()
    {
        if (_random.Next(100) < 10)
        {
            return OverdueMessages[_random.Next(OverdueMessages.Length)];
        }
        return null;
    }

    public string GetWorriedMessage()
    {
        return OverdueMessages[_random.Next(OverdueMessages.Length)];
    }

    public string GetLevelUpMessage(int newLevel)
    {
        var template = LevelUpMessages[_random.Next(LevelUpMessages.Length)];
        return string.Format(template, newLevel);
    }

    /// <summary>
    /// Today 0개일 때 메시지 (하루 1회만)
    /// </summary>
    public string GetRestingMessage()
    {
        return AllDoneMessages[_random.Next(AllDoneMessages.Length)];
    }

    /// <summary>
    /// 캐릭터 클릭 시 반응
    /// </summary>
    public string GetPokeMessage()
    {
        return PokeMessages[_random.Next(PokeMessages.Length)];
    }

    public PetStatus GetStatus() => _taskService.GetPetStatus();

    /// <summary>
    /// 앱 종료 시 작별 인사
    /// </summary>
    public string GetGoodbyeMessage()
    {
        return GoodbyeMessages[_random.Next(GoodbyeMessages.Length)];
    }
}
