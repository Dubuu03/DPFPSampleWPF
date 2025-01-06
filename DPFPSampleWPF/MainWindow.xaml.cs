using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
            MainContent.Content = null; // Clears the frame
        }

        private void BtnAttendance_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to AttendancePage
            MainContent.Navigate(new AttendancePage());
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            BtnRegister.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#56aeff"));

            // Navigate to RegisterPage
            MainContent.Navigate(new RegisterPage());
        }

        // Manage Students button click
        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
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


        private void MainContent_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            // Optional: handle navigation events if needed
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
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
