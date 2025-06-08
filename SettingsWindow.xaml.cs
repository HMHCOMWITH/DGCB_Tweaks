using System;
using System.Windows;
using System.Windows.Controls;
using static DesktopWidgetApp.MainWindow; // MainWindow의 AppSettings를 사용하기 위함

namespace DesktopWidgetApp
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _currentSettings;
        // 설정이 저장될 때 MainWindow에 알리기 위한 콜백
        private Action<AppSettings> _onSave;

        public SettingsWindow(AppSettings settings, Action<AppSettings> onSaveCallback)
        {
            InitializeComponent();

            _currentSettings = settings;
            _onSave = onSaveCallback;

            LoadSettingsIntoUI();
        }

        // 현재 설정을 UI 컨트롤에 로드하는 메서드
        private void LoadSettingsIntoUI()
        {
            // 학년/반
            GradeTextBox.Text = _currentSettings.Grade;
            ClassTextBox.Text = _currentSettings.ClassNum;

            // 배경 투명도
            // 투명도(0.0 ~ 1.0)를 슬라이더 값(0 ~ 100)으로 변환
            TransparencySlider.Value = _currentSettings.Opacity * 100;
            UpdateTransparencyLabel();

            // 창 활성 설정
            switch (_currentSettings.ActivationMode)
            {
                case WindowActivationMode.Topmost:
                    ActivationTopmostRadio.IsChecked = true;
                    break;
                case WindowActivationMode.NoActivate:
                    ActivationNoActivateRadio.IsChecked = true;
                    break;
                default: // Normal
                    ActivationNormalRadio.IsChecked = true;
                    break;
            }

            // 자동 실행
            AutoRunToggleButton.IsChecked = _currentSettings.AutoRunEnabled;
            UpdateAutoRunButtonContent();
        }

        // 저장 버튼 클릭 이벤트
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // UI 컨트롤의 값을 _currentSettings 객체에 저장
            _currentSettings.Grade = GradeTextBox.Text;
            _currentSettings.ClassNum = ClassTextBox.Text;
            _currentSettings.Opacity = TransparencySlider.Value / 100.0;

            if (ActivationTopmostRadio.IsChecked == true)
                _currentSettings.ActivationMode = WindowActivationMode.Topmost;
            else if (ActivationNoActivateRadio.IsChecked == true)
                _currentSettings.ActivationMode = WindowActivationMode.NoActivate;
            else
                _currentSettings.ActivationMode = WindowActivationMode.Normal;

            _currentSettings.AutoRunEnabled = AutoRunToggleButton.IsChecked ?? false;

            // 콜백을 통해 MainWindow에 변경된 설정을 전달
            _onSave?.Invoke(_currentSettings);
            this.Close();
        }

        // 돌아가기 버튼 클릭 이벤트
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 아무것도 저장하지 않고 창을 닫음
        }

        // 투명도 슬라이더 값 변경 시 라벨 업데이트
        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTransparencyLabel();
        }

        private void UpdateTransparencyLabel()
        {
            if (TransparencyLabel != null)
            {
                // 라벨 텍스트를 "배경 투명도 설정 (현재: XX%)" 형식으로 변경
                TransparencyLabel.Text = $"배경 투명도 설정 (현재: {TransparencySlider.Value:F0}%)";
            }
        }

        // 자동 실행 토글 버튼 상태 변경 시 내용(O/X) 업데이트
        private void AutoRunToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAutoRunButtonContent();
        }

        private void AutoRunToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateAutoRunButtonContent();
        }

        private void UpdateAutoRunButtonContent()
        {
            if (AutoRunToggleButton != null)
            {
                AutoRunToggleButton.Content = AutoRunToggleButton.IsChecked == true ? "O" : "X";
            }
        }
    }
}
