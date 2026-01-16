using System.Text.RegularExpressions;

namespace SlimeTodo.Services;

public class ParseResult
{
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public List<string> HashTags { get; set; } = [];
}

public partial class NaturalLanguageParser
{
    public ParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new ParseResult { Title = input };

        var result = new ParseResult { Title = input.Trim() };
        var title = result.Title;

        // 패턴 목록 (우선순위 순서로 검사) - 끝/시작 위치 둘 다 지원
        var patterns = new (Regex endRegex, Regex? startRegex, Func<Match, DateTime?> parser)[]
        {
            // 영문 키워드
            (TodayEndRegex(), TodayStartRegex(), _ => DateTime.Today),
            (TomorrowEndRegex(), TomorrowStartRegex(), _ => DateTime.Today.AddDays(1)),

            // 한글 키워드
            (KorTodayEndRegex(), KorTodayStartRegex(), _ => DateTime.Today),
            (KorTomorrowEndRegex(), KorTomorrowStartRegex(), _ => DateTime.Today.AddDays(1)),
            (KorDayAfterEndRegex(), KorDayAfterStartRegex(), _ => DateTime.Today.AddDays(2)),
            (KorTwoDaysAfterEndRegex(), KorTwoDaysAfterStartRegex(), _ => DateTime.Today.AddDays(3)),

            // 상대적 날짜: +1d, +3d, +1w, +2m
            (RelativeDateEndRegex(), RelativeDateStartRegex(), ParseRelativeDate),

            // 다음주 요일
            (NextWeekDayEndRegex(), NextWeekDayStartRegex(), ParseNextWeekDay),

            // 요일 (이번주 또는 단순)
            (WeekDayEndRegex(), WeekDayStartRegex(), ParseWeekDay),

            // 이번주/다음주/이번달/다음달
            (ThisWeekEndRegex(), ThisWeekStartRegex(), _ => GetEndOfWeek(DateTime.Today)),
            (NextWeekEndRegex(), NextWeekStartRegex(), _ => GetEndOfWeek(DateTime.Today.AddDays(7))),
            (ThisMonthEndRegex(), ThisMonthStartRegex(), _ => GetEndOfMonth(DateTime.Today)),
            (NextMonthEndRegex(), NextMonthStartRegex(), _ => GetEndOfMonth(DateTime.Today.AddMonths(1))),

            // 특정 날짜: 1/15, 1-15
            (DateSlashEndRegex(), DateSlashStartRegex(), ParseMonthDay),

            // 특정 날짜: 1월15일, 1월 15일
            (DateKorEndRegex(), DateKorStartRegex(), ParseMonthDay),
        };

        foreach (var (endRegex, startRegex, parser) in patterns)
        {
            // 먼저 끝에서 검사
            var match = endRegex.Match(title);
            Regex? matchedRegex = null;

            if (match.Success)
            {
                matchedRegex = endRegex;
            }
            // 끝에 없으면 시작에서 검사
            else if (startRegex != null)
            {
                match = startRegex.Match(title);
                if (match.Success)
                {
                    matchedRegex = startRegex;
                }
            }

            if (match.Success && matchedRegex != null)
            {
                var parsedDate = parser(match);
                if (parsedDate.HasValue)
                {
                    result.DueDate = parsedDate;
                    title = matchedRegex.Replace(title, "").Trim();
                    break;
                }
            }
        }

        // 해시태그 파싱: #태그명 (여러 개 가능)
        var hashTagMatches = HashTagRegex().Matches(title);
        foreach (Match hashTagMatch in hashTagMatches)
        {
            result.HashTags.Add(hashTagMatch.Groups[1].Value.Trim());
        }
        if (result.HashTags.Count > 0)
        {
            title = HashTagRegex().Replace(title, "").Trim();
        }

        // 제목이 비어있으면 원본 유지
        result.Title = string.IsNullOrWhiteSpace(title) ? input.Trim() : title;

        return result;
    }

    private static DateTime? ParseRelativeDate(Match match)
    {
        if (!int.TryParse(match.Groups[1].Value, out int amount))
            return null;

        var unit = match.Groups[2].Value.ToLower();
        return unit switch
        {
            "d" => DateTime.Today.AddDays(amount),
            "w" => DateTime.Today.AddDays(amount * 7),
            "m" => DateTime.Today.AddMonths(amount),
            _ => null
        };
    }

    private static DateTime? ParseWeekDay(Match match)
    {
        var dayName = match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value)
            ? match.Groups[2].Value
            : match.Groups[1].Value;

        var targetDayOfWeek = GetDayOfWeek(dayName);
        if (targetDayOfWeek < 0) return null;

        var today = DateTime.Today;
        var todayDow = (int)today.DayOfWeek;
        if (todayDow == 0) todayDow = 7; // 일요일을 7로

        var daysUntil = targetDayOfWeek - todayDow;
        if (daysUntil <= 0) daysUntil += 7; // 이미 지났으면 다음주

        return today.AddDays(daysUntil);
    }

    private static DateTime? ParseNextWeekDay(Match match)
    {
        var dayName = match.Groups[1].Value;
        var targetDayOfWeek = GetDayOfWeek(dayName);
        if (targetDayOfWeek < 0) return null;

        var today = DateTime.Today;
        var todayDow = (int)today.DayOfWeek;
        if (todayDow == 0) todayDow = 7;

        // 다음주 해당 요일까지의 거리
        var daysUntil = (7 - todayDow) + targetDayOfWeek;

        return today.AddDays(daysUntil);
    }

    private static int GetDayOfWeek(string dayName)
    {
        return dayName switch
        {
            "월" => 1,
            "화" => 2,
            "수" => 3,
            "목" => 4,
            "금" => 5,
            "토" => 6,
            "일" => 7,
            _ => -1
        };
    }

    private static DateTime? ParseMonthDay(Match match)
    {
        if (!int.TryParse(match.Groups[1].Value, out int month) ||
            !int.TryParse(match.Groups[2].Value, out int day))
            return null;

        if (month < 1 || month > 12 || day < 1 || day > 31)
            return null;

        var today = DateTime.Today;
        var year = today.Year;

        try
        {
            var targetDate = new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
            if (targetDate < today)
            {
                year++;
                targetDate = new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
            }
            return targetDate;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime GetEndOfWeek(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        if (dayOfWeek == 0) return date; // 이미 일요일
        return date.AddDays(7 - dayOfWeek);
    }

    private static DateTime GetEndOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
    }

    // 영문 키워드 - 끝
    [GeneratedRegex(@"\btoday\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TodayEndRegex();

    [GeneratedRegex(@"\btomorrow\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TomorrowEndRegex();

    // 영문 키워드 - 시작
    [GeneratedRegex(@"^\s*today\b", RegexOptions.IgnoreCase)]
    private static partial Regex TodayStartRegex();

    [GeneratedRegex(@"^\s*tomorrow\b", RegexOptions.IgnoreCase)]
    private static partial Regex TomorrowStartRegex();

    // 한글 키워드 - 끝
    [GeneratedRegex(@"오늘\s*$")]
    private static partial Regex KorTodayEndRegex();

    [GeneratedRegex(@"내일\s*$")]
    private static partial Regex KorTomorrowEndRegex();

    [GeneratedRegex(@"모레\s*$")]
    private static partial Regex KorDayAfterEndRegex();

    [GeneratedRegex(@"글피\s*$")]
    private static partial Regex KorTwoDaysAfterEndRegex();

    // 한글 키워드 - 시작
    [GeneratedRegex(@"^\s*오늘\s+")]
    private static partial Regex KorTodayStartRegex();

    [GeneratedRegex(@"^\s*내일\s+")]
    private static partial Regex KorTomorrowStartRegex();

    [GeneratedRegex(@"^\s*모레\s+")]
    private static partial Regex KorDayAfterStartRegex();

    [GeneratedRegex(@"^\s*글피\s+")]
    private static partial Regex KorTwoDaysAfterStartRegex();

    // 상대적 날짜 - 끝/시작
    [GeneratedRegex(@"\+(\d+)([dwmDWM])\s*$")]
    private static partial Regex RelativeDateEndRegex();

    [GeneratedRegex(@"^\s*\+(\d+)([dwmDWM])\s+")]
    private static partial Regex RelativeDateStartRegex();

    // 요일 - 끝/시작
    [GeneratedRegex(@"(이번\s*주\s*)?(월|화|수|목|금|토|일)요?일?\s*$")]
    private static partial Regex WeekDayEndRegex();

    [GeneratedRegex(@"^\s*(이번\s*주\s*)?(월|화|수|목|금|토|일)요?일?\s+")]
    private static partial Regex WeekDayStartRegex();

    [GeneratedRegex(@"다음\s*주\s*(월|화|수|목|금|토|일)요?일?\s*$")]
    private static partial Regex NextWeekDayEndRegex();

    [GeneratedRegex(@"^\s*다음\s*주\s*(월|화|수|목|금|토|일)요?일?\s+")]
    private static partial Regex NextWeekDayStartRegex();

    // 주/달 - 끝/시작
    [GeneratedRegex(@"이번\s*주\s*$")]
    private static partial Regex ThisWeekEndRegex();

    [GeneratedRegex(@"^\s*이번\s*주\s+")]
    private static partial Regex ThisWeekStartRegex();

    [GeneratedRegex(@"다음\s*주\s*$")]
    private static partial Regex NextWeekEndRegex();

    [GeneratedRegex(@"^\s*다음\s*주\s+")]
    private static partial Regex NextWeekStartRegex();

    [GeneratedRegex(@"이번\s*달\s*$")]
    private static partial Regex ThisMonthEndRegex();

    [GeneratedRegex(@"^\s*이번\s*달\s+")]
    private static partial Regex ThisMonthStartRegex();

    [GeneratedRegex(@"다음\s*달\s*$")]
    private static partial Regex NextMonthEndRegex();

    [GeneratedRegex(@"^\s*다음\s*달\s+")]
    private static partial Regex NextMonthStartRegex();

    // 날짜 - 끝/시작
    [GeneratedRegex(@"(\d{1,2})[/\-](\d{1,2})\s*$")]
    private static partial Regex DateSlashEndRegex();

    [GeneratedRegex(@"^\s*(\d{1,2})[/\-](\d{1,2})\s+")]
    private static partial Regex DateSlashStartRegex();

    [GeneratedRegex(@"(\d{1,2})월\s*(\d{1,2})일?\s*$")]
    private static partial Regex DateKorEndRegex();

    [GeneratedRegex(@"^\s*(\d{1,2})월\s*(\d{1,2})일?\s+")]
    private static partial Regex DateKorStartRegex();

    // 해시태그: #태그명 (공백 포함 시 따옴표 사용)
    [GeneratedRegex(@"#([^\s#]+|""[^""]+"")\s*")]
    private static partial Regex HashTagRegex();
}
