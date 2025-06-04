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
    // API 응답 구조 클래스들 (이전과 동일)
    public class MealServiceApiResponse { [JsonProperty("mealServiceDietInfo")] public List<MealServiceContent> MealServiceDietInfo { get; set; } }
    public class MealServiceContent { [JsonProperty("head")] public List<ServiceHeadInfo> Head { get; set; } [JsonProperty("row")] public List<MealDataRow> Row { get; set; } }
    public class ServiceHeadInfo { [JsonProperty("list_total_count")] public int? ListTotalCount { get; set; } [JsonProperty("RESULT")] public ServiceResult Result { get; set; } }
    public class ServiceResult { [JsonProperty("CODE")] public string Code { get; set; } [JsonProperty("MESSAGE")] public string Message { get; set; } }
    public class MealDataRow { [JsonProperty("MLSV_YMD")] public string MealDate { get; set; } [JsonProperty("MMEAL_SC_CODE")] public string MealCode { get; set; } [JsonProperty("MMEAL_SC_NM")] public string MealName { get; set; } [JsonProperty("DDISH_NM")] public string DishName { get; set; } [JsonProperty("CAL_INFO")] public string CalorieInfo { get; set; } }

    public partial class MainWindow : Window
    {
        private readonly string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopWidgetApp", "settings.xml");

        private const string NeisMealTimetableApiKey = "4cfaa1386bf64e448aed4060ba841503";
        private const string NeisMealApiBaseUrl = "https://open.neis.go.kr/hub/mealServiceDietInfo";
        private const string AtptOfcdcScCode_Fixed = "J10";
        private const string SdSchulCode_Fixed = "7530601";
        private const string MealServiceCode_Fixed = "2"; // 중식

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
                Debug.WriteLine($"MainWindow_Loaded에서 예외 발생: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"앱 로딩 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Debug.WriteLine("MainWindow: Loaded 이벤트 완료");
        }

        private async Task LoadInitialDataAsync()
        {
            Debug.WriteLine("LoadInitialDataAsync 시작");
            LoadSchoolNotices();
            LoadDailyWord();
            await LoadSchoolMealsAsync();
            LoadTimetableData();
            LoadPerformanceAssessmentData();
            Debug.WriteLine("LoadInitialDataAsync 완료");
        }

        private void SetupWindowProperties()
        {
            Debug.WriteLine("SetupWindowProperties 시작");
            AppSettings settings = TryLoadAppSettings();
            this.Topmost = settings.IsAlwaysOnTop;
            Debug.WriteLine($"Topmost 설정: {this.Topmost}");
            Debug.WriteLine("SetupWindowProperties 완료");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) { this.DragMove(); }
        }

        public class AppSettings
        {
            public string Grade { get; set; } = "1";
            public string ClassNum { get; set; } = "1";
            public bool IsAlwaysOnTop { get; set; } = true;
        }

        private AppSettings TryLoadAppSettings()
        {
            Debug.WriteLine("TryLoadAppSettings 시작");
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                    using (FileStream fs = new FileStream(settingsFilePath, FileMode.Open))
                    {
                        if (serializer.Deserialize(fs) is AppSettings loadedSettings)
                        {
                            Debug.WriteLine($"설정 파일 로드 성공: Grade={loadedSettings.Grade}, ClassNum={loadedSettings.ClassNum}, IsAlwaysOnTop={loadedSettings.IsAlwaysOnTop}");
                            return loadedSettings;
                        }
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"설정 파일 로드 오류 ({settingsFilePath}): {ex.Message}"); }
            }
            Debug.WriteLine("설정 파일 없음 또는 오류. 기본 설정 사용.");
            return new AppSettings();
        }

        private void LoadSettings()
        {
            Debug.WriteLine("LoadSettings 시작");
            AppSettings settings = TryLoadAppSettings();
            UpdateTimetableTitle(settings.Grade, settings.ClassNum);
            if (!File.Exists(settingsFilePath))
            {
                Debug.WriteLine("초기 설정 다이얼로그 표시 예정");
                ShowInitialSetupDialog(settings);
            }
            Debug.WriteLine("LoadSettings 완료");
        }

        private void SaveSettings(AppSettings settings)
        {
            Debug.WriteLine($"SaveSettings 시작: Grade={settings.Grade}, ClassNum={settings.ClassNum}, IsAlwaysOnTop={settings.IsAlwaysOnTop}");
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
                using (FileStream fs = new FileStream(settingsFilePath, FileMode.Create))
                {
                    serializer.Serialize(fs, settings);
                }
                UpdateTimetableTitle(settings.Grade, settings.ClassNum);
                this.Topmost = settings.IsAlwaysOnTop;
                Debug.WriteLine("설정 저장 완료 및 UI 반영");
            }
            catch (Exception ex) { Debug.WriteLine($"설정 저장 오류: {ex.Message}"); MessageBox.Show($"설정 저장 중 오류 발생: {ex.Message}", "오류"); }
        }

        private void UpdateTimetableTitle(string grade, string classNum)
        {
            DateTime today = DateTime.Today;
            string dayOfWeekKorean = today.ToString("dddd", new CultureInfo("ko-KR"));
            string dateString = $"{today.Month}월 {today.Day}일 {dayOfWeekKorean}";
            Debug.WriteLine($"UpdateTimetableTitle 호출됨: Grade={grade}, ClassNum={classNum}, DateString={dateString}");
            if (TimetableTitleText != null)
            {
                TimetableTitleText.Text = $"📅 시간표 - {grade}학년 {classNum}반 | {dateString}";
                Debug.WriteLine($"TimetableTitleText 업데이트: {TimetableTitleText.Text}");
            }
            else { Debug.WriteLine("TimetableTitleText is null."); }
        }

        private void ShowInitialSetupDialog(AppSettings currentSettings)
        {
            Debug.WriteLine("ShowInitialSetupDialog 시작");
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
                }
                else { MessageBox.Show("학년과 반을 정확히 입력해주세요.", "입력 오류"); }
            };
            setupWindow.ShowDialog();
            Debug.WriteLine("ShowInitialSetupDialog 완료");
        }

        private void LoadSchoolNotices() { SchoolNoticeContent.Text = "[학교 공지 API 연동 예정]"; }
        private void LoadDailyWord() { DailyWordContent.Text = "[오늘의 영단어 API 연동 예정]"; }

        // 급식 정보를 로드하고 UI에 표시하는 메서드 (오늘/내일 급식 분리 표시)
        private async Task LoadSchoolMealsAsync()
        {
            // UI 업데이트는 UI 스레드에서 수행
            await Dispatcher.InvokeAsync(() =>
            {
                // XAML에 정의된 TextBlock 이름에 맞춰서 업데이트
                // TodayMealContentText와 TomorrowMealContentText는 XAML에서 새로 정의될 이름입니다.
                if (TodayMealContentText != null) TodayMealContentText.Text = "오늘 급식 로딩 중...";
                if (TomorrowMealContentText != null) TomorrowMealContentText.Text = "내일 급식 로딩 중...";
            });
            Debug.WriteLine("LoadSchoolMealsAsync 시작");

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            string todayMealDisplay = await GetMealInfoForDateAsync(today, "오늘");
            string tomorrowMealDisplay = await GetMealInfoForDateAsync(tomorrow, "내일");

            await Dispatcher.InvokeAsync(() =>
            {
                if (TodayMealContentText != null)
                    TodayMealContentText.Text = string.IsNullOrWhiteSpace(todayMealDisplay) || todayMealDisplay.Contains("정보가 없습니다") ? "오늘 급식 정보가 없습니다." : todayMealDisplay;

                if (TomorrowMealContentText != null)
                    TomorrowMealContentText.Text = string.IsNullOrWhiteSpace(tomorrowMealDisplay) || tomorrowMealDisplay.Contains("정보가 없습니다") ? "내일 급식 정보가 없습니다." : tomorrowMealDisplay;
            });
            Debug.WriteLine($"LoadSchoolMealsAsync 완료: 오늘 급식 유효? = {!todayMealDisplay.Contains("정보가 없습니다")}, 내일 급식 유효? = {!tomorrowMealDisplay.Contains("정보가 없습니다")}");
        }

        private async Task<string> GetMealInfoForDateAsync(DateTime date, string dayNameForLog)
        {
            string mealDateStr = date.ToString("yyyyMMdd");
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"KEY={Uri.EscapeDataString(NeisMealTimetableApiKey)}");
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
                    Debug.WriteLine($"[{dayNameForLog}] 급식 API 응답 (상태: {response.StatusCode}): {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 500))}...");
                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<MealServiceApiResponse>(jsonResponseForDebug);
                        if (apiResponse?.MealServiceDietInfo != null)
                        {
                            var mealContentWithRows = apiResponse.MealServiceDietInfo.FirstOrDefault(content => content.Row != null && content.Row.Any());
                            if (mealContentWithRows?.Row != null && mealContentWithRows.Row.Any())
                            {
                                List<string> dishesOfTheDay = mealContentWithRows.Row
                                    .Where(mealRow => !string.IsNullOrWhiteSpace(mealRow.DishName))
                                    .Select(mealRow => mealRow.DishName.Replace("<br/>", "\n").Trim())
                                    .ToList();
                                return dishesOfTheDay.Any() ? string.Join("\n\n", dishesOfTheDay) : "급식 정보가 없습니다.";
                            }
                            else
                            {
                                var headInfo = apiResponse.MealServiceDietInfo.FirstOrDefault(content => content.Head != null && content.Head.Any())?.Head.FirstOrDefault();
                                if (headInfo?.Result?.Code == "INFO-200") { return "급식 정보가 없습니다."; }
                                if (headInfo?.Result == null && (apiResponse.MealServiceDietInfo.FirstOrDefault()?.Row == null || !apiResponse.MealServiceDietInfo.FirstOrDefault().Row.Any())) { return "급식 정보가 없습니다. (데이터 없음)"; }
                                Debug.WriteLine($"[{dayNameForLog}] 급식 내용 없음: Head={JsonConvert.SerializeObject(headInfo)}, Row is empty or null.");
                                return "급식 정보가 없습니다. (내용 비어있음)";
                            }
                        }
                        Debug.WriteLine($"[{dayNameForLog}] 급식 JSON 구조 오류: MealServiceDietInfo is null or empty. Response: {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 500))}");
                        return "급식 정보가 없습니다. (구조 오류)";
                    }
                    else
                    {
                        try { var errorResponse = JsonConvert.DeserializeObject<MealServiceApiResponse>(jsonResponseForDebug); if (errorResponse?.MealServiceDietInfo?.FirstOrDefault()?.Head?.FirstOrDefault()?.Result?.Message != null) { return $"급식 정보 없음 (API: {errorResponse.MealServiceDietInfo.First().Head.First().Result.Message})"; } } catch { /* 무시 */ }
                        Debug.WriteLine($"[{dayNameForLog}] 급식 API HTTP 오류: {response.StatusCode}. Response: {jsonResponseForDebug.Substring(0, Math.Min(jsonResponseForDebug.Length, 500))}");
                        return $"급식 정보 없음 (API 오류: {response.StatusCode})";
                    }
                }
                catch (HttpRequestException httpEx) { Debug.WriteLine($"[{dayNameForLog}] 급식 API HttpRequestException: {httpEx.Message}. URL: {requestUrl}"); return "급식 정보 없음 (네트워크 오류)"; }
                catch (TaskCanceledException taskEx) { Debug.WriteLine($"[{dayNameForLog}] 급식 API TaskCanceledException (Timeout?): {taskEx.Message}. URL: {requestUrl}"); return "급식 정보 없음 (시간 초과)"; }
                catch (JsonException jsonEx) { Debug.WriteLine($"[{dayNameForLog}] 급식 API JsonException: {jsonEx.Message} --- 응답 원본: {jsonResponseForDebug}"); return "급식 정보 없음 (데이터 형식 오류)"; }
                catch (Exception ex) { Debug.WriteLine($"[{dayNameForLog}] 급식 API Exception: {ex.Message} --- 응답 원본: {jsonResponseForDebug}"); return "급식 정보 없음 (알 수 없는 오류)"; }
            }
        }

        private void LoadTimetableData()
        {
            // TODO: 시간표 API 연동 로직 구현
            // 시간표 표 크기 조정을 위해 폰트 크기나 셀 패딩 등을 조절할 수 있습니다.
            // 예시: 첫 번째 셀에 임시 텍스트 설정 (폰트 크기 조정은 XAML이나 스타일에서 하는 것이 더 일반적)
            if (TimetableDisplayGrid.RowDefinitions.Count > 1 && TimetableDisplayGrid.ColumnDefinitions.Count > 1)
            {
                SetTimetableCell(1, 1, "[1교시]");
            }
        }
        private void LoadPerformanceAssessmentData() { /* 이전과 동일 */ }

        private void CreateTimetableGrid()
        {
            TimetableDisplayGrid.Children.Clear();
            TimetableDisplayGrid.RowDefinitions.Clear();
            TimetableDisplayGrid.ColumnDefinitions.Clear();
            string[] days = { "", "월", "화", "수", "목", "금" };
            int periods = 7;
            // 행 정의 (헤더 + 7교시), 각 행의 높이를 좀 더 확보하거나, 내부 TextBlock의 FontSize를 키울 수 있습니다.
            for (int i = 0; i <= periods; i++) { TimetableDisplayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 40 }); } // MinHeight 증가
            // 열 정의, 각 열의 너비를 좀 더 확보
            for (int i = 0; i < days.Length; i++) { TimetableDisplayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = (i == 0 ? 50 : 100) }); } // MinWidth 증가

            for (int j = 0; j < days.Length; j++)
            {
                TextBlock header = new TextBlock { Text = days[j], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 16 }; // FontSize 증가
                Grid.SetRow(header, 0); Grid.SetColumn(header, j); TimetableDisplayGrid.Children.Add(header);
            }
            for (int i = 1; i <= periods; i++)
            {
                TextBlock periodHeader = new TextBlock { Text = $"{i}교시", FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2), Foreground = Brushes.White, FontSize = 16 }; // FontSize 증가
                Grid.SetRow(periodHeader, i); Grid.SetColumn(periodHeader, 0); TimetableDisplayGrid.Children.Add(periodHeader);
                for (int j = 1; j < days.Length; j++)
                {
                    Border cellBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5) };
                    TextBlock cell = new TextBlock { Text = "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 15 }; // Margin, FontSize 증가
                    cellBorder.Child = cell; Grid.SetRow(cellBorder, i); Grid.SetColumn(cellBorder, j); TimetableDisplayGrid.Children.Add(cellBorder);
                }
            }
        }
        private void SetTimetableCell(int row, int col, string text) { /* 이전과 동일 */ }
        private void CreatePerformanceAssessmentGrid() { /* 이전과 동일 */ }
        private void SetPerformanceCell(int row, int col, string text) { /* 이전과 동일 */ }

        // 설정 버튼 클릭 이벤트 핸들러 (새로 추가)
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("설정 버튼 클릭됨");
            // TODO: 설정 창을 띄우거나 설정 관련 로직 실행
            // 예시: 간단한 메시지 박스
            AppSettings currentSettings = TryLoadAppSettings();
            MessageBox.Show($"설정 기능은 여기에 구현될 예정입니다.\n\n현재 설정:\n학년: {currentSettings.Grade}" +
                            $"\n반: {currentSettings.ClassNum}" +
                            $"\n항상 위: {currentSettings.IsAlwaysOnTop}",
                            "설정 정보");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
    }
}

/*
// --------------------------------------------------------------------
// MainWindow.xaml
// --------------------------------------------------------------------
<Window x:Class="DesktopWidgetApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:local="clr-namespace:DesktopWidgetApp"
        mc:Ignorable="d"
        Title="바탕화면 위젯" Height="1000" Width="600" <!-- 창 크기 변경 (세로 확장) -->
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ShowInTaskbar="False"
        MouseLeftButtonDown="Window_MouseLeftButtonDown">

    <Border Background="#7F000000" CornerRadius="10" BorderBrush="LightGray" BorderThickness="1">
        <Grid Margin="10">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/> <!-- 0: 상단 (공지, 영단어, 급식) -->
                <RowDefinition Height="Auto"/> <!-- 1: 중간 (수행평가, 설정 버튼) -->
                <RowDefinition Height="*"/>    <!-- 2: 하단 (시간표) -->
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="1.2*" /> <!-- 왼쪽 열 -->
                <ColumnDefinition Width="2*" />   <!-- 오른쪽 열 -->
            </Grid.ColumnDefinitions>

            <!-- 닫기 버튼 -->
            <Button x:Name="CloseButton" Content="X" Click="CloseButton_Click"
                    HorizontalAlignment="Right" VerticalAlignment="Top" 
                    Grid.Row="0" Grid.Column="1" Margin="0,5,5,0" 
                    Panel.ZIndex="100" 
                    Width="25" Height="25" FontWeight="Bold" Background="IndianRed" Foreground="White" BorderThickness="0"
                    ToolTip="앱 종료"/>

            <!-- 왼쪽 섹션 -->
            <StackPanel Grid.Row="0" Grid.Column="0" Grid.RowSpan="2" Margin="0,0,10,0">
                <!-- 학교 공지 -->
                <Border Background="#33FFFFFF" CornerRadius="5" Padding="10" Margin="0,35,0,10">
                    <StackPanel>
                        <TextBlock Text="📢 학교 공지" FontSize="16" FontWeight="Bold" Foreground="White" Margin="0,0,0,5"/>
                        <TextBlock x:Name="SchoolNoticeContent" Text="[학교 공지 API 연동 예정]" Foreground="White" TextWrapping="Wrap" MinHeight="60"/>
                    </StackPanel>
                </Border>
                <!-- 수행평가 공지 -->
                <Border Background="#33FFFFFF" CornerRadius="5" Padding="10" Margin="0,0,0,5"> <!-- Padding 10으로 수정, 하단 마진 추가 -->
                     <StackPanel>
                        <TextBlock Text="📝 수행평가 공지" FontSize="16" FontWeight="Bold" Foreground="White" Margin="0,0,0,5"/>
                        <Grid x:Name="PerformanceAssessmentGrid" MinHeight="150"/>
                    </StackPanel>
                </Border>
                <!-- 설정 버튼 추가 -->
                <Button x:Name="SettingsButton" Content="⚙️" Click="SettingsButton_Click"
                        HorizontalAlignment="Left" VerticalAlignment="Bottom" 
                        Margin="10,5,0,0" <!-- 수행평가 공지 아래에 위치하도록 마진 조정 -->
                        Width="25" Height="25" FontWeight="Bold" Background="#555555" Foreground="White" BorderThickness="0"
                        ToolTip="설정"/>
            </StackPanel>

            <!-- 오른쪽 상단 섹션 -->
            <StackPanel Grid.Row="0" Grid.Column="1" Orientation="Vertical" Margin="0,35,0,10"> 
                 <!-- 오늘의 영단어 -->
                <Border Background="#33FFFFFF" CornerRadius="5" Padding="10" Margin="0,0,0,5">
                    <StackPanel>
                        <TextBlock Text="📚 오늘의 영단어" FontSize="16" FontWeight="Bold" Foreground="White" Margin="0,0,0,5"/>
                        <TextBlock x:Name="DailyWordContent" Text="[영단어 로딩 중...]" Foreground="White" MinHeight="40"/>
                    </StackPanel>
                </Border>
                <!-- 오늘의 급식 (가로 2칸으로 변경) -->
                <Border Background="#33FFFFFF" CornerRadius="5" Padding="10" Margin="0,5,0,0">
                    <StackPanel>
                        <TextBlock Text="🍚 급식 정보 (중식)" FontSize="16" FontWeight="Bold" Foreground="White" Margin="0,0,0,5"/>
                        <!-- 가로 2칸으로 급식 정보를 표시하기 위한 Grid -->
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/> <!-- 오늘 급식 칸 -->
                                <ColumnDefinition Width="*"/> <!-- 내일 급식 칸 -->
                            </Grid.ColumnDefinitions>
                            <!-- 오늘 급식 TextBlock -->
                            <TextBlock x:Name="TodayMealContentText" Grid.Column="0" Text="[오늘 급식 로딩 중...]" Foreground="White" TextWrapping="Wrap" Margin="0,0,5,0" VerticalAlignment="Top"/>
                            <!-- 내일 급식 TextBlock -->
                            <TextBlock x:Name="TomorrowMealContentText" Grid.Column="1" Text="[내일 급식 로딩 중...]" Foreground="White" TextWrapping="Wrap" Margin="5,0,0,0" VerticalAlignment="Top"/>
                        </Grid>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- 시간표 섹션 -->
            <Border Grid.Row="1" Grid.Column="1" Grid.RowSpan="2" Background="#33FFFFFF" CornerRadius="5" Padding="10" Margin="0,10,0,0"> <!-- 상단 마진 추가 -->
                <DockPanel>
                    <TextBlock x:Name="TimetableTitleText" DockPanel.Dock="Top" Text="📅 시간표" FontSize="16" FontWeight="Bold" Foreground="White" Margin="0,0,0,5"/>
                    <Grid x:Name="TimetableDisplayGrid"/>
                </DockPanel>
            </Border>
        </Grid>
    </Border>
</Window>
*/
