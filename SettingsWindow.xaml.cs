using System;
using System.Windows;
using System.Windows.Controls;
using static DesktopWidgetApp.MainWindow;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DesktopWidgetApp
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _currentSettings;
        private Action<AppSettings> _onSave;
        private Func<Task> _onReload; // 새로고침 콜백을 Func<Task>로 변경하여 비동기 작업 지원

        public SettingsWindow(AppSettings settings, Action<AppSettings> onSaveCallback, Func<Task> onReloadCallback)
        {
            InitializeComponent();
            _currentSettings = settings;
            _onSave = onSaveCallback;
            _onReload = onReloadCallback; // 새로고침 콜백 저장
            LoadSettingsIntoUI();
        }

        private void LoadSettingsIntoUI()
        {
            GradeTextBox.Text = _currentSettings.Grade;
            ClassTextBox.Text = _currentSettings.ClassNum;
            TransparencySlider.Value = _currentSettings.Opacity * 100;
            // 노션뭐시기 추가
            UserNotionApiKeyTextBox.Text = _currentSettings.UserNotionApiKey;
            UserNotionDbIdTextBox.Text = _currentSettings.UserNotionDbId;
            UpdateTransparencyLabel();


            switch (_currentSettings.ActivationMode)
            {
                case WindowActivationMode.Topmost: ActivationTopmostRadio.IsChecked = true; break;
                case WindowActivationMode.NoActivate: ActivationNoActivateRadio.IsChecked = true; break;
                default: ActivationNormalRadio.IsChecked = true; break;
            }
            AutoRunToggleButton.IsChecked = _currentSettings.AutoRunEnabled;
            UpdateAutoRunButtonContent();


        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _currentSettings.Grade = GradeTextBox.Text;
            _currentSettings.ClassNum = ClassTextBox.Text;
            _currentSettings.Opacity = TransparencySlider.Value / 100.0;

            if (ActivationTopmostRadio.IsChecked == true) _currentSettings.ActivationMode = WindowActivationMode.Topmost;
            else if (ActivationNoActivateRadio.IsChecked == true) _currentSettings.ActivationMode = WindowActivationMode.NoActivate;
            else _currentSettings.ActivationMode = WindowActivationMode.Normal;

            _currentSettings.AutoRunEnabled = AutoRunToggleButton.IsChecked ?? false;
            _onSave?.Invoke(_currentSettings);


            // 수행평가 Notion 정보 저장
            _currentSettings.UserNotionApiKey = UserNotionApiKeyTextBox.Text;
            _currentSettings.UserNotionDbId = UserNotionDbIdTextBox.Text;
            //            public string UserNotionApiKey { get; set; } = "";
           // public string UserNotionDbId { get; set; } = "";

        _onSave?.Invoke(_currentSettings);
            this.Close();
        }
        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_onReload == null) return;

            // 버튼 비활성화 및 텍스트 변경으로 중복 클릭 방지
            ReloadButton.IsEnabled = false;
            ReloadButton.Content = "로딩 중...";

            try
            {
                // MainWindow의 데이터 로드 메서드 실행
                await _onReload();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"새로고침 중 오류 발생: {ex.Message}");
                MessageBox.Show("데이터를 새로고침하는 중 오류가 발생했습니다.");
            }
            finally
            {
                // 작업 완료 후 버튼 원래 상태로 복원
                ReloadButton.IsEnabled = true;
                ReloadButton.Content = "새로고침";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { this.Close(); }
        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateTransparencyLabel(); }
        private void UpdateTransparencyLabel() { if (TransparencyLabel != null) { TransparencyLabel.Text = $"배경 투명도 설정 (현재: {TransparencySlider.Value:F0}%)"; } }
        private void AutoRunToggleButton_Checked(object sender, RoutedEventArgs e) { UpdateAutoRunButtonContent(); }
        private void AutoRunToggleButton_Unchecked(object sender, RoutedEventArgs e) { UpdateAutoRunButtonContent(); }
        private void UpdateAutoRunButtonContent() { if (AutoRunToggleButton != null) { AutoRunToggleButton.Content = AutoRunToggleButton.IsChecked == true ? "O" : "X"; } }
    }
}