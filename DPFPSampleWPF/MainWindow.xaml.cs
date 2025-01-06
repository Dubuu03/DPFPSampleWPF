using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Media.Animation;

namespace DPFPSampleWPF
{
    public partial class MainWindow : Window
    {
        private bool sidebarExpanded = true;
        private int sidebarWidthCollapsed = 60;
        private int sidebarWidthExpanded = 220;

        // Fields to track login state:
        private bool _isLoggedIn = false;          // Are we logged in at all?
        private string _loggedInStatus = null;     // "Admin" or "Student"
        private int? _loggedInUserID = null;       // which account?

        public MainWindow()
        {
            InitializeComponent();
            MainContent.Navigate(new RegisterPage());
        }

        // Helper method to reset button background
        private void ResetButtonBackgrounds()
        {
            BtnHome.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnAttendance.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnRegister.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnManage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnRecords.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnLog.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
            BtnSettings.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313B44"));
        }

        private void MenuIcon_Click(object sender, RoutedEventArgs e)
        {
            double from = PanelSidebar.Width;
            double to = sidebarExpanded ? sidebarWidthCollapsed : sidebarWidthExpanded;
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(200));
            PanelSidebar.BeginAnimation(WidthProperty, anim);
            sidebarExpanded = !sidebarExpanded;
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonBackgrounds(); // Reset background for all buttons
            BtnHome.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56aeff"));
            MainContent.Content = null; // Clears the frame
        }

        private void BtnAttendance_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonBackgrounds(); // Reset background for all buttons
            BtnAttendance.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56aeff"));
            MainContent.Navigate(new AttendancePage());
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonBackgrounds(); // Reset background for all buttons
            BtnRegister.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56aeff"));
            MainContent.Navigate(new RegisterPage());
        }

        // Manage Students button click
        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonBackgrounds(); // Reset background for all buttons
            BtnManage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56aeff"));

            var loginWnd = new AdminLoginWindow(requireAdmin: false)  // accept admin or student
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (loginWnd.ShowDialog() == true)
            {
                // Could be Admin or Student
                string userStatus = loginWnd.LoggedInStatus;
                int? userID = loginWnd.LoggedInUserID;

                // Then navigate to ManageStudentsPage
                MainContent.Navigate(new ManageStudentsPage(userID, userStatus));
            }
            else
            {
                MessageBox.Show("Login canceled.");
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ResetButtonBackgrounds(); // Reset background for all buttons
            BtnSettings.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#56aeff"));

            // Open the login window with requireAdmin = true
            var loginWnd = new AdminLoginWindow(requireAdmin: true)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (loginWnd.ShowDialog() == true)
            {
                // The user is admin => show SettingsPage
                MainContent.Navigate(new SettingsPage());
            }
            else
            {
                MessageBox.Show("Only admin can access Settings.");
            }
        }
    }
}
