using System;
using System.Windows;
using System.Windows.Controls;
using static DesktopWidgetApp.MainWindow;
using Microsoft.Win32;
using System.Diagnostics;

namespace DesktopWidgetApp
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _currentSettings;
        private Action<AppSettings> _onSave;

        public SettingsWindow(AppSettings settings, Action<AppSettings> onSaveCallback)
        {
            InitializeComponent();
            _currentSettings = settings;
            _onSave = onSaveCallback;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e) { this.Close(); }
        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { UpdateTransparencyLabel(); }
        private void UpdateTransparencyLabel() { if (TransparencyLabel != null) { TransparencyLabel.Text = $"배경 투명도 설정 (현재: {TransparencySlider.Value:F0}%)"; } }
        private void AutoRunToggleButton_Checked(object sender, RoutedEventArgs e) { UpdateAutoRunButtonContent(); }
        private void AutoRunToggleButton_Unchecked(object sender, RoutedEventArgs e) { UpdateAutoRunButtonContent(); }
        private void UpdateAutoRunButtonContent() { if (AutoRunToggleButton != null) { AutoRunToggleButton.Content = AutoRunToggleButton.IsChecked == true ? "O" : "X"; } }
    }
}