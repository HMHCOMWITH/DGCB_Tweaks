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

// 중요: 이 네임스페이스를 사용자님의 프로젝트 이름과 일치시켜 주세요. (예: namespace DGCB_Tweaks)
namespace DesktopWidgetApp
{
    #region API Response Classes
    // --- API 응답 구조 클래스들 ---
    public class MealServiceApiResponse { [JsonProperty("mealServiceDietInfo")] public List<ServiceContentBase<ServiceHeadInfo, MealDataRow>> MealServiceDietInfo { get; set; } }
    public class HisTimetableApiResponse { [JsonProperty("hisTimetable")] public List<ServiceContentBase<ServiceHeadInfo, TimetableDataRow>> HisTimetable { get; set; } }

    // ServiceContentBase를 일반 클래스로 변경하여 JSON 역직렬화 오류 해결
    public class ServiceContentBase<THead, TRow> where THead : class where TRow : class
    {
        [JsonProperty("head")] public List<THead> Head { get; set; }
        [JsonProperty("row")] public List<TRow> Row { get; set; }
    }
    public class ServiceHeadInfo { [JsonProperty("list_total_count")] public int? ListTotalCount { get; set; } [JsonProperty("RESULT")] public ServiceResult Result { get; set; } }
    public class ServiceResult { [JsonProperty("CODE")] public string Code { get; set; } [JsonProperty("MESSAGE")] public string Message { get; set; } }

    public class MealDataRow { [JsonProperty("MLSV_YMD")] public string MealDate { get; set; } [JsonProperty("MMEAL_SC_CODE")] public string MealCode { get; set; } [JsonProperty("MMEAL_SC_NM")] public string MealName { get; set; } [JsonProperty("DDISH_NM")] public string DishName { get; set; } [JsonProperty("CAL_INFO")] public string CalorieInfo { get; set; } }
    public class TimetableDataRow { [JsonProperty("ALL_TI_YMD")] public string Date { get; set; } [JsonProperty("PERIO")] public string Period { get; set; } [JsonProperty("ITRT_CNTNT")] public string Subject { get; set; } [JsonProperty("TEACHER_NM")] public string TeacherName { get; set; } }

    public class NotionApiResponse<T> { [JsonProperty("results")] public List<T> Results { get; set; } }
    public class NotionMotdPage { [JsonProperty("properties")] public NotionMotdProperties Properties { get; set; } }
    public class NotionMotdProperties { [JsonProperty("Message")] public NotionTitleProperty Message { get; set; } }

    public class NotionDdayPage { [JsonProperty("properties")] public NotionDdayProperties Properties { get; set; } }
    public class NotionDdayProperties { [JsonProperty("이벤트 명")] public NotionTitleProperty EventName { get; set; } [JsonProperty("날짜")] public NotionDateProperty EventDate { get; set; } }

    public class NotionTitleProperty { [JsonProperty("title")] public List<NotionRichText> Title { get; set; } }
    public class NotionDateProperty { [JsonProperty("date")] public NotionDateObject Date { get; set; } }
    public class NotionDateObject { [JsonProperty("start")] public string Start { get; set; } }
    public class NotionRichText { [JsonProperty("text")] public NotionTextContent Text { get; set; } }
    public class NotionTextContent { [JsonProperty("content")] public string Content { get; set; } }
    #endregion

    public partial class MainWindow : Window
    {
        #region Fields and Constants
        private readonly string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DGCBTweaks");
        private readonly string settingsFilePath;
        private readonly string statisticsFilePath;

        private const string NeisApiKey = "4cfaa1386bf64e448aed4060ba841503";
        private const string NeisMealApiBaseUrl = "https://open.neis.go.kr/hub/mealServiceDietInfo";
        private const string NeisHisTimetableApiBaseUrl = "https://open.neis.go.kr/hub/hisTimetable";
        private const string AtptOfcdcScCode_Fixed = "J10";
        private const string SdSchulCode_Fixed = "7530601";
        private const string MealServiceCode_Fixed = "2";

        private const string NotionApiKey = "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";
        private const string MotdDatabaseId = "20af2d42beb9804e9e52c5f6b72a67a3";
        private const string DdayDatabaseId = "20df2d42beb980c09a58fc147a4eb6ba";

        private static readonly Random random = new Random();
        #endregion

        #region P/Invoke Definitions
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private IntPtr _hWnd;
        #endregion

        #region Constructor and Window Events
        public MainWindow()
        {
            settingsFilePath = Path.Combine(appDataPath, "settings.xml");
            statisticsFilePath = Path.Combine(appDataPath, "Statistics.xml");
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hWnd = new WindowInteropHelper(this).Handle;
            if (HwndSource.FromHwnd(_hWnd) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
            ApplyWindowActivationStyle(TryLoadAppSettings().ActivationMode);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE) { if (TryLoadAppSettings().ActivationMode == WindowActivationMode.NoActivate) { handled = true; return new IntPtr(MA_NOACTIVATE); } }
            return IntPtr.Zero;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow: Loaded 이벤트 시작");
            try
            {
                UpdateAndSaveStatistics();
                LoadSettings();
                SetupWindowProperties();
                CreateTimetableGrid();
                CreatePerformanceAssessmentGrid();
                await LoadInitialDataAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"MainWindow_Loaded에서 예외 발생: {ex.Message}"); }
        }

        private async Task LoadInitialDataAsync()
        {
            Debug.WriteLine("LoadInitialDataAsync 시작");
            var tasks = new List<Task>
            {
                LoadDdayAsync(),
                LoadDailyWordAsync(),
                LoadSchoolMealsAsync(),
                LoadTimetableDataAsync(),
                LoadPerformanceAssessmentDataAsync(),
                LoadMotdAsync()
            };
            await Task.WhenAll(tasks);
            Debug.WriteLine("LoadInitialDataAsync 완료");
        }
        #endregion

        #region Settings and App Configuration
        public class AppSettings { public string Grade { get; set; } = "1"; public string ClassNum { get; set; } = "1"; public double Opacity { get; set; } = 0.5; public WindowActivationMode ActivationMode { get; set; } = WindowActivationMode.Normal; public bool AutoRunEnabled { get; set; } = false; }
        public enum WindowActivationMode { Normal, Topmost, NoActivate }

        private void SetupWindowProperties()
        {
            AppSettings settings = TryLoadAppSettings();
            ApplyWindowActivationStyle(settings.ActivationMode);
            if (MainBorder != null) { byte alpha = (byte)Math.Round(settings.Opacity * 255); MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)); }
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

        private AppSettings TryLoadAppSettings() { if (File.Exists(settingsFilePath)) { try { XmlSerializer serializer = new XmlSerializer(typeof(AppSettings)); using (FileStream fs = new FileStream(settingsFilePath, FileMode.Open)) { if (serializer.Deserialize(fs) is AppSettings loadedSettings) { return loadedSettings; } } } catch (Exception ex) { Debug.WriteLine($"설정 파일 로드 오류: {ex.Message}"); } } return new AppSettings(); }

        private void LoadSettings() { AppSettings settings = TryLoadAppSettings(); UpdateTimetableTitle(settings.Grade, settings.ClassNum); if (!File.Exists(settingsFilePath)) { ShowInitialSetupDialog(settings); } }

        private void SaveSettings(AppSettings settings) { try { XmlSerializer serializer = new XmlSerializer(typeof(AppSettings)); Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!); using (FileStream fs = new FileStream(settingsFilePath, FileMode.Create)) { serializer.Serialize(fs, settings); } UpdateTimetableTitle(settings.Grade, settings.ClassNum); Debug.WriteLine("설정 저장 완료"); } catch (Exception ex) { Debug.WriteLine($"설정 저장 오류: {ex.Message}"); } }

        private void OnSettingsSaved(AppSettings newSettings) { SaveSettings(newSettings); ApplyWindowActivationStyle(newSettings.ActivationMode); SetAutoRun(newSettings.AutoRunEnabled); if (MainBorder != null) { byte alpha = (byte)Math.Round(newSettings.Opacity * 255); MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)); } }

        private void UpdateTimetableTitle(string grade, string classNum) { DateTime today = DateTime.Today; string dayOfWeekKorean = today.ToString("dddd", new CultureInfo("ko-KR")); string dateString = $"{today.Month}월 {today.Day}일 {dayOfWeekKorean}"; if (TimetableTitleText != null) TimetableTitleText.Text = $"📅 시간표 - {grade}학년 {classNum}반 | {dateString}"; }

        private void ShowInitialSetupDialog(AppSettings currentSettings)
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
        public class Statistics { public int LaunchCount { get; set; } = 0; public DateTime FirstLaunchDateTime { get; set; } = DateTime.Now; }
        private void UpdateAndSaveStatistics() { Statistics stats; if (File.Exists(statisticsFilePath)) { try { XmlSerializer serializer = new XmlSerializer(typeof(Statistics)); using (FileStream fs = new FileStream(statisticsFilePath, FileMode.Open)) { stats = (Statistics)serializer.Deserialize(fs); } stats.LaunchCount++; } catch (Exception ex) { Debug.WriteLine($"통계 파일 로드 오류: {ex.Message}."); stats = new Statistics { LaunchCount = 1 }; } } else { Debug.WriteLine("통계 파일 없음. 최초 실행."); stats = new Statistics { LaunchCount = 1, FirstLaunchDateTime = DateTime.Now }; } try { XmlSerializer serializer = new XmlSerializer(typeof(Statistics)); Directory.CreateDirectory(Path.GetDirectoryName(statisticsFilePath)!); using (FileStream fs = new FileStream(statisticsFilePath, FileMode.Create)) { serializer.Serialize(fs, stats); } Debug.WriteLine($"통계 저장 완료: 실행 횟수 = {stats.LaunchCount}"); } catch (Exception ex) { Debug.WriteLine($"통계 파일 저장 오류: {ex.Message}"); } }
        #endregion

        #region Autorun
        private void SetAutoRun(bool isEnabled)
        {
            const string AppName = "DGCBTweaks";
            string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            try { RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); if (isEnabled) { rk.SetValue(AppName, AppPath); Debug.WriteLine($"자동 실행 등록됨: {AppPath}"); } else { if (rk.GetValue(AppName) != null) { rk.DeleteValue(AppName, false); Debug.WriteLine("자동 실행 해제됨."); } } }
            catch (Exception ex) { Debug.WriteLine($"자동 실행 설정 오류: {ex.Message}"); }
        }
        #endregion

        #region Data Loading & UI Update
        private async Task LoadDailyWordAsync() { await Dispatcher.InvokeAsync(() => { if (DailyWordContent != null) DailyWordContent.Text = "[오늘의 영단어 API 연동 예정]"; }); }
        private async Task LoadPerformanceAssessmentDataAsync() { /* 자리 표시자 */ }

        // 디데이 로드 메서드 (요구사항 반영하여 수정)
        // 디데이 로드 메서드 (요구사항 반영하여 수정 및 디버깅 강화)
        private async Task LoadDdayAsync()
        {
            Debug.WriteLine("LoadDdayAsync 시작");
            string eventNameText = "[응 버그남 ^^]";
            string ddayCountText = "D-??";

            if (string.IsNullOrWhiteSpace(NotionApiKey) || string.IsNullOrWhiteSpace(DdayDatabaseId)) { eventNameText = "[Notion 키/DB ID 미설정]"; }
            else
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotionApiKey);
                        client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");

                        var requestBody = new
                        {
                            filter = new
                            {
                                property = "날짜",
                                date = new
                                {
                                    on_or_after = DateTime.Today.ToString("yyyy-MM-dd")
                                }
                            },
                            sorts = new[] { new { property = "날짜", direction = "ascending" } },
                            page_size = 1
                        };
                        string jsonRequestBody = JsonConvert.SerializeObject(requestBody);
                        HttpResponseMessage response = await client.PostAsync($"https://api.notion.com/v1/databases/{DdayDatabaseId}/query", new StringContent(jsonRequestBody, Encoding.UTF8, "application/json"));
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Notion D-Day API 응답 (상태: {response.StatusCode}): {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 2000))}...");

                        if (response.IsSuccessStatusCode)
                        {
                            var apiResponse = JsonConvert.DeserializeObject<NotionApiResponse<NotionDdayPage>>(jsonResponse);
                            var firstEvent = apiResponse?.Results?.FirstOrDefault();

                            string eventName = firstEvent?.Properties?.EventName?.Title?.FirstOrDefault()?.Text?.Content;
                            string eventDateStr = firstEvent?.Properties?.EventDate?.Date?.Start;

                            if (!string.IsNullOrWhiteSpace(eventName) && !string.IsNullOrWhiteSpace(eventDateStr) && DateTime.TryParse(eventDateStr, out DateTime eventDate))
                            {
                                TimeSpan timeDiff = eventDate.Date - DateTime.Today;
                                int daysRemaining = timeDiff.Days;

                                eventNameText = $"현재 예정된 일정 - {eventName}";

                                if (daysRemaining > 0) { ddayCountText = $"D-{daysRemaining}"; }
                                else if (daysRemaining == 0) { ddayCountText = "D-DAY"; }
                                else { ddayCountText = $"D+{-daysRemaining}"; }
                            }
                        }
                        else { eventNameText = "[D-Day API 오류]"; }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Notion D-Day API 호출 예외: {ex.Message}"); eventNameText = "[D-Day 로드 실패]"; }
                }
            }
            await Dispatcher.InvokeAsync(() => {
                if (DdayEventNameText != null) DdayEventNameText.Text = eventNameText;
                if (DdayDaysText != null) DdayDaysText.Text = ddayCountText;
            });
            Debug.WriteLine($"LoadDdayAsync 완료: {eventNameText} / {ddayCountText}");
        }

        // MOTD 로드 메서드 (요구사항 반영하여 수정 및 디버깅 강화)
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
                await Dispatcher.InvokeAsync(() => { if (TodayMealContentText != null) TodayMealContentText.Text = "오늘 급식 로딩 중..."; if (TomorrowMealContentText != null) TomorrowMealContentText.Text = "내일 급식 로딩 중..."; });
                DateTime today = DateTime.Today; DateTime tomorrow = today.AddDays(1);
                string todayMealDisplay = await GetMealInfoForDateAsync(today, "오늘");
                string tomorrowMealDisplay = await GetMealInfoForDateAsync(tomorrow, "내일");
                await Dispatcher.InvokeAsync(() => { if (TodayMealContentText != null) TodayMealContentText.Text = todayMealDisplay; if (TomorrowMealContentText != null) TomorrowMealContentText.Text = tomorrowMealDisplay; });
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
        private void LoadPerformanceAssessmentData() { /* 자리 표시자 */ }
        private void CreateTimetableGrid() { TimetableDisplayGrid.Children.Clear(); TimetableDisplayGrid.RowDefinitions.Clear(); TimetableDisplayGrid.ColumnDefinitions.Clear(); string[] days = { "", "월", "화", "수", "목", "금" }; int periods = 7; for (int i = 0; i <= periods; i++) { TimetableDisplayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 35 }); } for (int i = 0; i < days.Length; i++) { TimetableDisplayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = (i == 0 ? 45 : 90) }); } for (int j = 0; j < days.Length; j++) { TextBlock header = new TextBlock { Text = days[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); TimetableDisplayGrid.Children.Add(header); } for (int i = 1; i <= periods; i++) { TextBlock periodHeader = new TextBlock { Text = $"{i}교시", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(periodHeader, i); Grid.SetColumn(periodHeader, 0); TimetableDisplayGrid.Children.Add(periodHeader); for (int j = 1; j < days.Length; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 14 }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); TimetableDisplayGrid.Children.Add(cellBorder); } } }
        private void SetTimetableCell(int row, int col, string text) { foreach (UIElement element in TimetableDisplayGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        private void CreatePerformanceAssessmentGrid() { PerformanceAssessmentGrid.Children.Clear(); PerformanceAssessmentGrid.RowDefinitions.Clear(); PerformanceAssessmentGrid.ColumnDefinitions.Clear(); int dataRows = 7; int cols = 2; string[] headers = { "날짜", "수행평가 공지" }; for (int i = 0; i <= dataRows; i++) { PerformanceAssessmentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); } for (int j = 0; j < cols; j++) { PerformanceAssessmentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); } for (int j = 0; j < headers.Length; j++) { TextBlock header = new TextBlock { Text = headers[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); PerformanceAssessmentGrid.Children.Add(header); } for (int i = 1; i <= dataRows; i++) { for (int j = 0; j < cols; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); PerformanceAssessmentGrid.Children.Add(cellBorder); } } }
        private void SetPerformanceCell(int row, int col, string text) { foreach (UIElement element in PerformanceAssessmentGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        #endregion

        #region UI Event Handlers
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) { this.DragMove(); } }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
        private void SettingsButton_Click(object sender, RoutedEventArgs e) { SettingsWindow settingsWindow = new SettingsWindow(TryLoadAppSettings(), OnSettingsSaved); settingsWindow.Owner = this; settingsWindow.ShowDialog(); }
        #endregion
    }
}