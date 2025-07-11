using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;

/// 베타테스트빌드 - 2025.07.11

// 이 파일은 DesktopWidgetApp 프로젝트의 일부로, WPF 기반의 데스크탑 위젯 애플리케이션을 구현하는 코드입니다.
// MainWindow.xaml 파일과 연결되어 있으며, 애플리케이션의 주요 기능을 담당합니다.


// 중요: 이 네임스페이스를 사용자님의 프로젝트 이름과 일치시켜 주세요. (예: namespace DGCB_Tweaks)
// ^안함 / 파일 전체적으로 건드려야해서 귀찮음

//1. 네임스페이스
//얘는 파일 전체적으로 모두 모아놓는다 보면됨
// C# 언어 특성상 한 네임스페이스, 클래스들 안에 모든 코드를 넣어야 해서 이런 형식임
namespace DesktopWidgetApp
{
    #region API Response Classes
    // 2. Region - 뭉탱이들 모아놓는거라 보면됨. 실제 코드와는 관련없는 에디터 전용 기능

    //    이걸 사용하면 코드가 가독성좋고 깔끔하게 정리되고, 나중에 유지보수할 때도 편함

    //    예를 들어, API 응답 구조가 바뀌면 이 부분만 수정하면 되니까 유지보수가 쉬워짐

    // --- API 응답 구조 클래스들 ---

    // 3. JsonProperty 
    //    이건 Newtonsoft.Json 라이브러리의 JsonProperty 어트리뷰트로, JSON 직렬화/역직렬화 시 필드 이름을 지정하는 데 사용됨
    // 매핑과 프로퍼티는 JSON 필드 이름과 C# 클래스 프로퍼티 이름을 연결하는 역할을 함 - API로부터 받는 데이터에서 데이터의 머리를 인식한다고 보면 됨.
    // List<T>는 C#의 같은 타입의 개체를 순서대로 저장하는 컬렉션 타입임

    public class MealServiceApiResponse { [JsonProperty("mealServiceDietInfo")] public List<ServiceContentBase<ServiceHeadInfo, MealDataRow>> MealServiceDietInfo { get; set; } }
    public class HisTimetableApiResponse { [JsonProperty("hisTimetable")] public List<ServiceContentBase<ServiceHeadInfo, TimetableDataRow>> HisTimetable { get; set; } }

    public class ServiceContentBase<THead, TRow> where THead : class where TRow : class
    {
        [JsonProperty("head")] public List<THead> Head { get; set; }
        [JsonProperty("row")] public List<TRow> Row { get; set; }
    } // 이건나도모르겠네
    public class ServiceHeadInfo { [JsonProperty("list_total_count")] public int? ListTotalCount { get; set; } [JsonProperty("RESULT")] public ServiceResult Result { get; set; } }
    // 이건 API 응답의 헤더 정보를 담는 클래스임. list_total_count는 전체 데이터 개수를 나타내고, RESULT는 요청 결과 코드와 메시지를 담고 있음
    public class ServiceResult { [JsonProperty("CODE")] public string Code { get; set; } [JsonProperty("MESSAGE")] public string Message { get; set; } }
    // 급식 데이터에서 CODE 결과를 받음. 이코드가 "INFO-200"이면 데이터가 없다는 뜻임

    public class MealDataRow { [JsonProperty("MLSV_YMD")] public string MealDate { get; set; } [JsonProperty("MMEAL_SC_CODE")] public string MealCode { get; set; } [JsonProperty("MMEAL_SC_NM")] public string MealName { get; set; } [JsonProperty("DDISH_NM")] public string DishName { get; set; } [JsonProperty("CAL_INFO")] public string CalorieInfo { get; set; } }
    // MLSYV_YMD: 급식 날짜, MMEAL_SC_CODE: 급식 코드, MMEAL_SC_NM: 급식 이름, DDISH_NM: 음식 이름, CAL_INFO: 칼로리 정보
    public class TimetableDataRow { [JsonProperty("ALL_TI_YMD")] public string Date { get; set; } [JsonProperty("PERIO")] public string Period { get; set; } [JsonProperty("ITRT_CNTNT")] public string Subject { get; set; } [JsonProperty("TEACHER_NM")] public string TeacherName { get; set; } }
    // ALL_TI_YMD: 날짜, PERIO: 교시, ITRT_CNTNT: 과목 내용, TEACHER_NM: 선생님 이름
    public class NotionApiResponse<T> { [JsonProperty("results")] public List<T> Results { get; set; } }
    // --- Notion API 응답 구조 클래스들 ---
    // Notion API 응답 구조 클래스들 - Notion에서 페이지를 가져올 때 사용하는 구조임

    // --- 이 밑부턴 코드가 서로 얽혀있어서 능지싸움임 ---
    public class NotionMotdPage { [JsonProperty("properties")] public NotionMotdProperties Properties { get; set; } }
    // NotionMotdPage는 MOTD 페이지의 구조를 나타내는 클래스임. Properties 프로퍼티가 NotionMotdProperties 타입으로 되어 있음
    // NotionMotdProperties는 MOTD 페이지의 속성을 나타내는 클래스임. Message 프로퍼티가 NotionTitleProperty 타입으로 되어 있음
    public class NotionMotdProperties { [JsonProperty("Message")] public NotionTitleProperty Message { get; set; } }
    // 아 귀찮다 이뒤는 알아서보셈

    public class NotionDdayPage { [JsonProperty("properties")] public NotionDdayProperties Properties { get; set; } }
    public class NotionDdayProperties { [JsonProperty("Eventname_NT")] public NotionTitleProperty EventName { get; set; } [JsonProperty("날짜")] public NotionDateProperty EventDate { get; set; } }

    public class NotionTitleProperty { [JsonProperty("title")] public List<NotionRichText> Title { get; set; } }
    // 
    public class NotionDateProperty { [JsonProperty("date")] public NotionDateObject Date { get; set; } }
    public class NotionDateObject { [JsonProperty("start")] public string Start { get; set; } }
    public class NotionRichText { [JsonProperty("text")] public NotionTextContent Text { get; set; } }
    public class NotionTextContent { [JsonProperty("content")] public string Content { get; set; } }
    public class NotionAssessmentPage { [JsonProperty("properties")] public NotionAssessmentProperties Properties { get; set; } }
    public class NotionAssessmentProperties { [JsonProperty("수행평가명")] public NotionTitleProperty AssessmentName { get; set; } [JsonProperty("날짜")] public NotionDateProperty DueDate { get; set; } }


    public class NotionWordPage { [JsonProperty("properties")] public NotionWordProperties Properties { get; set; } }
    public class NotionWordProperties { [JsonProperty("영단어")] public NotionTitleProperty Word { get; set; } }
    #endregion

    public partial class MainWindow : Window
        /// 사실상 이 파일의 전부라 보면됨
        /// 이 클래스 안에 모든 작동코드가 들어있음
    {
        #region Fields and Constants
        private readonly string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DGCBTweaks"); // 앱 데이터 경로 설정 (AppData\DGCBTweaks 폴더 - 설정 저장 등)
        private readonly string settingsFilePath; // 설정 파일 경로 (AppData\DGCBTweaks\settings.xml)
        private readonly string statisticsFilePath; // 통계 파일 경로 (AppData\DGCBTweaks\Statistics.xml)

        private const string NeisApiKey = "4cfaa1386bf64e448aed4060ba841503"; // NEIS API 키 (개발자 등록 후 발급받은 키 - 급식과 시간표 정보를 담당)
        private const string NeisMealApiBaseUrl = "https://open.neis.go.kr/hub/mealServiceDietInfo"; // NEIS 급식 API 기본 URL
        private const string NeisHisTimetableApiBaseUrl = "https://open.neis.go.kr/hub/hisTimetable"; // NEIS 시간표 API 기본 URL
        private const string AtptOfcdcScCode_Fixed = "J10"; // 교육청 코드 (J10은 경기도교육청)
        private const string SdSchulCode_Fixed = "7530601"; // 학교 코드 (효명고)
        private const string MealServiceCode_Fixed = "2"; // 급식 서비스 코드 (2는 중식)

        private const string NotionApiKey = "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"; // Notion API 키 (고정형 - 오늘의 메시지, 오늘의 영단어, 디데이 등의 고정형 기능의 API키를 담당)
        private const string MotdDatabaseId = "22cf2d42beb9802bb872deee72b84d63"; // MOTD 데이터베이스 ID (오늘의 메시지 데이터베이스를 찾는 담당)
        private const string DdayDatabaseId = "20df2d42beb980c09a58fc147a4eb6ba"; // D-Day 데이터베이스 ID (D-Day 기능을 담당)
        private const string WordDatabaseId = "22cf2d42beb980c1a570fed13ddadb3b";
        /// <summary>
        /// jjkfdjklasjkfdasjkdjkdfjkfjfk;jkffjk;jkf;sdjkjk;sjfkdl;jklssadjkdasfjkdjflldjkssjadjakdljflajkljlfaj;fdsjljdsfjfdasjl;sdjkladjasdjadsfjkdl;fjkl;ajdsjfkladjkdjkl;
        /// 여기 바꿔라 브랜치 작업끝나면
        /// </summary>

        private static readonly Random random = new Random(); //랜덤 숫자 생성기 (오늘의 메시지 기능에서 랜덤 번호를 선택하는 데 사용됨)
        #endregion

        #region P/Invoke Definitions
        // P/Invoke를 사용하여 Win32 API 함수들을 호출하기 위한 정의들 - 항상 위 / 항상 아래 등의 윈도우 기능 건드리기용
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex); // GetWindowLong은 윈도우의 속성을 가져오는 함수
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong); // SetWindowLong은 윈도우의 속성을 설정하는 함수
        private const int GWL_EXSTYLE = -20; // GWL_EXSTYLE은 확장 윈도우 스타일을 가져오거나 설정하는 데 사용되는 인덱스
        private const int WS_EX_NOACTIVATE = 0x08000000; // 윈도우에서 창 활성화 막아주는 상수 (창이 클릭되어도 활성화되지 않도록 함)
        private const int WM_MOUSEACTIVATE = 0x0021; // WM_MOUSEACTIVATE는 마우스 클릭 시 창 활성화 메시지.. 라는데 모르겠다
        private const int MA_NOACTIVATE = 3; // MA_NOACTIVATE는 마우스 클릭 시 창을 활성화하지 않도록 하는 상수
        private IntPtr _hWnd; // 현재 윈도우의 핸들 (윈도우를 식별하는 고유한 값 - P/Invoke에서 사용됨)
        #endregion



        #region Constructor and Window Events
        public MainWindow() // 생성자 - 앱 데이터 경로와 파일 경로 설정, UI 초기화
        {
            settingsFilePath = Path.Combine(appDataPath, "settings.xml");
            statisticsFilePath = Path.Combine(appDataPath, "Statistics.xml");
            InitializeComponent(); // UI 초기화 (XAML에서 정의한 UI 요소들을 초기화함 - WPF에서 제일 기본, 중요한 코드)
            this.Loaded += MainWindow_Loaded; 
        }

        protected override void OnSourceInitialized(EventArgs e)
        { // 윈도우 초기화 이벤트 - 윈도우 핸들을 가져오고 메시지 훅을 설정함
            // override는 부모 클래스의 메서드를 재정의하는 키워드임
            
            base.OnSourceInitialized(e);
            _hWnd = new WindowInteropHelper(this).Handle; //여긴나도모름ㅎㅎ
            if (HwndSource.FromHwnd(_hWnd) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
            ApplyWindowActivationStyle(TryLoadAppSettings().ActivationMode); // 현재 설정에 따라 창 활성화 스타일을 적용함 - TryLoadAppSettings()는 설정파일 불러오는거니깐 후설
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) // 윈도우 메시지 처리 메서드 - WM_MOUSEACTIVATE 메시지를 처리하여 창 활성화 모드를 적용함 << 이건뭐지?
        {
            if (msg == WM_MOUSEACTIVATE) { if (TryLoadAppSettings().ActivationMode == WindowActivationMode.NoActivate) { handled = true; return new IntPtr(MA_NOACTIVATE); } } 
            return IntPtr.Zero;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e) // 프로그램 로딩 총괄
        {
            Debug.WriteLine("MainWindow: Loaded 이벤트 시작");
            try
            { // 프로그램이 로드될 때 수행되는 초기화/시작 작업 진행
                UpdateAndSaveStatistics(); // 통계 업데이트 및 저장 (프로그램 실행 횟수, 최초 실행 시간 등 기록)
                LoadSettings(); // 설정 로드 (학년, 반, 투명도, 개인 노션 키 등 사용자 설정을 불러옴)
                SetupWindowProperties(); // 윈도우 속성 설정 (창 활성화 모드, 투명도 등 설정 적용)
                CreateTimetableGrid(); // 시간표 그리드 생성 (시간표 UI 요소를 동적으로 생성함)
                CreatePerformanceAssessmentGrid(); // 수행평가 그리드 생성 (수행평가 UI 요소를 동적으로 생성함)
                await LoadInitialDataAsync(); // 초기 데이터 로드 (디데이, 오늘의 영단어, 급식, 시간표, 수행평가, MOTD 등 초기 데이터를 비동기로 로드함)
            }
            catch (Exception ex) { Debug.WriteLine($"MainWindow_Loaded에서 예외 발생: {ex.Message}"); } // 오류발생 시 디버그 출력
        }

        private async Task LoadInitialDataAsync() // 초기 데이터 로드 메서드 - 앱 시작 시 필요한 데이터를 비동기로 로드함
        {
            Debug.WriteLine("LoadInitialDataAsync 시작");
            var tasks = new List<Task> // 비동기 작업들을 리스트에 추가, Task는 비동기 작업을 나타내는 타입임
            {
                LoadDdayAsync(), // D-Day 데이터 로드
                LoadDailyWordAsync(), // 오늘의 영단어 데이터 로드
                LoadSchoolMealsAsync(), // 급식 데이터 로드
                LoadTimetableDataAsync(), // 시간표 데이터 로드
                LoadPerformanceAssessmentDataAsync(), // 수행평가 데이터 로드
                LoadMotdAsync() // MOTD 데이터 로드
            };
            await Task.WhenAll(tasks); // 모든 비동기 작업이 완료될 때까지 대기
            Debug.WriteLine("LoadInitialDataAsync 완료"); // 모든 초기 데이터 로드가 완료되면 디버그 출력
        }
        #endregion

        #region Settings and App Configuration
        public class AppSettings
        { // 앱 설정 클래스 - 사용자 설정
            public string Grade { get; set; } = "1"; // 학년 데이터를 저장하는 프로퍼티 (기본값은 "1") 
            public string ClassNum { get; set; } = "1"; // get; set;은 데이터를  읽고 쓸 수 있다는 이야기
            public double Opacity { get; set; } = 0.5; // 창 투명도 설정 (0.0 ~ 1.0 사이의 값, 기본값은 0.5)
            public WindowActivationMode ActivationMode { get; set; } = WindowActivationMode.Normal; // 창 활성화 모드 설정 (기본값은 Normal - 일반 모드)
            public bool AutoRunEnabled { get; set; } = false; // 자동 실행 설정 (기본값은 false - 자동 실행 비활성화)

            public string UserNotionApiKey { get; set; } = ""; // 개인 Notion API 키 (사용자가 설정한 키, 기본값은 빈 문자열)
            public string UserNotionDbId { get; set; } = ""; // 개인 Notion 데이터베이스 ID (사용자가 설정한 ID, 기본값은 빈 문자열)
        }
        public enum WindowActivationMode { Normal, Topmost, NoActivate } // 창 활성화 모드 열거형 - Normal(일반), Topmost(항상 위), NoActivate(활성화 안함) 세 가지 모드로 설정 가능

        private void SetupWindowProperties() // 윈도우 속성 설정 메서드 - 창 활성화 모드와 투명도 적용
        {
            AppSettings settings = TryLoadAppSettings(); // 설정 파일에서 앱 설정을 불러옴
            ApplyWindowActivationStyle(settings.ActivationMode); // 현재 설정에 따라 창 활성화 스타일 적용
            if (MainBorder != null) { byte alpha = (byte)Math.Round(settings.Opacity * 255); MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)); } 
            // MainBorder가 null이 아닐 때 투명도 적용 (MainBorder는 XAML에서 정의된 UI 요소로, 창의 배경을 설정하는 데 사용됨)
        }

        private void ApplyWindowActivationStyle(WindowActivationMode mode) 
        {
            if (_hWnd == IntPtr.Zero) return;
            this.Topmost = (mode == WindowActivationMode.Topmost);
            int extendedStyle = GetWindowLong(_hWnd, GWL_EXSTYLE); 
            if (mode == WindowActivationMode.NoActivate) { SetWindowLong(_hWnd, GWL_EXSTYLE, extendedStyle | WS_EX_NOACTIVATE); } 
            else { SetWindowLong(_hWnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_NOACTIVATE); }
            Debug.WriteLine($"창 활성 모드 적용: {mode}");
        }

        private AppSettings TryLoadAppSettings() {
            if (File.Exists(settingsFilePath)) {
                try { XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                    using (FileStream fs = new FileStream(settingsFilePath, FileMode.Open)) 
                    { if (serializer.Deserialize(fs) is AppSettings loadedSettings) 
                        {
                            return loadedSettings; 
                        } 
                    } 
                } 
                catch (Exception ex) 
                {
                    Debug.WriteLine($"설정 파일 로드 오류: {ex.Message}"); 
                }
            }
            return new AppSettings(); 
        }

        private void LoadSettings()
        { // 설정 로드 메서드 - 앱 시작 시 설정 파일을 불러옴
            AppSettings settings = TryLoadAppSettings();
            UpdateTimetableTitle(settings.Grade, settings.ClassNum);
            if (!File.Exists(settingsFilePath)) { ShowInitialSetupDialog(settings); }
        }

        private void SaveSettings(AppSettings settings) // 설정 저장 메서드 - XML 파일로 설정을 저장함
        {
            try { XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
                using (FileStream fs = new FileStream(settingsFilePath, FileMode.Create)) 
                {
                    serializer.Serialize(fs, settings); 
                }
                UpdateTimetableTitle(settings.Grade, settings.ClassNum);
                Debug.WriteLine("설정 저장 완료");
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"설정 저장 오류: {ex.Message}"); 
            }
        }


        private void OnSettingsSaved(AppSettings newSettings)
        { // 설정 저장 후 UI 업데이트 및 창 속성 적용 메서드
            SaveSettings(newSettings);
            ApplyWindowActivationStyle(newSettings.ActivationMode);
            SetAutoRun(newSettings.AutoRunEnabled);
            if (MainBorder != null) 
            { 
                byte alpha = (byte)Math.Round(newSettings.Opacity * 255);
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)); 
            }
            // 설정 저장 후 수행평가 정보도 다시 로드
            _ = LoadPerformanceAssessmentDataAsync(); // 수행평가 데이터 비동기로 로드
            _ = LoadTimetableDataAsync(); // 시간표 데이터 비동기로 로드
            _ = LoadSchoolMealsAsync(); // 급식 데이터 비동기로 로드 
        }

        private void UpdateTimetableTitle(string grade, string classNum) // 시간표 제목 업데이트 메서드 
        {
            DateTime today = DateTime.Today; string dayOfWeekKorean = today.ToString("dddd", new CultureInfo("ko-KR"));
            string dateString = $"{today.Month}월 {today.Day}일 {dayOfWeekKorean}";
            if (TimetableTitleText != null) 
                TimetableTitleText.Text = $"📅 시간표 - {grade}학년 {classNum}반 | {dateString}"; 
        }

        private void ShowInitialSetupDialog(AppSettings currentSettings) // 초기 설정 대화상자 표시 메서드 
        {
            var gradeTextBox = new TextBox { Margin = new Thickness(5), Text = currentSettings.Grade };
            var classTextBox = new TextBox { Margin = new Thickness(5), Text = currentSettings.ClassNum };
            var saveButton = new Button { Content = "저장", Margin = new Thickness(5) };
            StackPanel setupPanel = new StackPanel { Margin = new Thickness(20), Background = Brushes.LightGray };
            setupPanel.Children.Add(new TextBlock { Text = "초기 설정: 학년과 반을 입력하세요.", Margin = new Thickness(5), Foreground = Brushes.Black });
            setupPanel.Children.Add(gradeTextBox);
            setupPanel.Children.Add(classTextBox);
            setupPanel.Children.Add(saveButton);
            var setupWindow = new Window { Title = "초기 설정", Content = setupPanel, Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.ToolWindow, Topmost = true };

            saveButton.Click += async (s, e) =>
            {
                string grade = gradeTextBox.Text;
                string classNum = classTextBox.Text;
                if (!string.IsNullOrWhiteSpace(grade) && !string.IsNullOrWhiteSpace(classNum) && grade != "학년" && classNum != "반")
                {
                    currentSettings.Grade = grade;
                    currentSettings.ClassNum = classNum;
                    SaveSettings(currentSettings);
                    setupWindow.Close();
                    await LoadSchoolMealsAsync();
                    await LoadTimetableDataAsync(); // 설정 저장 후 시간표 리로드
                }
                else { MessageBox.Show("학년과 반을 정확히 입력해주세요."); }
            };
            setupWindow.ShowDialog();
        }
        #endregion

        #region Statistics & Autorun 
        // 통계 및 시작시 자동실행 메서드
        public class Statistics { public int LaunchCount { get; set; } = 0; public DateTime FirstLaunchDateTime { get; set; } = DateTime.Now; }
        private void UpdateAndSaveStatistics() { Statistics stats; if (File.Exists(statisticsFilePath)) { try { XmlSerializer serializer = new XmlSerializer(typeof(Statistics)); using (FileStream fs = new FileStream(statisticsFilePath, FileMode.Open)) { stats = (Statistics)serializer.Deserialize(fs); } stats.LaunchCount++; } catch (Exception ex) { Debug.WriteLine($"통계 파일 로드 오류: {ex.Message}."); stats = new Statistics { LaunchCount = 1 }; } } else { Debug.WriteLine("통계 파일 없음. 최초 실행."); stats = new Statistics { LaunchCount = 1, FirstLaunchDateTime = DateTime.Now }; } try { XmlSerializer serializer = new XmlSerializer(typeof(Statistics)); Directory.CreateDirectory(Path.GetDirectoryName(statisticsFilePath)!); using (FileStream fs = new FileStream(statisticsFilePath, FileMode.Create)) { serializer.Serialize(fs, stats); } Debug.WriteLine($"통계 저장 완료: 실행 횟수 = {stats.LaunchCount}"); } catch (Exception ex) { Debug.WriteLine($"통계 파일 저장 오류: {ex.Message}"); } }
        // 자동 실행 등록/해제 메서드 (오류 수정)
        private void SetAutoRun(bool isEnabled)
        {
            const string AppName = "DGCBTweaks";
            // 중요: .dll이 아닌 .exe 파일의 경로를 가져오도록 수정
            string AppPath = Environment.ProcessPath;

            // AppPath가 null이거나 비어있으면 실행하지 않음 (안전장치)
            if (string.IsNullOrEmpty(AppPath))
            {
                Debug.WriteLine("자동 실행 경로를 찾을 수 없습니다.");
                return;
            }

            try
            {
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); // 레지스트리를 통한 시작프로그램 설정
                if (isEnabled)
                {
                    // 레지스트리에 등록 시 큰따옴표로 경로를 감싸서 공백이 포함된 경로도 안전하게 처리
                    rk.SetValue(AppName, $"\"{AppPath}\""); //APPPATH는 .exe 파일의 경로
                    Debug.WriteLine($"자동 실행 등록됨: \"{AppPath}\"");
                }
                else
                {
                    if (rk.GetValue(AppName) != null) 
                    {
                        rk.DeleteValue(AppName, false);
                        Debug.WriteLine("자동 실행 해제됨.");
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"자동 실행 설정 오류: {ex.Message}"); }
        }
        #endregion


        #region Data Loading & UI Update

private async Task LoadDailyWordAsync()
        {
            Debug.WriteLine("LoadDailyWordAsync 시작");
            List<TextBlock> wordTextBlocks = new List<TextBlock> { Word1, Word2, Word3, Word4, Word5, Word6 }; // 영단어를 표시할 TextBlock 리스트 생성 - 여기에 단어 넣을예정
            // UI 초기화
            await Dispatcher.InvokeAsync(() =>
            { // UI 스레드에서 실행되도록 Dispatcher를 사용하여 UI 요소를 초기화 - 람다식 사용
                foreach (var tb in wordTextBlocks) { if (tb != null) tb.Text = "..."; }
            });

            if (string.IsNullOrWhiteSpace(NotionApiKey) || string.IsNullOrWhiteSpace(WordDatabaseId))
            {
                await Dispatcher.InvokeAsync(() => { foreach (var tb in wordTextBlocks) { if (tb != null) tb.Text = "[API 정보 미설정]"; } });
                return;
            }

            List<string> allWords = new List<string>();
            using (HttpClient client = new HttpClient()) // HttpClient를 사용하여 Notion API에 요청을 보냄
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotionApiKey); // Notion API 키를 Authorization 헤더에 추가
                    client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
                    HttpResponseMessage response = await client.PostAsync($"https://api.notion.com/v1/databases/{WordDatabaseId}/query", new StringContent("{}", Encoding.UTF8, "application/json"));
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode) // 응답이 성공적이면
                    {
                        var apiResponse = JsonConvert.DeserializeObject<NotionApiResponse<NotionWordPage>>(jsonResponse); // JSON 응답을 NotionWordPage 타입으로 역직렬화
                        if (apiResponse?.Results != null && apiResponse.Results.Any()) // 결과가 null이 아니고, 결과가 하나 이상 있는 경우
                        {
                            allWords = apiResponse.Results // 영단어 페이지들을 순회하며 단어를 추출
                                .Select(r => r.Properties?.Word?.Title?.FirstOrDefault()?.Text?.Content) // 각 페이지에서 영단어를 추출하는 람다식
                                .Where(w => !string.IsNullOrWhiteSpace(w)) // 단어가 비어있지 않은 항목만 필터링
                                .ToList(); // 최종적으로 리스트로 변환

                        }
                    }
                    Debug.WriteLine($"Notion 영단어 응답 (상태: {response.StatusCode}): {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 2000))}...");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"영단어 Notion API 예외: {ex.Message}");
                    await Dispatcher.InvokeAsync(() => { foreach (var tb in wordTextBlocks) { if (tb != null) tb.Text = "[로드 실패]"; } });
                    return;
                }
            }

            // 단어 섞기 및 6개 선택
            if (allWords.Any()) // 단어 리스트가 비어있지 않은 경우
            {
                // Fisher-Yates 알고리즘으로 리스트 섞기
                for (int i = allWords.Count - 1; i > 0; i--) // 
                {
                    int j = random.Next(i + 1); // 0부터 i까지의 랜덤 인덱스 생성
                    (allWords[i], allWords[j]) = (allWords[j], allWords[i]); // i번째와 j번째 단어를 교환하여 리스트를 섞음
                }
                var selectedWords = allWords.Take(6).ToList(); // 섞인 리스트에서 처음 6개 단어를 선택

                // UI 업데이트
                await Dispatcher.InvokeAsync(() => { // 아니씨발이게뭔데
                    for (int i = 0; i < wordTextBlocks.Count; i++) // wordTextBlocks의 각 TextBlock에 대해
                    {
                        if (wordTextBlocks[i] != null) // null 체크
                        {
                            wordTextBlocks[i].Text = i < selectedWords.Count ? selectedWords[i] : ""; // 선택된 단어가 있으면 텍스트를 설정, 없으면 빈 문자열로 설정
                            Debug.WriteLine("에러인가? 단어가 채워진 것인가?");
                        } 
                    } 
                });
            }
            else // 단어 리스트가 비어있는 경우
            {
                await Dispatcher.InvokeAsync(() => { foreach (var tb in wordTextBlocks) { if (tb != null) tb.Text = "[단어 없음]"; } }); // 단어가 없는 경우 UI에 메시지 표시
                Debug.WriteLine("오류발쌩!!!!");
            }
            Debug.WriteLine("LoadDailyWordAsync 완료");
        }
      
        private async Task LoadPerformanceAssessmentDataAsync() // 수행평가 로드
        {
            Debug.WriteLine("LoadPerformanceAssessmentDataAsync 시작");
            await Dispatcher.InvokeAsync(() => ClearPerformanceGrid("수행평가 로딩 중..."));

            AppSettings settings = TryLoadAppSettings();
            string userApiKey = settings.UserNotionApiKey;
            string userDbId = settings.UserNotionDbId;

            if (string.IsNullOrWhiteSpace(userApiKey) || string.IsNullOrWhiteSpace(userDbId))
            {
                await Dispatcher.InvokeAsync(() => ClearPerformanceGrid("Notion API 정보 미설정. '이용 방법 및 문의'를 참조해주세요."));
                return;
            }

            List<(string date, string name)> assessments = new List<(string, string)>(); // 수행평가 정보를 저장할 리스트
            using (HttpClient client = new HttpClient()) // HttpClient를 사용하여 Notion API에 요청을 보냄
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userApiKey); // Notion API 키를 Authorization 헤더에 추가
                    client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28"); // Notion API 버전을 헤더에 추가

                    HttpResponseMessage response = await client.PostAsync($"https://api.notion.com/v1/databases/{userDbId}/query", new StringContent("{}", Encoding.UTF8, "application/json")); // 데이터베이스 쿼리 요청 (빈 JSON 객체를 보냄)
                    string jsonResponse = await response.Content.ReadAsStringAsync(); // 응답 본문을 문자열로 읽음

                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<NotionApiResponse<NotionAssessmentPage>>(jsonResponse); // Notion API 응답을 컴터가 읽도록 변경
                        if (apiResponse?.Results != null && apiResponse.Results.Any()) // 응답 결과가 null이 아니고, 결과가 하나 이상 있는 경우
                        {
                            assessments = apiResponse.Results // 수행평가 페이지들을 순회하며 날짜와 이름을 추출
                                .Select(p =>
                                { // 각 페이지에서 날짜와 이름을 추출하는 람다식
                                    string date = p.Properties?.DueDate?.Date?.Start; // DueDate 속성에서 날짜를 가져옴
                                    string name = p.Properties?.AssessmentName?.Title?.FirstOrDefault()?.Text?.Content; // AssessmentName 속성에서 이름을 가져옴
                                    return (date, name); // 날짜와 이름을 튜플로 반환
                                })
                                .Where(item => !string.IsNullOrWhiteSpace(item.date) && !string.IsNullOrWhiteSpace(item.name)) // 날짜와 이름이 모두 비어있지 않은 항목만 필터링
                                .OrderBy(item => DateTime.TryParse(item.date, out var d) ? d : DateTime.MaxValue) // 날짜를 기준으로 오름차순 정렬 (날짜가 유효하지 않은 경우 최대값으로 처리)
                                .ToList(); // 최종적으로 리스트로 변환
                        }
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() => ClearPerformanceGrid("Notion API 정보가 올바르지 않습니다. 데이터베이스 아이디와 API 키, API 키의 접근 권한, 데이터베이스의 '수행평가명' 탭 이름이 제대로 기재되어있는지 확인해주세요."));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"수행평가 Notion API 예외: {ex.Message}");
                    await Dispatcher.InvokeAsync(() => ClearPerformanceGrid("수행평가 로드 실패"));
                    return;
                }
            }

            // UI 업데이트
            await Dispatcher.InvokeAsync(() => PopulatePerformanceGrid(assessments)); // 수행평가 정보를 그리드에 표시
        }

        private void PopulatePerformanceGrid(List<(string date, string name)> assessments) // 수행평가 정보를 그리드에 표시하는 메서드
        {
            ClearPerformanceGrid(""); // 기존 내용 초기화
            if (!assessments.Any()) // 수행평가가 없는 경우
            {
                SetPerformanceCell(1, 0, "예정된 수행평가가 없습니다.");
                Grid.SetColumnSpan(PerformanceAssessmentGrid.Children.OfType<Border>().Last(b => Grid.GetRow(b) == 1 && Grid.GetColumn(b) == 0), 2);
                // 그리드의 첫 번째 셀에 메시지 표시
                return;
            }

            for (int i = 0; i < Math.Min(assessments.Count, 7); i++) // 최대 7개까지 표시
            {
                // 날짜 포맷 변경 시도
                string displayDate = assessments[i].date; // 기본적으로 날짜를 그대로 사용
                if (DateTime.TryParse(assessments[i].date, out DateTime parsedDate)) //  날짜 파싱 성공 시
                {
                    displayDate = parsedDate.ToString("MM/dd"); // 날짜를 MM/dd 형식으로 변환
                }
                SetPerformanceCell(i + 1, 0, displayDate);// 그리드의 첫 번째 열에 날짜 표시
                SetPerformanceCell(i + 1, 1, assessments[i].name);// 그리드의 두 번째 열에 수행평가 이름 표시
            }
        }

        private void ClearPerformanceGrid(string message) // 수행평가 표 초기화
        {
            for (int r = 1; r <= 7; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    // 첫 번째 셀에만 메시지 표시, 나머지는 공백
                    string text = (r == 1 && c == 0 && !string.IsNullOrWhiteSpace(message)) ? message : "";
                    SetPerformanceCell(r, c, text);
                    // 메시지가 있을 경우 ColumnSpan 설정
                    var border = PerformanceAssessmentGrid.Children.OfType<Border>().FirstOrDefault(b => Grid.GetRow(b) == r && Grid.GetColumn(b) == c);
                    if (border != null)
                    {
                        Grid.SetColumnSpan(border, (r == 1 && c == 0 && !string.IsNullOrWhiteSpace(message)) ? 2 : 1);
                    }
                }
            }
        }

        // 디데이 로드 메서드 (요구사항 반영하여 수정)
        // 디데이 로드 메서드 (요구사항 반영하여 수정 및 디버깅 강화)
        private async Task LoadDdayAsync() // 디데이 로드
        {
            Debug.WriteLine("LoadDdayAsync 시작");
            string eventNameText = "[응 버그남 ^^]";
            string ddayCountText = "D-??";

            if (string.IsNullOrWhiteSpace(NotionApiKey) || string.IsNullOrWhiteSpace(DdayDatabaseId)) { eventNameText = "[Notion 키/DB ID 미설정]"; } // 노션키 없으면 나옴
            else
            {
                using (HttpClient client = new HttpClient()) // HttpClient를 사용하여 Notion API에 요청을 보냄
                {
                    try
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotionApiKey); // Notion API 키를 Authorization 헤더에 추가
                        client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28"); // Notion API 버전을 헤더에 추가

                        var requestBody = new
                        {
                            filter = new
                            {
                                property = "날짜",
                                date = new
                                {
                                    on_or_after = DateTime.Today.ToString("yyyy-MM-dd") // 오늘 날짜 이후의 이벤트만 필터링 <<< 작동안함
                                }
                            },
                            sorts = new[] { new { property = "날짜", direction = "ascending" } }, // 날짜 기준으로 오름차순 정렬
                            page_size = 1
                        };
                        string jsonRequestBody = JsonConvert.SerializeObject(requestBody); // 요청 본문을 JSON 문자열로 직렬화
                        HttpResponseMessage response = await client.PostAsync($"https://api.notion.com/v1/databases/{DdayDatabaseId}/query", new StringContent(jsonRequestBody, Encoding.UTF8, "application/json"));
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Notion D-Day API 응답 (상태: {response.StatusCode}): {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 2000))}...");

                        if (response.IsSuccessStatusCode) // 성공했으면
                        {
                            var apiResponse = JsonConvert.DeserializeObject<NotionApiResponse<NotionDdayPage>>(jsonResponse); // 바꿔
                            var firstEvent = apiResponse?.Results?.FirstOrDefault(); // 첫 번째 이벤트만 가져옴(디데이는 하나만 표시하니깐)

                            string eventName = firstEvent?.Properties?.EventName?.Title?.FirstOrDefault()?.Text?.Content; // 이벤트 이름 가져오기
                            string eventDateStr = firstEvent?.Properties?.EventDate?.Date?.Start; // 이벤트 날짜 가져오기 (ISO 8601 형식)

                            if (!string.IsNullOrWhiteSpace(eventName) && !string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out DateTime eventDate)) // 둘다 유효하면
                            {
                                TimeSpan timeDiff = eventDate.Date - DateTime.Today; // 디데이 계산
                                int daysRemaining = timeDiff.Days; // 남은 일수 계산 (음수면 지난거, 0이면 오늘, 양수면 미래)

                                eventNameText = $"현재 예정된 일정 - {eventName}"; // 이벤트 이름 설정

                                if (daysRemaining > 0) { ddayCountText = $"D-{daysRemaining}"; } // 미래 일정이면 D-XX 형식으로 표시
                                else if (daysRemaining == 0) { ddayCountText = "D-DAY"; } // 오늘 일정이면 D-DAY로 표시
                                else { ddayCountText = $"D+{-daysRemaining}"; } // 지난 일정이면 D+XX 형식으로 표시
                            }
                        }
                        else { eventNameText = "[D-Day API 오류]"; } // API 호출이 실패한 경우 오류 메시지 설정
                    }
                    catch (Exception ex) { Debug.WriteLine($"Notion D-Day API 호출 예외: {ex.Message}"); eventNameText = "[D-Day 로드 실패]"; }
                }
            }
            await Dispatcher.InvokeAsync(() =>
            { // UI 업데이트
                if (DdayEventNameText != null) DdayEventNameText.Text = eventNameText; // 이벤트 이름 텍스트 업데이트
                if (DdayDaysText != null) DdayDaysText.Text = ddayCountText; // 디데이 카운트 텍스트 업데이트
            });
            Debug.WriteLine($"LoadDdayAsync 완료: {eventNameText} / {ddayCountText}");
        }

        // MOTD 로드 메서드 (요구사항 반영하여 수정 및 디버깅 강화) 아씹 귀찮아 여기부턴 라이브때할래
        private async Task LoadMotdAsync()
        {
            Debug.WriteLine("LoadMotdAsync 시작");
            string motdMessage = "오늘도 좋은 하루 보내세요!";
            if (string.IsNullOrWhiteSpace(NotionApiKey) || string.IsNullOrWhiteSpace(MotdDatabaseId)) { Debug.WriteLine("Notion MOTD API 키 또는 DB ID가 유효하지 않습니다."); }
            else
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotionApiKey);
                        client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
                        HttpResponseMessage response = await client.PostAsync($"https://api.notion.com/v1/databases/{MotdDatabaseId}/query", new StringContent("{}", Encoding.UTF8, "application/json"));
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Notion MOTD API 응답 (상태: {response.StatusCode}): {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 300))}...");
                        if (response.IsSuccessStatusCode)
                        {
                            var apiResponse = JsonConvert.DeserializeObject<NotionApiResponse<NotionMotdPage>>(jsonResponse);
                            if (apiResponse?.Results != null && apiResponse.Results.Any())
                            {
                                List<string> messages = apiResponse.Results.Select(r => r.Properties?.Message?.Title?.FirstOrDefault()?.Text?.Content).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                                if (messages.Any())
                                {
                                    int index = random.Next(messages.Count);
                                    motdMessage = messages[index];
                                }
                                else { Debug.WriteLine("Notion DB에 유효한 메시지가 없습니다."); }
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Notion MOTD API 호출 예외: {ex.Message}"); motdMessage = "메시지 로드 실패"; }
                }
            }
            await Dispatcher.InvokeAsync(() => { if (MotdContentText != null) MotdContentText.Text = motdMessage; });
        }


        private async Task LoadSchoolMealsAsync()
        {
            Debug.WriteLine("LoadSchoolMealsAsync 메서드 시작됨");
            try
            {
                await Dispatcher.InvokeAsync(() => 
                {
                    if (TodayMealContentText != null) 
                        TodayMealContentText.Text = "오늘 급식 로딩 중...";
                    if (TomorrowMealContentText != null)
                        TomorrowMealContentText.Text = "내일 급식 로딩 중...";
                    Debug.WriteLine("123");
                    MealInfoTitleText.Text = "🍚 급식 정보 (중식)";

                }

                ); // <<<얘뭐임?

                DateTime today = DateTime.Today;
                DateTime tomorrow = today.AddDays(1);
                string todayMealDisplay = await GetMealInfoForDateAsync(today, "오늘");
                string tomorrowMealDisplay = await GetMealInfoForDateAsync(tomorrow, "내일");
                await Dispatcher.InvokeAsync(() => 
                {
                    if (TodayMealContentText != null)
                        TodayMealContentText.Text = todayMealDisplay;
                    if (TomorrowMealContentText != null)
                        TomorrowMealContentText.Text = tomorrowMealDisplay; 
                
                });
                bool todayHasMeal = !todayMealDisplay.Contains("정보 없음");
                if (todayHasMeal)
                {
                    // 설정에서 현재 학년/반 정보를 가져와 급식 시간 계산
                    AppSettings settings = TryLoadAppSettings();
                    if (int.TryParse(settings.Grade, out int grade) && int.TryParse(settings.ClassNum, out int classNum))
                    {
                        string lunchTime = CalculateLunchTime(grade, classNum, today.DayOfWeek);
                        MealInfoTitleText.Text = $"🍚 급식 정보 (중식) | {lunchTime} 취식";
                    }
                }
                Debug.WriteLine($"LoadSchoolMealsAsync 완료");
            }
            catch (Exception ex) { Debug.WriteLine($"LoadSchoolMealsAsync에서 예외 발생: {ex.Message}"); await Dispatcher.InvokeAsync(() => { if (TodayMealContentText != null) TodayMealContentText.Text = "급식 로드 실패"; if (TomorrowMealContentText != null) TomorrowMealContentText.Text = "급식 로드 실패"; }); }
        }

        private async Task<string> GetMealInfoForDateAsync(DateTime date, string dayNameForLog)
        {
            string mealDateStr = date.ToString("yyyyMMdd"); var queryBuilder = new StringBuilder(); queryBuilder.Append($"KEY={Uri.EscapeDataString(NeisApiKey)}&Type=json&pIndex=1&pSize=10&ATPT_OFCDC_SC_CODE={Uri.EscapeDataString(AtptOfcdcScCode_Fixed)}&SD_SCHUL_CODE={Uri.EscapeDataString(SdSchulCode_Fixed)}&MLSV_YMD={Uri.EscapeDataString(mealDateStr)}&MMEAL_SC_CODE={Uri.EscapeDataString(MealServiceCode_Fixed)}");
            string requestUrl = $"{NeisMealApiBaseUrl}?{queryBuilder.ToString()}"; Debug.WriteLine($"[{dayNameForLog}] 급식 API 요청 URL: {requestUrl}");
            string jsonResponseForDebug = "N/A";
            using (HttpClient client = new HttpClient())
            {
                try { client.Timeout = TimeSpan.FromSeconds(15); HttpResponseMessage response = await client.GetAsync(requestUrl); jsonResponseForDebug = await response.Content.ReadAsStringAsync(); if (response.IsSuccessStatusCode) { var apiResponse = JsonConvert.DeserializeObject<MealServiceApiResponse>(jsonResponseForDebug); if (apiResponse?.MealServiceDietInfo != null) { var mealContent = apiResponse.MealServiceDietInfo.FirstOrDefault(c => c.Row != null && c.Row.Any()); if (mealContent?.Row != null) { List<string> dishes = mealContent.Row.Where(r => !string.IsNullOrWhiteSpace(r.DishName)).Select(r => r.DishName.Replace("<br/>", "\n").Trim()).ToList(); return dishes.Any() ? string.Join("\n\n", dishes) : $"[{dayNameForLog}] 급식 정보 없음"; } else { var headInfo = apiResponse.MealServiceDietInfo.FirstOrDefault(c => c.Head != null && c.Head.Any())?.Head.FirstOrDefault(); if (headInfo?.Result?.Code == "INFO-200") return $"[{dayNameForLog}] 급식 정보 없음"; else return $"[{dayNameForLog}] 급식 정보 없음 (내용 비어있음)"; } } return $"[{dayNameForLog}] 급식 정보 없음 (구조 오류)"; } else { return $"[{dayNameForLog}] 급식 정보 없음 (오류: {response.StatusCode})"; } }
                catch (Exception ex) { Debug.WriteLine($"[{dayNameForLog}] 급식 API 예외: {ex.Message}"); return $"[{dayNameForLog}] 급식 정보 없음 (오류 발생)"; }
            }
        }
        #region Lunch Time Calculation Logic
        // 급식 시간을 계산하는 새로운 메서드
        private string CalculateLunchTime(int grade, int classNum, DayOfWeek dayOfWeek)
        {
            // 주말이면 계산하지 않음
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                return "주말";
            }

            // 1. 학년별 배식 시작 시간 설정
            DateTime baseTime;
            switch (grade)
            {
                case 3:
                    baseTime = DateTime.Today.AddHours(12).AddMinutes(40);
                    break;
                case 2:
                    baseTime = DateTime.Today.AddHours(13).AddMinutes(0);
                    break;
                case 1:
                    baseTime = DateTime.Today.AddHours(13).AddMinutes(20);
                    break;
                default:
                    return "알 수 없음"; // 학년 정보가 1,2,3이 아닐 경우
            }

            // 2. 반 번호로 조(group) 찾기
            int group;
            if (classNum == 1 || classNum == 6) group = 1;
            else if (classNum == 2 || classNum == 7) group = 2;
            else if (classNum == 3 || classNum == 8) group = 3;
            else if (classNum == 4 || classNum == 9) group = 4;
            else if (classNum == 5 || classNum == 10) group = 5;
            else return "알 수 없음"; // 반 정보가 1~10이 아닐 경우

            // 3. 요일별 조 순서 계산
            // DayOfWeek: Sunday = 0, Monday = 1, ..., Friday = 5
            int dayOffset = (int)dayOfWeek - 1; // 월요일(0) ~ 금요일(4)
            if (dayOffset < 0 || dayOffset > 4) return "평일 아님"; // 월~금 이외의 날

            // 요일별 순서 리스트 생성
            List<int> dailyOrder = new List<int> { 1, 2, 3, 4, 5 };
            for (int i = 0; i < dayOffset; i++)
            {
                int first = dailyOrder[0];
                dailyOrder.RemoveAt(0);
                dailyOrder.Add(first);
            }
            // 예: 화요일(dayOffset=1) -> [2, 3, 4, 5, 1]

            // 4. 해당 조의 순번 찾기
            int groupOrderIndex = dailyOrder.IndexOf(group); // 0부터 시작하는 순번 (0이면 첫번째, 1이면 두번째)

            if (groupOrderIndex == -1) return "알 수 없음"; // 조를 찾지 못할 경우

            // 5. 최종 배식 시간 계산 (순번 * 2분)
            DateTime finalTime = baseTime.AddMinutes(groupOrderIndex * 2);

            // "HH시 mm분" 형식으로 변환하여 반환
            return finalTime.ToString("HH시 mm분");
        }
        #endregion

        private async Task LoadTimetableDataAsync()
        {
            Debug.WriteLine("LoadTimetableDataAsync 시작");
            await Dispatcher.InvokeAsync(() => SetTimetableCell(1, 1, "시간표 로딩 중..."));
            AppSettings settings = TryLoadAppSettings();
            string grade = settings.Grade;
            string classNm = settings.ClassNum;
            DateTime today = DateTime.Today;
            string currentYear = today.Year.ToString();
            string currentSemester;
            if (today.Month >= 3 && today.Month <= 7)
            {
                currentSemester = "1";
            } // 학기 구하기
            else
            {
                currentSemester = "2";
                if (today.Month < 3)
                {
                    currentYear = (today.Year - 1).ToString();
                }
            }
            int diffToMonday = DayOfWeek.Monday - today.DayOfWeek; if (diffToMonday > 0) diffToMonday -= 7;

            DateTime monday = today.AddDays(diffToMonday);
            string fromDateStr = monday.ToString("yyyyMMdd");
            string toDateStr = monday.AddDays(5).ToString("yyyyMMdd");
            Debug.WriteLine($"시간표 조회 조건: AY={currentYear}, SEM={currentSemester}, GRADE={grade}, CLASS_NM={classNm}, FROM={fromDateStr}, TO={toDateStr}");

            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"KEY={Uri.EscapeDataString(NeisApiKey)}&Type=json&pIndex=1&pSize=100&ATPT_OFCDC_SC_CODE={Uri.EscapeDataString(AtptOfcdcScCode_Fixed)}&SD_SCHUL_CODE={Uri.EscapeDataString(SdSchulCode_Fixed)}&AY={Uri.EscapeDataString(currentYear)}&SEM={Uri.EscapeDataString(currentSemester)}&GRADE={Uri.EscapeDataString(grade)}&CLASS_NM={Uri.EscapeDataString(classNm)}&TI_FROM_YMD={Uri.EscapeDataString(fromDateStr)}&TI_TO_YMD={Uri.EscapeDataString(toDateStr)}");
            string requestUrl = $"{NeisHisTimetableApiBaseUrl}?{queryBuilder.ToString()}"; Debug.WriteLine($"시간표 API 요청 URL: {requestUrl}");
            string jsonResponseForDebug = "N/A"; List<TimetableDataRow> timetableEntries = new List<TimetableDataRow>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(20);
                    HttpResponseMessage response = await client.GetAsync(requestUrl);
                    jsonResponseForDebug = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"시간표 API 응답 (상태: {response.StatusCode}): {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 300))}...");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<HisTimetableApiResponse>(jsonResponseForDebug);
                        if (apiResponse?.HisTimetable != null)
                        {
                            var timetableContent = apiResponse.HisTimetable.FirstOrDefault(c => c.Row != null && c.Row.Any());
                            if (timetableContent?.Row != null) { timetableEntries.AddRange(timetableContent.Row); }
                            else { var headInfo = apiResponse.HisTimetable.FirstOrDefault(c => c.Head != null && c.Head.Any())?.Head.FirstOrDefault(); if (headInfo?.Result?.Code == "INFO-200") Debug.WriteLine("시간표 정보 없음 (INFO-200)."); else Debug.WriteLine($"시간표 Row 비어있음. Head: {JsonConvert.SerializeObject(headInfo)}"); }
                        }
                    }
                    else { Debug.WriteLine($"시간표 API HTTP 오류: {response.StatusCode}"); await Dispatcher.InvokeAsync(() => ClearTimetableGridContent($"API 오류 ({response.StatusCode})")); return; }
                }
                catch (Exception ex) { Debug.WriteLine($"시간표 API 호출 예외: {ex.Message}"); await Dispatcher.InvokeAsync(() => ClearTimetableGridContent("호출 오류")); return; }
            }
            await PopulateTimetableGrid(timetableEntries, monday);
            Debug.WriteLine("LoadTimetableDataAsync 완료");
        }
        private async Task PopulateTimetableGrid(List<TimetableDataRow> entries, DateTime monday)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Debug.WriteLine($"PopulateTimetableGrid 시작. 항목 수: {entries?.Count ?? 0}");
                for (int r = 1; r <= 7; r++) { for (int c = 1; c <= 5; c++) { SetTimetableCell(r, c, ""); } }
                if (entries == null || !entries.Any()) { Debug.WriteLine("표시할 시간표 데이터 없음."); SetTimetableCell(1, 1, "정보 없음"); return; }
                foreach (var entry in entries)
                {
                    try
                    {
                        DateTime entryDate = DateTime.ParseExact(entry.Date, "yyyyMMdd", CultureInfo.InvariantCulture);
                        int dayColumn = (int)entryDate.DayOfWeek; if (dayColumn == 0) dayColumn = 7;
                        if (dayColumn >= 1 && dayColumn <= 5)
                        {
                            if (int.TryParse(entry.Period, out int periodRow) && periodRow >= 1 && periodRow <= 7)
                            {
                                string subject = entry.Subject ?? ""; string teacher = entry.TeacherName ?? "";
                                string displayText = subject;
                                if (!string.IsNullOrWhiteSpace(teacher)) { displayText += $"\n({teacher.Split(',').FirstOrDefault()?.Trim()})"; }
                                SetTimetableCell(periodRow, dayColumn, displayText);
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"시간표 항목 처리 오류: {ex.Message}"); }
                }
                Debug.WriteLine("PopulateTimetableGrid 완료");
            });
        }

        private void ClearTimetableGridContent(string message) { for (int r = 1; r <= 7; r++) { for (int c = 1; c <= 5; c++) { SetTimetableCell(r, c, (r == 1 && c == 1) ? message : ""); } } }
        // private void LoadPerformanceAssessmentData() { /* 자리 표시자 */ } << 폐기인듯? 거의
        private void CreateTimetableGrid() { TimetableDisplayGrid.Children.Clear(); TimetableDisplayGrid.RowDefinitions.Clear(); TimetableDisplayGrid.ColumnDefinitions.Clear(); string[] days = { "", "월", "화", "수", "목", "금" }; int periods = 7; for (int i = 0; i <= periods; i++) { TimetableDisplayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 35 }); } for (int i = 0; i < days.Length; i++) { TimetableDisplayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = (i == 0 ? 45 : 90) }); } for (int j = 0; j < days.Length; j++) { TextBlock header = new TextBlock { Text = days[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); TimetableDisplayGrid.Children.Add(header); } for (int i = 1; i <= periods; i++) { TextBlock periodHeader = new TextBlock { Text = $"{i}교시", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(periodHeader, i); Grid.SetColumn(periodHeader, 0); TimetableDisplayGrid.Children.Add(periodHeader); for (int j = 1; j < days.Length; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 14 }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); TimetableDisplayGrid.Children.Add(cellBorder); } } }
        private void SetTimetableCell(int row, int col, string text) { foreach (UIElement element in TimetableDisplayGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        private void CreatePerformanceAssessmentGrid() { PerformanceAssessmentGrid.Children.Clear(); PerformanceAssessmentGrid.RowDefinitions.Clear(); PerformanceAssessmentGrid.ColumnDefinitions.Clear(); int dataRows = 7; int cols = 2; string[] headers = { "날짜", "수행평가 공지" }; for (int i = 0; i <= dataRows; i++) { PerformanceAssessmentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); } for (int j = 0; j < cols; j++) { PerformanceAssessmentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); } for (int j = 0; j < headers.Length; j++) { TextBlock header = new TextBlock { Text = headers[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); PerformanceAssessmentGrid.Children.Add(header); } for (int i = 1; i <= dataRows; i++) { for (int j = 0; j < cols; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); PerformanceAssessmentGrid.Children.Add(cellBorder); } } }
        private void SetPerformanceCell(int row, int col, string text) { foreach (UIElement element in PerformanceAssessmentGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        #endregion

        #region UI Event Handlers
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) { this.DragMove(); } }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
         // private void SettingsButton_Click(object sender, RoutedEventArgs e) { SettingsWindow settingsWindow = new SettingsWindow(TryLoadAppSettings(), OnSettingsSaved); settingsWindow.Owner = this; settingsWindow.ShowDialog(); }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("설정 버튼 클릭됨");
            AppSettings currentSettings = TryLoadAppSettings();
            // 설정 창을 띄울 때, '저장' 콜백과 '새로고침' 콜백을 모두 전달합니다.
            SettingsWindow settingsWindow = new SettingsWindow(
                currentSettings,
                OnSettingsSaved,
                async () => await LoadInitialDataAsync() // 새로고침 버튼이 눌리면 LoadInitialDataAsync 실행
            );
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        private void Timetable_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 이 메서드는 ScrollViewer가 마우스 클릭을 처리하기 '전에' 실행됩니다.
            // 여기서 마우스 상태를 확인하고 창 이동 명령을 직접 호출하여
            // 이벤트가 ScrollViewer에 의해 중단되는 것을 막습니다.
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        #endregion
    }
}