# GajiGaji (가지가지) - ToDoApp_v2

## 프로젝트 개요
귀여운 가지 캐릭터와 함께하는 WPF 기반 할일 관리 데스크톱 앱

- **버전**: 1.2.0
- **플랫폼**: .NET 8.0 WPF (Windows 10+)
- **아키텍처**: MVVM 패턴

## 프로젝트 구조

```
ToDoApp_v2/
├── ToDoApp_v2/                    # 메인 WPF 앱
│   ├── App.xaml                   # 앱 진입점, 리소스 등록
│   ├── MainWindow.xaml            # 메인 윈도우 (1100x700)
│   │
│   ├── Models/                    # 데이터 모델
│   │   ├── TodoTask.cs            # 할일 모델 (서브태스크, 반복, 알림)
│   │   ├── Project.cs             # 프로젝트 모델
│   │   ├── AppData.cs             # 루트 데이터 컨테이너
│   │   ├── PetStatus.cs           # 펫 상태 모델
│   │   └── Enums.cs               # PetMood, ViewType 등
│   │
│   ├── ViewModels/                # 비즈니스 로직
│   │   ├── MainViewModel.cs       # 메인 상태 관리 (핵심)
│   │   ├── TaskItemViewModel.cs   # 개별 할일 관리
│   │   ├── ProjectViewModel.cs    # 프로젝트 관리
│   │   ├── HashTagViewModel.cs    # 해시태그 관리
│   │   ├── WeekViewModel.cs       # 주간 뷰 상태
│   │   ├── StatisticsViewModel.cs # 통계
│   │   ├── ViewModelBase.cs       # MVVM 베이스
│   │   └── RelayCommand.cs        # 커맨드 패턴
│   │
│   ├── Views/                     # UI 컴포넌트
│   │   ├── TaskListView.xaml      # 할일 목록 (메인)
│   │   ├── NavigationView.xaml    # 사이드바 네비게이션
│   │   ├── WeekView.xaml          # 주간 캘린더 뷰
│   │   ├── PetView.xaml           # 펫 캐릭터 표시
│   │   ├── StatisticsView.xaml    # 통계 뷰
│   │   ├── QuickAddBar.xaml       # 빠른 할일 추가
│   │   └── NoteEditorView.xaml    # 메모 편집기 (Quill WYSIWYG)
│   │
│   ├── Services/                  # 서비스 레이어
│   │   ├── TaskService.cs         # 할일 CRUD
│   │   ├── StorageService.cs      # JSON 저장/로드
│   │   ├── PetService.cs          # 펫 무드 관리
│   │   ├── UndoService.cs         # Undo/Redo
│   │   ├── ThemeService.cs        # 다크/라이트 테마
│   │   ├── NotificationService.cs # 알림
│   │   ├── TrayIconService.cs     # 시스템 트레이
│   │   └── NaturalLanguageParser.cs # 자연어 파싱
│   │
│   ├── Converters/                # XAML 값 변환기
│   │   └── BoolToVisibilityConverter.cs
│   │
│   ├── Resources/                 # 스타일 & 테마
│   │   ├── Styles.xaml            # 라이트 테마
│   │   ├── DarkTheme.xaml         # 다크 테마
│   │   └── Editor/                # Quill 에디터 리소스
│   │       ├── index.html         # 에디터 HTML 템플릿
│   │       ├── editor.css         # 에디터 테마 스타일
│   │       ├── quill.min.js       # Quill 라이브러리 (오프라인)
│   │       └── quill.snow.css     # Quill Snow 테마 (오프라인)
│   │
│   ├── Fonts/                     # 커스텀 폰트
│   └── Assets/                    # 이미지 에셋
│
└── landing-page/                  # 랜딩 페이지 (HTML)
```

## 주요 기능

| 기능 | 설명 | 관련 파일 |
|------|------|----------|
| 할일 관리 | 생성, 수정, 삭제, 완료 | TaskService.cs, TaskItemViewModel.cs |
| 서브태스크 | 중첩 할일 지원 | TodoTask.cs, TaskListView.xaml |
| 해시태그 | 색상 태그 분류 | HashTagViewModel.cs |
| 프로젝트 | 할일 그룹화 | ProjectViewModel.cs |
| 반복 설정 | 일/주/월/년 반복 | RecurrenceType enum |
| 알림 | Windows 토스트 알림 | NotificationService.cs |
| 펫 시스템 | 무드 변화 (Normal/Excited/Resting/Worried) | PetService.cs |
| 통계 | 스트릭, 완료 기록 | StatisticsViewModel.cs |
| 테마 | 다크/라이트 모드 | ThemeService.cs |
| Undo/Redo | 작업 취소/재실행 | UndoService.cs |
| 메모 | Quill WYSIWYG 에디터, 이미지 지원 | NoteEditorView.xaml, Resources/Editor/ |

## 뷰 타입 (ViewType)

- `Inbox` - 미분류 할일
- `Today` - 오늘 할일
- `Week` - 주간 캘린더
- `Upcoming` - 예정된 할일
- `Statistics` - 통계
- `Project` - 프로젝트별 할일

## 키보드 단축키

| 키 | 동작 |
|----|------|
| N | 새 할일 추가 |
| F | 검색 |
| M | 메모 패널 토글 |
| Esc | 메모 패널/검색 닫기 |
| Ctrl+Z | 실행 취소 (메모 에디터에서는 텍스트 실행취소) |
| Ctrl+Y | 다시 실행 |

## 데이터 저장

- **파일**: `data.json` (앱 로컬 폴더)
- **포맷**: JSON with schema versioning
- **자동 저장**: 변경 시 즉시 저장

## 디자인 시스템

### 컬러 팔레트
```
Accent:     #7C3AED (보라)
Important:  #FF9500 (주황)
Completed:  #058527 (초록)
Error:      #E85D75 (빨강)
```

### 폰트
- Pretendard Variable (본문)
- Galmuri11 (포인트)

## 빌드 & 실행

```bash
# 빌드
dotnet build

# 실행
dotnet run --project ToDoApp_v2

# 퍼블리시 (단일 실행 파일)
dotnet publish -c Release -r win-x64 --self-contained
```

## 개발 가이드라인

### MVVM 패턴
- View: XAML UI만 담당
- ViewModel: UI 로직, 상태 관리
- Model: 순수 데이터 구조
- Service: 비즈니스 로직, I/O

### 코드 컨벤션
- C# 명명 규칙 준수
- nullable reference types 활성화
- XAML에서는 DynamicResource 사용 (테마 지원)

### 새 기능 추가 시
1. Model 정의 (필요시)
2. Service에 로직 구현
3. ViewModel에서 Service 사용
4. View에서 바인딩

## 최근 변경 사항

### v1.2.0
- Quill WYSIWYG 에디터 적용 (Markdig 마크다운 에디터 대체)
- 이미지 붙여넣기/드래그앤드롭 지원
- 완전한 오프라인 지원 (CDN 의존성 제거)
- 메모 패널 토글 기능 (같은 할일 클릭 시 패널 열기/닫기)
- 앱 창 크기 조정 (800x600 → 1100x700)
- Ctrl+Z 키 처리 개선 (에디터 vs 앱 실행취소 분리)

### v1.1.1
- Empty state에 앱 사용법 가이드 추가
- 빠른 시작 가이드 표시 (할일이 없을 때)

## 알려진 이슈

- (현재 없음)

## TODO / 향후 계획

- [ ] 클라우드 동기화
- [ ] 모바일 앱 연동
- [ ] 위젯 지원
