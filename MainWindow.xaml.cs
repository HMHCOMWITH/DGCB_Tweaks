// MainWindow.xaml.cs
// 필요한 네임스페이스들을 선언합니다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using Newtonsoft.Json;
using System.Diagnostics;

namespace DesktopWidgetApp
{
    // --- API 응답 구조 클래스 정의 ---
    public class MealServiceApiResponse { [JsonProperty("mealServiceDietInfo")] public List<MealServiceContentBase> MealServiceDietInfo { get; set; } }
    public class HisTimetableApiResponse { [JsonProperty("hisTimetable")] public List<TimetableServiceContentBase> HisTimetable { get; set; } }
    public abstract class ServiceContentBase<THead, TRow> where THead : class where TRow : class
    {
        [JsonProperty("head")] public List<THead> Head { get; set; }
        [JsonProperty("row")] public List<TRow> Row { get; set; }
    }
    public class MealServiceContentBase : ServiceContentBase<ServiceHeadInfo, MealDataRow> { }
    public class TimetableServiceContentBase : ServiceContentBase<ServiceHeadInfo, TimetableDataRow> { }
    public class ServiceHeadInfo
    {
        [JsonProperty("list_total_count")] public int? ListTotalCount { get; set; }
        [JsonProperty("RESULT")] public ServiceResult Result { get; set; }
    }
    public class ServiceResult { [JsonProperty("CODE")] public string Code { get; set; } [JsonProperty("MESSAGE")] public string Message { get; set; } } // CODE (대문자) 확인
    public class MealDataRow { [JsonProperty("MLSV_YMD")] public string MealDate { get; set; } [JsonProperty("MMEAL_SC_CODE")] public string MealCode { get; set; } [JsonProperty("MMEAL_SC_NM")] public string MealName { get; set; } [JsonProperty("DDISH_NM")] public string DishName { get; set; } [JsonProperty("CAL_INFO")] public string CalorieInfo { get; set; } }
    public class TimetableDataRow
    {
        [JsonProperty("ALL_TI_YMD")] public string Date { get; set; }
        [JsonProperty("PERIO")] public string Period { get; set; }
        [JsonProperty("ITRT_CNTNT")] public string Subject { get; set; }
        [JsonProperty("TEACHER_NM")] public string TeacherName { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopWidgetApp", "settings.xml");
        private const string NeisApiKey = "4cfaa1386bf64e448aed4060ba841503";
        private const string NeisMealApiBaseUrl = "https://open.neis.go.kr/hub/mealServiceDietInfo";
        private const string NeisHisTimetableApiBaseUrl = "https://open.neis.go.kr/hub/hisTimetable";
        private const string AtptOfcdcScCode_Fixed = "J10";
        private const string SdSchulCode_Fixed = "7530601";
        private const string MealServiceCode_Fixed = "2";

        public MainWindow()
        {
            InitializeComponent();
            Debug.WriteLine("MainWindow: InitializeComponent 완료");
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow: Loaded 이벤트 시작");
            try
            {
                LoadSettings();
                SetupWindowProperties();
                CreateTimetableGrid();
                CreatePerformanceAssessmentGrid();
                await LoadInitialDataAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow_Loaded에서 치명적 예외 발생: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"앱 로딩 중 심각한 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Debug.WriteLine("MainWindow: Loaded 이벤트 완료");
        }

        private async Task LoadInitialDataAsync()
        {
            Debug.WriteLine("LoadInitialDataAsync 시작");
            try
            {
                LoadSchoolNotices();
                LoadDailyWord();
                await LoadSchoolMealsAsync(); // 급식 정보 로드
                await LoadTimetableDataAsync(); // 시간표 정보 로드
                LoadPerformanceAssessmentData();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadInitialDataAsync에서 예외 발생 (하위 작업에서 처리되었을 수 있음): {ex.Message}\n{ex.StackTrace}");
            }
            Debug.WriteLine("LoadInitialDataAsync 완료");
        }

        private void SetupWindowProperties() { AppSettings settings = TryLoadAppSettings(); this.Topmost = settings.IsAlwaysOnTop; Debug.WriteLine("SetupWindowProperties 완료 - Topmost: " + this.Topmost); }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) { this.DragMove(); } }
        public class AppSettings { public string Grade { get; set; } = "1"; public string ClassNum { get; set; } = "1"; public bool IsAlwaysOnTop { get; set; } = true; }

        private AppSettings TryLoadAppSettings()
        {
            Debug.WriteLine("TryLoadAppSettings 시작");
            if (File.Exists(settingsFilePath))
            {
                try { XmlSerializer serializer = new XmlSerializer(typeof(AppSettings)); using (FileStream fs = new FileStream(settingsFilePath, FileMode.Open)) { if (serializer.Deserialize(fs) is AppSettings loadedSettings) { Debug.WriteLine($"설정 파일 로드 성공: Grade={loadedSettings.Grade}, ClassNum={loadedSettings.ClassNum}"); return loadedSettings; } } }
                catch (Exception ex) { Debug.WriteLine($"설정 파일 로드 오류: {ex.Message}"); }
            }
            Debug.WriteLine("설정 파일 없음. 기본 설정 사용.");
            return new AppSettings();
        }

        private void LoadSettings()
        {
            Debug.WriteLine("LoadSettings 시작");
            AppSettings settings = TryLoadAppSettings();
            UpdateTimetableTitle(settings.Grade, settings.ClassNum);
            if (!File.Exists(settingsFilePath)) { Debug.WriteLine("초기 설정 다이얼로그 표시 예정"); ShowInitialSetupDialog(settings); }
            Debug.WriteLine("LoadSettings 완료");
        }

        private void SaveSettings(AppSettings settings)
        {
            Debug.WriteLine($"SaveSettings 시작: Grade={settings.Grade}, ClassNum={settings.ClassNum}");
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings)); Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
                using (FileStream fs = new FileStream(settingsFilePath, FileMode.Create)) { serializer.Serialize(fs, settings); }
                UpdateTimetableTitle(settings.Grade, settings.ClassNum);
                this.Topmost = settings.IsAlwaysOnTop;
                Debug.WriteLine("설정 저장 완료");
            }
            catch (Exception ex) { Debug.WriteLine($"설정 저장 오류: {ex.Message}"); MessageBox.Show($"설정 저장 오류: {ex.Message}"); }
        }

        private void UpdateTimetableTitle(string grade, string classNum)
        {
            DateTime today = DateTime.Today; string dayOfWeekKorean = today.ToString("dddd", new CultureInfo("ko-KR")); string dateString = $"{today.Month}월 {today.Day}일 {dayOfWeekKorean}";
            Debug.WriteLine($"UpdateTimetableTitle 호출됨: Grade={grade}, ClassNum={classNum}, DateString={dateString}");
            if (TimetableTitleText != null) { TimetableTitleText.Text = $"📅 시간표 - {grade}학년 {classNum}반 | {dateString}"; Debug.WriteLine($"TimetableTitleText 업데이트: {TimetableTitleText.Text}"); }
            else { Debug.WriteLine("TimetableTitleText is null."); }
        }

        private void ShowInitialSetupDialog(AppSettings currentSettings)
        {
            Debug.WriteLine("ShowInitialSetupDialog 시작");
            var gradeTextBox = new TextBox { Margin = new Thickness(5), Text = currentSettings.Grade }; var classTextBox = new TextBox { Margin = new Thickness(5), Text = currentSettings.ClassNum }; var saveButton = new Button { Content = "저장", Margin = new Thickness(5) }; StackPanel setupPanel = new StackPanel { Margin = new Thickness(20), Background = Brushes.LightGray }; setupPanel.Children.Add(new TextBlock { Text = "초기 설정: 학년과 반을 입력하세요.", Margin = new Thickness(5), Foreground = Brushes.Black }); setupPanel.Children.Add(gradeTextBox); setupPanel.Children.Add(classTextBox); setupPanel.Children.Add(saveButton); var setupWindow = new Window { Title = "초기 설정", Content = setupPanel, Width = 300, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.ToolWindow, Topmost = true };
            saveButton.Click += async (s, e) =>
            {
                string grade = gradeTextBox.Text; string classNum = classTextBox.Text;
                if (!string.IsNullOrWhiteSpace(grade) && !string.IsNullOrWhiteSpace(classNum) && grade != "학년" && classNum != "반") { currentSettings.Grade = grade; currentSettings.ClassNum = classNum; SaveSettings(currentSettings); setupWindow.Close(); await LoadSchoolMealsAsync(); await LoadTimetableDataAsync(); }
                else { MessageBox.Show("학년과 반을 정확히 입력해주세요."); }
            };
            setupWindow.ShowDialog(); Debug.WriteLine("ShowInitialSetupDialog 완료");
        }

        private void LoadSchoolNotices() { if (SchoolNoticeContent != null) SchoolNoticeContent.Text = "[학교 공지 API 연동 예정]"; else Debug.WriteLine("SchoolNoticeContent is null"); }
        private void LoadDailyWord() { if (DailyWordContent != null) DailyWordContent.Text = "[오늘의 영단어 API 연동 예정]"; else Debug.WriteLine("DailyWordContent is null"); }

        // 급식 정보 로드 메서드 (복원 및 디버깅 강화)
        private async Task LoadSchoolMealsAsync()
        {
            Debug.WriteLine("LoadSchoolMealsAsync 메서드 시작됨"); // 메서드 진입 로그
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    if (TodayMealContentText != null) TodayMealContentText.Text = "오늘 급식 로딩 중..."; else Debug.WriteLine("TodayMealContentText is null in LoadSchoolMealsAsync UI Update");
                    if (TomorrowMealContentText != null) TomorrowMealContentText.Text = "내일 급식 로딩 중..."; else Debug.WriteLine("TomorrowMealContentText is null in LoadSchoolMealsAsync UI Update");
                });

                DateTime today = DateTime.Today;
                DateTime tomorrow = today.AddDays(1);
                string todayMealDisplay = "[오늘 급식 정보 없음]";
                string tomorrowMealDisplay = "[내일 급식 정보 없음]";

                todayMealDisplay = await GetMealInfoForDateAsync(today, "오늘");
                tomorrowMealDisplay = await GetMealInfoForDateAsync(tomorrow, "내일");

                await Dispatcher.InvokeAsync(() =>
                {
                    if (TodayMealContentText != null) TodayMealContentText.Text = todayMealDisplay;
                    if (TomorrowMealContentText != null) TomorrowMealContentText.Text = tomorrowMealDisplay;
                });
                Debug.WriteLine($"LoadSchoolMealsAsync 성공적으로 완료: 오늘급식표시={!todayMealDisplay.Contains("정보 없음")}, 내일급식표시={!tomorrowMealDisplay.Contains("정보 없음")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSchoolMealsAsync에서 예외 발생: {ex.Message}\n{ex.StackTrace}");
                await Dispatcher.InvokeAsync(() =>
                {
                    if (TodayMealContentText != null) TodayMealContentText.Text = "급식 정보 로드 실패";
                    if (TomorrowMealContentText != null) TomorrowMealContentText.Text = "급식 정보 로드 실패";
                });
            }
        }

        private async Task<string> GetMealInfoForDateAsync(DateTime date, string dayNameForLog)
        {
            Debug.WriteLine($"GetMealInfoForDateAsync 시작: [{dayNameForLog}] 날짜: {date:yyyy-MM-dd}");
            string mealDateStr = date.ToString("yyyyMMdd");
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"KEY={Uri.EscapeDataString(NeisApiKey)}");
            queryBuilder.Append($"&Type={Uri.EscapeDataString("json")}");
            queryBuilder.Append($"&pIndex={Uri.EscapeDataString("1")}");
            queryBuilder.Append($"&pSize={Uri.EscapeDataString("10")}");
            queryBuilder.Append($"&ATPT_OFCDC_SC_CODE={Uri.EscapeDataString(AtptOfcdcScCode_Fixed)}");
            queryBuilder.Append($"&SD_SCHUL_CODE={Uri.EscapeDataString(SdSchulCode_Fixed)}");
            queryBuilder.Append($"&MLSV_YMD={Uri.EscapeDataString(mealDateStr)}");
            queryBuilder.Append($"&MMEAL_SC_CODE={Uri.EscapeDataString(MealServiceCode_Fixed)}");
            string requestUrl = $"{NeisMealApiBaseUrl}?{queryBuilder.ToString()}";
            Debug.WriteLine($"[{dayNameForLog}] 급식 API 요청 URL: {requestUrl}");
            string jsonResponseForDebug = "N/A";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    HttpResponseMessage response = await client.GetAsync(requestUrl);
                    jsonResponseForDebug = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[{dayNameForLog}] 급식 API 응답 (상태: {response.StatusCode}): {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 300))}...");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<MealServiceApiResponse>(jsonResponseForDebug);
                        if (apiResponse?.MealServiceDietInfo != null)
                        {
                            var mealContentWithRows = apiResponse.MealServiceDietInfo.FirstOrDefault(content => content.Row != null && content.Row.Any());
                            if (mealContentWithRows?.Row != null && mealContentWithRows.Row.Any())
                            {
                                List<string> dishesOfTheDay = mealContentWithRows.Row.Where(mealRow => !string.IsNullOrWhiteSpace(mealRow.DishName)).Select(mealRow => mealRow.DishName.Replace("<br/>", "\n").Trim()).ToList();
                                return dishesOfTheDay.Any() ? string.Join("\n\n", dishesOfTheDay) : $"[{dayNameForLog}] 급식 정보가 없습니다.";
                            }
                            else
                            {
                                var headInfo = apiResponse.MealServiceDietInfo.FirstOrDefault(content => content.Head != null && content.Head.Any())?.Head.FirstOrDefault();
                                // 여기서 headInfo.Result.Code (C# 속성명) 사용 확인
                                if (headInfo?.Result?.Code == "INFO-200") { Debug.WriteLine($"[{dayNameForLog}] 급식 정보 없음 (INFO-200)"); return $"[{dayNameForLog}] 급식 정보가 없습니다."; }
                                Debug.WriteLine($"[{dayNameForLog}] 급식 내용 없음. Head: {JsonConvert.SerializeObject(headInfo)}");
                                return $"[{dayNameForLog}] 급식 정보가 없습니다. (내용 비어있음)";
                            }
                        }
                        Debug.WriteLine($"[{dayNameForLog}] 급식 JSON 구조 오류. Response: {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 300))}");
                        return $"[{dayNameForLog}] 급식 정보가 없습니다. (구조 오류)";
                    }
                    else
                    {
                        // API 오류 메시지 파싱 시도
                        try { var errorResponse = JsonConvert.DeserializeObject<MealServiceApiResponse>(jsonResponseForDebug); if (errorResponse?.MealServiceDietInfo?.FirstOrDefault()?.Head?.FirstOrDefault()?.Result?.Message != null) { return $"[{dayNameForLog}] 급식 정보 없음 (API: {errorResponse.MealServiceDietInfo.First().Head.First().Result.Message})"; } } catch { /* 무시 */ }
                        Debug.WriteLine($"[{dayNameForLog}] 급식 API HTTP 오류: {response.StatusCode}");
                        return $"[{dayNameForLog}] 급식 정보 없음 (API 오류: {response.StatusCode})";
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[{dayNameForLog}] 급식 API Exception: {ex.GetType().Name} - {ex.Message} --- 응답: {jsonResponseForDebug}"); return $"[{dayNameForLog}] 급식 정보 없음 (오류: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 30))})"; }
            }
        }

        private async Task LoadTimetableDataAsync()
        {
            Debug.WriteLine("LoadTimetableDataAsync 시작");
            await Dispatcher.InvokeAsync(() => SetTimetableCell(1, 1, "시간표 로딩 중..."));
            AppSettings settings = TryLoadAppSettings();
            string grade = settings.Grade; string classNm = settings.ClassNum;
            DateTime today = DateTime.Today; string currentYear = today.Year.ToString(); string currentSemester;
            if (today.Month >= 3 && today.Month <= 7) { currentSemester = "1"; } else { currentSemester = "2"; if (today.Month < 3) { currentYear = (today.Year - 1).ToString(); } }
            int diffToMonday = DayOfWeek.Monday - today.DayOfWeek; if (diffToMonday > 0) diffToMonday -= 7;
            DateTime currentWeekMonday = today.AddDays(diffToMonday); DateTime currentWeekSaturday = currentWeekMonday.AddDays(5);
            string fromDateStr = currentWeekMonday.ToString("yyyyMMdd"); string toDateStr = currentWeekSaturday.ToString("yyyyMMdd");
            Debug.WriteLine($"시간표 조회 조건: AY={currentYear}, SEM={currentSemester}, GRADE={grade}, CLASS_NM={classNm}, FROM={fromDateStr}, TO={toDateStr}");
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"KEY={Uri.EscapeDataString(NeisApiKey)}"); queryBuilder.Append($"&Type={Uri.EscapeDataString("json")}"); queryBuilder.Append($"&pIndex={Uri.EscapeDataString("1")}"); queryBuilder.Append($"&pSize={Uri.EscapeDataString("100")}"); queryBuilder.Append($"&ATPT_OFCDC_SC_CODE={Uri.EscapeDataString(AtptOfcdcScCode_Fixed)}"); queryBuilder.Append($"&SD_SCHUL_CODE={Uri.EscapeDataString(SdSchulCode_Fixed)}"); queryBuilder.Append($"&AY={Uri.EscapeDataString(currentYear)}"); queryBuilder.Append($"&SEM={Uri.EscapeDataString(currentSemester)}"); queryBuilder.Append($"&GRADE={Uri.EscapeDataString(grade)}"); queryBuilder.Append($"&CLASS_NM={Uri.EscapeDataString(classNm)}"); queryBuilder.Append($"&TI_FROM_YMD={Uri.EscapeDataString(fromDateStr)}"); queryBuilder.Append($"&TI_TO_YMD={Uri.EscapeDataString(toDateStr)}");
            string requestUrl = $"{NeisHisTimetableApiBaseUrl}?{queryBuilder.ToString()}"; Debug.WriteLine($"시간표 API 요청 URL: {requestUrl}");
            string jsonResponseForDebug = "N/A"; List<TimetableDataRow> timetableEntries = new List<TimetableDataRow>();
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(20); HttpResponseMessage response = await client.GetAsync(requestUrl); jsonResponseForDebug = await response.Content.ReadAsStringAsync(); Debug.WriteLine($"시간표 API 응답 (상태: {response.StatusCode}): {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 300))}...");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<HisTimetableApiResponse>(jsonResponseForDebug);
                        if (apiResponse?.HisTimetable != null)
                        {
                            var timetableContent = apiResponse.HisTimetable.FirstOrDefault(content => content.Row != null && content.Row.Any());
                            if (timetableContent?.Row != null && timetableContent.Row.Any()) { timetableEntries.AddRange(timetableContent.Row); }
                            else { var headInfo = apiResponse.HisTimetable.FirstOrDefault(content => content.Head != null && content.Head.Any())?.Head.FirstOrDefault(); if (headInfo?.Result?.Code == "INFO-200") { Debug.WriteLine("시간표 정보 없음 (INFO-200)."); } else { Debug.WriteLine($"시간표 Row 비어있음. Head: {JsonConvert.SerializeObject(headInfo)}"); } } // .Code 사용 확인
                        }
                        else
                        {
                            Debug.WriteLine($"시간표 JSON 구조 오류. HisTimetable is null. Response: {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 300))}");
                            try { var genericResponse = JsonConvert.DeserializeObject<Dictionary<string, List<TimetableServiceContentBase>>>(jsonResponseForDebug); if (genericResponse != null && genericResponse.Values.Any()) { var firstContentList = genericResponse.Values.First(); var headInfo = firstContentList?.FirstOrDefault(c => c.Head != null && c.Head.Any())?.Head.FirstOrDefault(); if (headInfo?.Result != null) { Debug.WriteLine($"시간표 API 결과: CODE={headInfo.Result.Code}, MESSAGE={headInfo.Result.Message}"); if (headInfo.Result.Code == "INFO-200" || headInfo.Result.Code == "INFO-300") { } else { await Dispatcher.InvokeAsync(() => ClearTimetableGridContent($"API 오류: {headInfo.Result.Message.Substring(0, Math.Min(headInfo.Result.Message.Length, 30))}")); return; } } } } catch (Exception ex) { Debug.WriteLine($"시간표 오류 응답 파싱 실패: {ex.Message}"); }
                        }
                    }
                    else { Debug.WriteLine($"시간표 API HTTP 오류: {response.StatusCode}"); await Dispatcher.InvokeAsync(() => ClearTimetableGridContent($"API 오류 ({response.StatusCode})")); return; }
                }
                catch (Exception ex) { Debug.WriteLine($"시간표 API 호출 예외: {ex.Message}. 응답: {jsonResponseForDebug}"); await Dispatcher.InvokeAsync(() => ClearTimetableGridContent("호출 오류")); return; }
            }
            await PopulateTimetableGrid(timetableEntries, currentWeekMonday);
            Debug.WriteLine("LoadTimetableDataAsync 완료");
        }

        private async Task PopulateTimetableGrid(List<TimetableDataRow> entries, DateTime monday)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Debug.WriteLine($"PopulateTimetableGrid 시작. 항목 수: {entries?.Count ?? 0}");
                for (int r = 1; r <= 7; r++) { for (int c = 1; c <= 5; c++) { SetTimetableCell(r, c, ""); } } // 그리드 초기화
                if (entries == null || !entries.Any()) { Debug.WriteLine("표시할 시간표 데이터 없음."); SetTimetableCell(1, 1, "시간표 정보 없음"); return; }

                // 주의: 나이스 API의 DayOfWeek는 실제 DayOfWeek와 다를 수 있음. API 명세 확인 필요.
                // 여기서는 DateTime.DayOfWeek (Sunday = 0, Monday = 1, ..., Saturday = 6)를 사용한다고 가정.
                foreach (var entry in entries)
                {
                    try
                    {
                        DateTime entryDate = DateTime.ParseExact(entry.Date, "yyyyMMdd", CultureInfo.InvariantCulture);
                        int dayColumn = -1;
                        // DayOfWeek를 Grid의 열 인덱스(월=1 ~ 금=5)로 매핑
                        switch (entryDate.DayOfWeek)
                        {
                            case DayOfWeek.Monday: dayColumn = 1; break;
                            case DayOfWeek.Tuesday: dayColumn = 2; break;
                            case DayOfWeek.Wednesday: dayColumn = 3; break;
                            case DayOfWeek.Thursday: dayColumn = 4; break;
                            case DayOfWeek.Friday: dayColumn = 5; break;
                            default: continue; // 월~금 이외의 요일은 건너뜀
                        }

                        if (int.TryParse(entry.Period, out int periodRow) && periodRow >= 1 && periodRow <= 7)
                        {
                            string subject = entry.Subject ?? "";
                            string teacher = entry.TeacherName ?? "";
                            string displayText = subject;
                            if (!string.IsNullOrWhiteSpace(teacher)) { displayText += $"\n({teacher.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()})"; }
                            SetTimetableCell(periodRow, dayColumn, displayText);
                            Debug.WriteLine($"시간표 셀 업데이트: [{periodRow}교시, {entryDate.DayOfWeek}(Col:{dayColumn})] = {displayText}");
                        }
                        else { Debug.WriteLine($"잘못된 교시 값: {entry.Period} (날짜: {entry.Date})"); }
                    }
                    catch (Exception ex) { Debug.WriteLine($"시간표 항목 UI 업데이트 중 오류: {ex.Message} - 항목: {JsonConvert.SerializeObject(entry)}"); }
                }
                Debug.WriteLine("PopulateTimetableGrid 완료");
            });
        }

        private void ClearTimetableGridContent(string message) { for (int r = 1; r <= 7; r++) { for (int c = 1; c <= 5; c++) { SetTimetableCell(r, c, (r == 1 && c == 1) ? message : ""); } } }
        private void LoadPerformanceAssessmentData() { /* 이전과 동일 */ }
        private void CreateTimetableGrid() { /* 이전과 동일 - 폰트 및 크기 조정된 버전 */ TimetableDisplayGrid.Children.Clear(); TimetableDisplayGrid.RowDefinitions.Clear(); TimetableDisplayGrid.ColumnDefinitions.Clear(); string[] days = { "", "월", "화", "수", "목", "금" }; int periods = 7; for (int i = 0; i <= periods; i++) { TimetableDisplayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 35 }); } for (int i = 0; i < days.Length; i++) { TimetableDisplayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = (i == 0 ? 45 : 90) }); } for (int j = 0; j < days.Length; j++) { TextBlock header = new TextBlock { Text = days[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); TimetableDisplayGrid.Children.Add(header); } for (int i = 1; i <= periods; i++) { TextBlock periodHeader = new TextBlock { Text = $"{i}교시", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 15 }; Grid.SetRow(periodHeader, i); Grid.SetColumn(periodHeader, 0); TimetableDisplayGrid.Children.Add(periodHeader); for (int j = 1; j < days.Length; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 14 }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); TimetableDisplayGrid.Children.Add(cellBorder); } } }
        private void SetTimetableCell(int row, int col, string text) { foreach (UIElement element in TimetableDisplayGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        private void CreatePerformanceAssessmentGrid() { /* 이전과 동일 */ PerformanceAssessmentGrid.Children.Clear(); PerformanceAssessmentGrid.RowDefinitions.Clear(); PerformanceAssessmentGrid.ColumnDefinitions.Clear(); int dataRows = 7; int cols = 2; string[] headers = { "날짜", "수행평가 공지" }; for (int i = 0; i <= dataRows; i++) { PerformanceAssessmentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); } for (int j = 0; j < cols; j++) { PerformanceAssessmentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); } for (int j = 0; j < headers.Length; j++) { TextBlock header = new TextBlock { Text = headers[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White }; Grid.SetRow(header, 0); Grid.SetColumn(header, j); PerformanceAssessmentGrid.Children.Add(header); } for (int i = 1; i <= dataRows; i++) { for (int j = 0; j < cols; j++) { Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) }; TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap }; cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); PerformanceAssessmentGrid.Children.Add(cellBorder); } } }
        private void SetPerformanceCell(int row, int col, string text) { foreach (UIElement element in PerformanceAssessmentGrid.Children) { if (Grid.GetRow(element) == row && Grid.GetColumn(element) == col && element is Border border) { if (border.Child is TextBlock textBlock) { textBlock.Text = text; break; } } } }
        private void SettingsButton_Click(object sender, RoutedEventArgs e) { AppSettings currentSettings = TryLoadAppSettings(); MessageBox.Show($"설정 기능은 여기에 구현될 예정입니다.\n\n현재 설정:\n학년: {currentSettings.Grade}\n반: {currentSettings.ClassNum}\n항상 위: {currentSettings.IsAlwaysOnTop}", "설정 정보"); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
    }
}
