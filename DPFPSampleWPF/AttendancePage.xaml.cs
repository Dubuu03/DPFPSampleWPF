using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MySql.Data.MySqlClient;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Documents;
using Brushes = System.Windows.Media.Brushes;
using System.Collections.Generic;

namespace DPFPSampleWPF
{
    public partial class AttendancePage : Page, DPFP.Capture.EventHandler
    {
        private string connStr = "server=localhost;database=testdb;uid=root;pwd=1234;";
        private Capture _capturer;
        private Verification _verificator;
        private DispatcherTimer dateTimeTimer;

        private bool _lateInAM = false;
        private bool _lateOutAM = false;
        private bool _lateInPM = false;
        private bool _lateOutPM = false;

        public AttendancePage()
        {
            InitializeComponent();
            InitUI();
            InitCapture();
            this.Unloaded += AttendancePage_Unloaded;

            // Load “not logged in” students on page load
            LoadNotLoggedInStudents();

            GetTodaysEvent();
        }

        private void AttendancePage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopCapture();
            dateTimeTimer?.Stop();
        }

        private void InitUI()
        {
            lblDateTime.Content = "Today is " + DateTime.Now.ToString("D") + " — " + DateTime.Now.ToString("HH:mm:ss");
            dateTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            dateTimeTimer.Tick += (s, e) =>
            {
                lblDateTime.Content = "Today is "
                    + DateTime.Now.ToString("D")
                    + " — "
                    + DateTime.Now.ToString("HH:mm:ss");
            };
            dateTimeTimer.Start();
        }

        private void InitCapture()
        {
            try
            {
                _capturer = new Capture();
                if (_capturer != null)
                {
                    _capturer.EventHandler = this;
                }
                _verificator = new Verification();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not initialize fingerprint capture: " + ex.Message);
            }
        }
        private void GetTodaysEvent()
        {
            // The query to get today's event. Assuming you have a table named `events` with a date column `event_date`
            string query = "SELECT event_name FROM event WHERE CURDATE() BETWEEN event_start_date AND event_end_date;";

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        var result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            // Display the event name in the label
                            TodayEventLabel.Content = "Today's Event: " + result.ToString();
                        }
                        else
                        {
                            // If there's no event today, show a default message
                            TodayEventLabel.Content = "No event today.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors
                MessageBox.Show("Error fetching event: " + ex.Message);
            }
        }
        private void btnStartCapture_Click(object sender, RoutedEventArgs e)
        {
            StartCapture();
        }

        private void StartCapture()
        {
            if (_capturer != null)
            {
                try
                {
                    _capturer.StartCapture();
                    lblStatus.Content = "Fingerprint capture started. Place your finger.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Cannot start capture: " + ex.Message);
                }
            }
        }

        private void StopCapture()
        {
            if (_capturer != null)
            {
                try { _capturer.StopCapture(); }
                catch { /* ignore */ }
            }
        }

        public void OnComplete(object Capture, string ReaderSerialNumber, Sample sample)
        {
            Dispatcher.Invoke(() => ProcessSample(sample));
        }
        public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
        public void OnFingerTouch(object Capture, string ReaderSerialNumber) { }
        public void OnReaderConnect(object Capture, string ReaderSerialNumber) { }
        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { }
        public void OnSampleQuality(object Capture, string ReaderSerialNumber, CaptureFeedback CaptureFeedback) { }

        private void ProcessSample(Sample sample)
        {
            DrawFingerprintImage(sample);
            var features = ExtractFeatures(sample, DataPurpose.Verification);
            if (features != null)
            {
                StopCapture();
                int? matchedID = IdentifyUser(features);
                if (!matchedID.HasValue)
                {
                    lblStatus.Content = "No matching user found!";
                    UpdateAttendanceInfo("(No match)", "...", "...", "...", "...");
                }
                else
                {
                    lblStatus.Content = "Fingerprint recognized. Checking event restrictions...";

                    // NEW LOGIC => If user is blocked by an active event => stop
                    if (!CheckActiveEventForUser(matchedID.Value))
                    {
                        lblStatus.Content =
                            "There is an active event today, but your course/year is not allowed.";
                        return;
                    }

                    // If no event or user matched => normal attendance
                    lblStatus.Content = "Logging attendance...";
                    LogAttendanceWithScheduleCheck(matchedID.Value);
                    string userName = FetchAccountName(matchedID.Value);
                    ShowTodayAttendance(matchedID.Value, userName);
                }

                // refresh “not logged in” list after each punch
                LoadNotLoggedInStudents();
            }
        }


        private void DrawFingerprintImage(DPFP.Sample sample)
        {
            Bitmap bmp = ConvertSampleToBitmap(sample);
            if (bmp != null)
            {
                picFingerprint.Source = BitmapToImageSource(bmp);
            }
        }

        private Bitmap ConvertSampleToBitmap(DPFP.Sample sample)
        {
            var converter = new DPFP.Capture.SampleConversion();
            Bitmap bitmap = null;
            converter.ConvertToPicture(sample, ref bitmap);
            return bitmap;
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
        }

        private FeatureSet ExtractFeatures(Sample sample, DataPurpose purpose)
        {
            FeatureExtraction extractor = new FeatureExtraction();
            CaptureFeedback feedback = CaptureFeedback.None;
            FeatureSet features = new FeatureSet();
            extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);
            return (feedback == CaptureFeedback.Good) ? features : null;
        }

        private int? IdentifyUser(FeatureSet features)
        {
            int? foundID = null;
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT id, 
                           fingerprint1, fingerprint2, fingerprint3, fingerprint4,
                           fingerprint5, fingerprint6, fingerprint7, fingerprint8,
                           fingerprint9, fingerprint10
                    FROM account";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            int accountID = dr.GetInt32("id");
                            for (int i = 1; i <= 10; i++)
                            {
                                string colName = "fingerprint" + i;
                                if (!dr.IsDBNull(dr.GetOrdinal(colName)))
                                {
                                    byte[] dbTemplateBytes = (byte[])dr[colName];
                                    var dbTemplate = new Template();
                                    dbTemplate.DeSerialize(dbTemplateBytes);

                                    Verification.Result result = new Verification.Result();
                                    _verificator.Verify(features, dbTemplate, ref result);
                                    if (result.Verified)
                                    {
                                        foundID = accountID;
                                        break;
                                    }
                                }
                            }
                            if (foundID.HasValue) break;
                        }
                    }
                }
            }
            return foundID;
        }

        private string FetchAccountName(int userId)
        {
            string name = "(unknown)";
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "SELECT name FROM account WHERE id=@id LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        name = result.ToString();
                    }
                }
            }
            return name;
        }

        private void ShowTodayAttendance(int userId, string userName)
        {
            string inAM = "...", outAM = "...", inPM = "...", outPM = "...";
            DateTime today = DateTime.Today;

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT time_in_am, time_out_am, time_in_pm, time_out_pm
                    FROM attendance
                    WHERE account_id=@accID 
                      AND attendance_date=@today
                    LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@accID", userId);
                    cmd.Parameters.AddWithValue("@today", today);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            if (!dr.IsDBNull(0)) inAM = dr.GetDateTime("time_in_am").ToString("HH:mm:ss");
                            if (!dr.IsDBNull(1)) outAM = dr.GetDateTime("time_out_am").ToString("HH:mm:ss");
                            if (!dr.IsDBNull(2)) inPM = dr.GetDateTime("time_in_pm").ToString("HH:mm:ss");
                            if (!dr.IsDBNull(3)) outPM = dr.GetDateTime("time_out_pm").ToString("HH:mm:ss");
                        }
                    }
                }
            }
            UpdateAttendanceInfo(userName, inAM, outAM, inPM, outPM);
        }

        private void UpdateAttendanceInfo(string name, string timeInAM, string timeOutAM, string timeInPM, string timeOutPM)
        {
            var tb = new TextBlock();
            tb.Inlines.Add(new Run("Name: " + name + "\n\n")
            {
                Foreground = Brushes.Black
            });

            // Time In AM
            var runInAM = new Run("Time In AM: " + timeInAM + "\n");
            runInAM.Foreground = _lateInAM ? Brushes.Red : Brushes.Black;
            tb.Inlines.Add(runInAM);

            // Time Out AM
            var runOutAM = new Run("Time Out AM: " + timeOutAM + "\n");
            runOutAM.Foreground = _lateOutAM ? Brushes.Red : Brushes.Black;
            tb.Inlines.Add(runOutAM);

            // Time In PM
            var runInPM = new Run("Time In PM: " + timeInPM + "\n");
            runInPM.Foreground = _lateInPM ? Brushes.Red : Brushes.Black;
            tb.Inlines.Add(runInPM);

            // Time Out PM
            var runOutPM = new Run("Time Out PM: " + timeOutPM);
            runOutPM.Foreground = _lateOutPM ? Brushes.Red : Brushes.Black;
            tb.Inlines.Add(runOutPM);

            lblAttendanceInfo.Content = tb;
        }

        // Time schedule: (time_in_am_start, time_in_am_end, time_out_am_start, time_out_am_end,
        //                time_in_pm_start, time_in_pm_end, time_out_pm_start, time_out_pm_end)
        private (TimeSpan, TimeSpan, TimeSpan, TimeSpan, TimeSpan, TimeSpan, TimeSpan, TimeSpan) GetTimeSchedule(int id = 1)
        {
            var def = (TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
                       TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT time_in_am_start, time_in_am_end,
                           time_out_am_start, time_out_am_end,
                           time_in_pm_start, time_in_pm_end,
                           time_out_pm_start, time_out_pm_end
                    FROM time_schedule
                    WHERE time_id=@id
                    LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            var s1 = dr.GetTimeSpan("time_in_am_start");
                            var s2 = dr.GetTimeSpan("time_in_am_end");
                            var s3 = dr.GetTimeSpan("time_out_am_start");
                            var s4 = dr.GetTimeSpan("time_out_am_end");
                            var s5 = dr.GetTimeSpan("time_in_pm_start");
                            var s6 = dr.GetTimeSpan("time_in_pm_end");
                            var s7 = dr.GetTimeSpan("time_out_pm_start");
                            var s8 = dr.GetTimeSpan("time_out_pm_end");
                            return (s1, s2, s3, s4, s5, s6, s7, s8);
                        }
                    }
                }
            }
            return def;
        }

        private void LogAttendanceWithScheduleCheck(int accountID)
        {
            DateTime today = DateTime.Today;
            DateTime now = DateTime.Now;
            var s = GetTimeSchedule(1);

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string checkSql = @"
                    SELECT attendance_id, time_in_am, time_out_am, time_in_pm, time_out_pm
                    FROM attendance
                    WHERE account_id=@accID 
                      AND attendance_date=@today
                    LIMIT 1";

                int? existingID = null;
                DateTime? t_inAM = null, t_outAM = null, t_inPM = null, t_outPM = null;

                using (var cmdCheck = new MySqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@accID", accountID);
                    cmdCheck.Parameters.AddWithValue("@today", today);
                    using (var dr = cmdCheck.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            existingID = dr.GetInt32("attendance_id");
                            if (!dr.IsDBNull(1)) t_inAM = dr.GetDateTime("time_in_am");
                            if (!dr.IsDBNull(2)) t_outAM = dr.GetDateTime("time_out_am");
                            if (!dr.IsDBNull(3)) t_inPM = dr.GetDateTime("time_in_pm");
                            if (!dr.IsDBNull(4)) t_outPM = dr.GetDateTime("time_out_pm");
                        }
                    }
                }

                // Reset flags each time
                _lateInAM = false;
                _lateOutAM = false;
                _lateInPM = false;
                _lateOutPM = false;

                if (existingID == null)
                {
                    // time_in_am check
                    if (now.TimeOfDay > s.Item2)
                    {
                        var d = MessageBox.Show("You are late for AM. Proceed?",
                                                "Confirm",
                                                MessageBoxButton.YesNo);
                        if (d == MessageBoxResult.No) return;
                        _lateInAM = true;
                    }

                    string insSql = @"
                        INSERT INTO attendance 
                        (account_id, attendance_date, time_in_am)
                        VALUES
                        (@accID, @today, @now)";
                    using (var cmdIns = new MySqlCommand(insSql, conn))
                    {
                        cmdIns.Parameters.AddWithValue("@accID", accountID);
                        cmdIns.Parameters.AddWithValue("@today", today);
                        cmdIns.Parameters.AddWithValue("@now", now);
                        cmdIns.ExecuteNonQuery();
                    }
                }
                else
                {
                    // We decide which column to fill
                    string updCol = null;
                    if (t_inAM == null)
                    {
                        if (now.TimeOfDay > s.Item2)
                        {
                            var d = MessageBox.Show("You are late for AM. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateInAM = true;
                        }
                        updCol = "time_in_am";
                    }
                    else if (t_outAM == null)
                    {
                        if (now.TimeOfDay < s.Item3)
                        {
                            var d = MessageBox.Show("You are under time for AM. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateOutAM = true;
                        }
                        else if (now.TimeOfDay > s.Item4)
                        {
                            var d = MessageBox.Show("You are beyond normal AM out time. Late punch-out or overtime. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateOutAM = true;
                        }
                        updCol = "time_out_am";
                    }
                    else if (t_inPM == null)
                    {
                        if (now.TimeOfDay > s.Item6)
                        {
                            var d = MessageBox.Show("You are late for PM. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateInPM = true;
                        }
                        updCol = "time_in_pm";
                    }
                    else if (t_outPM == null)
                    {
                        if (now.TimeOfDay < s.Item7)
                        {
                            var d = MessageBox.Show("You are under time for PM. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateOutPM = true;
                        }
                        else if (now.TimeOfDay > s.Item8)
                        {
                            var d = MessageBox.Show("You are beyond normal PM out time. Overtime. Proceed?",
                                                    "Confirm",
                                                    MessageBoxButton.YesNo);
                            if (d == MessageBoxResult.No) return;
                            _lateOutPM = true;
                        }
                        updCol = "time_out_pm";
                    }
                    else
                    {
                        return; // all columns are filled
                    }

                    string updSql = $"UPDATE attendance SET {updCol}=@now WHERE attendance_id=@id";
                    using (var cmdUpd = new MySqlCommand(updSql, conn))
                    {
                        cmdUpd.Parameters.AddWithValue("@now", now);
                        cmdUpd.Parameters.AddWithValue("@id", existingID.Value);
                        cmdUpd.ExecuteNonQuery();
                    }
                }

                // 4) If user was late in any punch, record in late_summary
                if (_lateInAM || _lateOutAM || _lateInPM || _lateOutPM)
                {
                    RecordLateInMonth(accountID);
                }
            }
        }

        // Increments total_lates for the user in late_summary table for the current month
        private void RecordLateInMonth(int accountID)
        {
            DateTime now = DateTime.Now;
            string monthYear = now.ToString("yyyy-MM");  // e.g. "2023-09"

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string updSql = @"
                    UPDATE late_summary
                    SET total_lates = total_lates + 1
                    WHERE account_id=@accID
                      AND month_year=@monthYear
                    LIMIT 1
                ";

                using (MySqlCommand cmd = new MySqlCommand(updSql, conn))
                {
                    cmd.Parameters.AddWithValue("@accID", accountID);
                    cmd.Parameters.AddWithValue("@monthYear", monthYear);
                    int rowsAffected = cmd.ExecuteNonQuery();

                    // If no row updated, we insert a new row for that month
                    if (rowsAffected == 0)
                    {
                        string insSql = @"
                            INSERT INTO late_summary (account_id, month_year, total_lates)
                            VALUES (@accID, @monthYear, 1)
                        ";
                        using (MySqlCommand cmdIns = new MySqlCommand(insSql, conn))
                        {
                            cmdIns.Parameters.AddWithValue("@accID", accountID);
                            cmdIns.Parameters.AddWithValue("@monthYear", monthYear);
                            cmdIns.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        // Display students with status='Student' who haven't logged in today
        private void LoadNotLoggedInStudents()
        {
            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    string sql = @"
                        SELECT a.name
                        FROM account a
                        WHERE a.status='Student'
                          AND NOT EXISTS (
                            SELECT 1 
                            FROM attendance att
                            WHERE att.account_id = a.id
                              AND att.attendance_date = @today
                          )
                        ORDER BY a.name ASC
                    ";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@today", DateTime.Today);
                        using (var dr = cmd.ExecuteReader())
                        {
                            List<string> studentNames = new List<string>();
                            while (dr.Read())
                            {
                                studentNames.Add(dr.GetString("name"));
                            }
                            lstNotLoggedIn.ItemsSource = studentNames;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading not logged in students: " + ex.Message);
            }
        }

        private void SomeMethodAfterUserLogsIn()
        {
            LoadNotLoggedInStudents();
        }
        private (string studentId, string password)? ShowManualAttendanceDialog()
        {
            // Create a small window
            Window dialog = new Window
            {
                Title = "Manual Attendance",
                Width = 300,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this) // keep parent-child
            };

            // StackPanel with controls
            var stack = new StackPanel { Margin = new Thickness(10) };

            // Student ID
            var lblStudent = new TextBlock { Text = "Student ID:" };
            var txtStudent = new TextBox { Width = 200 };
            // Password
            var lblPass = new TextBlock { Text = "Password:", Margin = new Thickness(0, 10, 0, 0) };
            var txtPass = new PasswordBox { Width = 200 };

            // Buttons
            var panelButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var btnOK = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Width = 60 };

            // Add to stack
            stack.Children.Add(lblStudent);
            stack.Children.Add(txtStudent);
            stack.Children.Add(lblPass);
            stack.Children.Add(txtPass);
            panelButtons.Children.Add(btnOK);
            panelButtons.Children.Add(btnCancel);
            stack.Children.Add(panelButtons);

            dialog.Content = stack;

            // Variables to capture user input if "OK" is clicked
            string userStudentID = null;
            string userPassword = null;

            // Hook up events
            btnOK.Click += (s, e) =>
            {
                userStudentID = txtStudent.Text.Trim();
                userPassword = txtPass.Password.Trim();

                dialog.DialogResult = true;
                dialog.Close();
            };
            btnCancel.Click += (s, e) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                return (userStudentID, userPassword);
            }
            return null; // canceled
        }

        private void btnManualAttendance_Click(object sender, RoutedEventArgs e)
        {
            // 1) Show a dialog to get student_id + password
            var input = ShowManualAttendanceDialog();
            if (input == null)
            {
                // user canceled
                return;
            }
            string studentID = input.Value.studentId;
            string password = input.Value.password;

            // 2) Validate the login => check if (student_id, password) is in the account table
            //    Also confirm status='Student' if needed.
            int? matchedID = ValidateManualLogin(studentID, password);
            if (!matchedID.HasValue)
            {
                MessageBox.Show("Invalid student ID or password!");
                return;
            }

            // 3) Check active event restrictions => you already have CheckActiveEventForUser(matchedID.Value)
            if (!CheckActiveEventForUser(matchedID.Value))
            {
                lblStatus.Content = "There's an active event today, but your course/year is not allowed.";
                return;
            }

            // 4) If passed => LogAttendanceWithScheduleCheck
            //    The rest is effectively the same as your fingerprint logic
            lblStatus.Content = "Manual attendance recognized. Logging attendance...";
            LogAttendanceWithScheduleCheck(matchedID.Value);

            string userName = FetchAccountName(matchedID.Value);
            ShowTodayAttendance(matchedID.Value, userName);

            // Refresh “not logged in” list
            LoadNotLoggedInStudents();
        }
        private int? ValidateManualLogin(string studentID, string pass)
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // e.g. if your columns are “student_id, password, status”
                // plus the “id” as the PK
                string sql = @"
            SELECT id
            FROM account
            WHERE student_id=@studID
              AND password=@pass
              AND status='Student'
            LIMIT 1
        ";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@studID", studentID);
                    cmd.Parameters.AddWithValue("@pass", pass);

                    object obj = cmd.ExecuteScalar();
                    if (obj == null)
                    {
                        return null; // not found
                    }
                    return Convert.ToInt32(obj); // the 'id'
                }
            }
        }



        private bool CheckActiveEventForUser(int accountID)
        {
            // 1) Are there any events active on today’s date?
            //    If no events => we do NOT block. Return true.
            // 2) If there are events => does user’s course+year match participants?

            DateTime today = DateTime.Today;
            // fetch user’s course/year from account
            string userCourse = null;
            string userYear = null;

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();

                // First, fetch user’s course/year
                string sqlUser = "SELECT course, year FROM account WHERE id=@id LIMIT 1";
                using (var cmdU = new MySqlCommand(sqlUser, conn))
                {
                    cmdU.Parameters.AddWithValue("@id", accountID);
                    using (var dr = cmdU.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            userCourse = dr.IsDBNull(dr.GetOrdinal("course"))
                                ? "" : dr.GetString("course");
                            userYear = dr.IsDBNull(dr.GetOrdinal("year"))
                                ? "" : dr.GetString("year");
                        }
                    }
                }

                // Next, check for any event that is “active” for today’s date
                // e.g. event_start_date <= today <= event_end_date
                // This might yield multiple events if your DB can have overlapping events
                List<(int eid, string eCourse, string eYear)> activeEvents = new List<(int, string, string)>();

                string sqlEvt = @"
            SELECT event_id, event_name, 
                   event_start_date, event_end_date,
                   participants_course, participants_year
            FROM event
            WHERE @today >= event_start_date 
              AND @today <= event_end_date
        ";
                using (var cmdE = new MySqlCommand(sqlEvt, conn))
                {
                    cmdE.Parameters.AddWithValue("@today", today);
                    using (var drEvt = cmdE.ExecuteReader())
                    {
                        while (drEvt.Read())
                        {
                            int eid = drEvt.GetInt32("event_id");
                            // read CSV
                            string eCourse = drEvt.IsDBNull(drEvt.GetOrdinal("participants_course"))
                                ? "" : drEvt.GetString("participants_course");
                            string eYear = drEvt.IsDBNull(drEvt.GetOrdinal("participants_year"))
                                ? "" : drEvt.GetString("participants_year");

                            activeEvents.Add((eid, eCourse, eYear));
                        }
                    }
                }

                // If no active events => return true (allow normal attendance)
                if (activeEvents.Count == 0)
                {
                    return true;
                }

                // If there ARE active events, the user must match at least one
                // i.e., userCourse is in participants_course CSV,
                //  and userYear is in participants_year CSV for that event.
                // If user matches, we allow => return true. Otherwise false.
                foreach (var ev in activeEvents)
                {
                    // parse CSV
                    var coursesCSV = (ev.eCourse ?? "").Split(',');
                    var yearsCSV = (ev.eYear ?? "").Split(',');

                    bool courseMatch = false;
                    bool yearMatch = false;

                    // check if userCourse is in ev.eCourse
                    foreach (var c in coursesCSV)
                    {
                        if (c.Trim().Equals(userCourse, StringComparison.OrdinalIgnoreCase))
                        {
                            courseMatch = true;
                            break;
                        }
                    }

                    foreach (var y in yearsCSV)
                    {
                        if (y.Trim().Equals(userYear, StringComparison.OrdinalIgnoreCase))
                        {
                            yearMatch = true;
                            break;
                        }
                    }

                    // If user matches BOTH course + year for this event => success
                    if (courseMatch && yearMatch)
                    {
                        return true;
                    }
                }

                // If we get here, user does NOT match any of the active events => block
                return false;
            }
        }

    }
}
