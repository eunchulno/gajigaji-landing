namespace SlimeTodo.Models;

public enum PetMood
{
    Normal,     // 기본
    Excited,    // 신남 (완료 직후)
    Resting,    // 휴식 (다 끝남)
    Worried     // 걱정 (연체 있음)
}

public enum ViewType
{
    Inbox,
    Today,
    Week,     // 이번 주 보기
    Upcoming,
    Statistics,
    Project  // 특정 프로젝트 보기
}
