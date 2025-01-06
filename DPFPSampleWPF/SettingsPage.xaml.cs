using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using DPFPSampleWPF.Models;
using MySql.Data.MySqlClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Menu;
using Microsoft.VisualBasic;
using Microsoft.Xaml.Behaviors;

namespace DPFPSampleWPF
{
    public partial class SettingsPage : Page
    {
        private string connStr = "server=localhost;database=testdb;uid=root;pwd=1234;";
      
        // ObservableCollections for data binding
        private ObservableCollection<EventModel> _eventsList = new ObservableCollection<EventModel>();
        private ObservableCollection<CourseModel> _coursesList = new ObservableCollection<CourseModel>();

        public SettingsPage()
        {
            InitializeComponent();
        
            _eventsList = new ObservableCollection<EventModel>();
            dgEvents.ItemsSource = _eventsList;
            LoadEvents();
            LoadCourses();  // fetch existing courses into dgCourses
            LoadTimeSchedule(); // fetch time_in/out values into text fields
            LoadCoursesAndYears();

        }

        // 1) EVENTS TAB LOGIC

        private void LoadEvents()
        {
            var eventsList = new List<EventModel>();
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // The crucial part: just read from participants_course, participants_year as raw CSV
                string sql = @"
            SELECT event_id,
                   event_name,
                   event_start_date,
                   event_end_date,
                   participants_course,  -- store CSV
                   participants_year     -- store CSV
            FROM event
            ORDER BY event_id;
        ";

                using (var cmd = new MySqlCommand(sql, conn))
                {
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            var em = new EventModel
                            {
                                event_id = dr.GetInt32("event_id"),
                                event_name = dr.GetString("event_name"),
                                event_start_date = dr.GetDateTime("event_start_date"),
                                event_end_date = dr.GetDateTime("event_end_date"),
                                // we read the two CSV columns:
                                participants_course = dr.IsDBNull(dr.GetOrdinal("participants_course"))
                                    ? ""
                                    : dr.GetString("participants_course"),
                                participants_year = dr.IsDBNull(dr.GetOrdinal("participants_year"))
                                    ? ""
                                    : dr.GetString("participants_year"),
                            };
                            eventsList.Add(em);
                        }
                    }
                }
            }
            dgEvents.ItemsSource = eventsList;
        }



        private void btnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            // 1) Basic checks for event name / start / end
            string eventName = txtEventName.Text.Trim();
            DateTime? start = dtpEventStart.SelectedDate;
            DateTime? end = dtpEventEnd.SelectedDate;

            if (string.IsNullOrEmpty(eventName))
            {
                MessageBox.Show("Please enter an Event Name!");
                return;
            }
            if (!start.HasValue || !end.HasValue)
            {
                MessageBox.Show("Please select a Start Date and End Date!");
                return;
            }
            // Ensure user picked at least one course and at least one year
            if (lstCourses.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one Course!");
                return;
            }
            if (lstYears.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one Year Level!");
                return;
            }

            
            var selectedCourses = new List<string>();
            foreach (var item in lstCourses.SelectedItems)
            {
                // item is likely a string if you only did lstCourses.Items.Add("BSCS");
                selectedCourses.Add(item.ToString());
            }
            string courseCsv = string.Join(",", selectedCourses);

            // 3) Build CSV of selected years
            var selectedYears = new List<string>();
            foreach (var item in lstYears.SelectedItems)
            {
                selectedYears.Add(item.ToString());
            }
            string yearCsv = string.Join(",", selectedYears);

            // 4) Insert row in 'event' table:
            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"
                INSERT INTO event
                (event_name, event_start_date, event_end_date, 
                 participants_course, participants_year)
                VALUES
                (@ename, @start, @end, @courseCsv, @yearCsv)";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ename", eventName);
                        cmd.Parameters.AddWithValue("@start", start.Value);
                        cmd.Parameters.AddWithValue("@end", end.Value);
                        cmd.Parameters.AddWithValue("@courseCsv", courseCsv);
                        cmd.Parameters.AddWithValue("@yearCsv", yearCsv);

                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Event added successfully!");

                // 5) Optionally clear fields 
                txtEventName.Clear();
                dtpEventStart.SelectedDate = null;
                dtpEventEnd.SelectedDate = null;
                lstCourses.UnselectAll();
                lstYears.UnselectAll();

                // 6) Refresh the DataGrid
                LoadEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding event: " + ex.Message);
            }
        }





        private void LoadCoursesAndYears()
        {
            lstCourses.Items.Clear();
            lstYears.Items.Clear();

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    // 1) Fetch all courses
                    string sqlCourses = "SELECT course_id, course_name FROM course ORDER BY course_name";
                    using (var cmdCourses = new MySqlCommand(sqlCourses, conn))
                    {
                        using (var dr = cmdCourses.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                var c = new CourseItem
                                {
                                    ID = dr.GetInt32("course_id"),
                                    Name = dr.GetString("course_name")
                                };
                                lstCourses.Items.Add(c);
                            }
                        }
                    }

                    // 2) Fetch all years
                    string sqlYears = "SELECT year_id, year_name FROM year_level ORDER BY year_name";
                    using (var cmdYears = new MySqlCommand(sqlYears, conn))
                    {
                        using (var dr = cmdYears.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                var y = new YearItem
                                {
                                    ID = dr.GetInt32("year_id"),
                                    Name = dr.GetString("year_name")
                                };
                                lstYears.Items.Add(y);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading courses/years: " + ex.Message);
            }
        }



        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            // Which row are we editing?
            var btn = sender as FrameworkElement;
            var row = btn?.DataContext as EventModel;
            if (row == null) return;

            // Confirm we want to edit
            if (MessageBox.Show($"Edit event \"{row.event_name}\"?",
                                "Confirm",
                                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            // 1) Prepare a list of all possible courses/years from your DB or from the same 
            //    arrays you used for the Add side. For instance:
            var allCourses = new List<string>();
            var allYears = new List<string>();
            // load them from DB or from memory:
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // fetch courses
                string sqlC = "SELECT course_name FROM course ORDER BY course_name";
                using (var cmdC = new MySqlCommand(sqlC, conn))
                {
                    using (var dr = cmdC.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            allCourses.Add(dr.GetString("course_name"));
                        }
                    }
                }
                // fetch years
                string sqlY = "SELECT year_name FROM year_level ORDER BY year_name";
                using (var cmdY = new MySqlCommand(sqlY, conn))
                {
                    using (var dr = cmdY.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            allYears.Add(dr.GetString("year_name"));
                        }
                    }
                }
            }
            

            // 2) Parse the CSV from row.participants_course into a List<string> 
            List<string> selectedCourses = new List<string>();
            if (!string.IsNullOrEmpty(row.participants_course))
            {
                // e.g. "BSCS,BSIS" => ["BSCS","BSIS"]
                selectedCourses = new List<string>(row.participants_course.Split(','));
            }

            // similarly for participants_year
            List<string> selectedYears = new List<string>();
            if (!string.IsNullOrEmpty(row.participants_year))
            {
                selectedYears = new List<string>(row.participants_year.Split(','));
            }
            var editWnd = new EditEventWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            editWnd.LoadEventData(
                row.event_name,
                row.event_start_date,
                row.event_end_date,
                allCourses,
                allYears,
                selectedCourses,
                selectedYears

            );

            // 4) ShowDialog - if user hits Save => true
            if (editWnd.ShowDialog() == true)
            {
                // user pressed Save 
                var newName = editWnd.EditedName;
                var newStart = editWnd.EditedStart.Value;
                var newEnd = editWnd.EditedEnd.Value;
                var newCourseCsv = string.Join(",", editWnd.EditedCourses);
                var newYearCsv = string.Join(",", editWnd.EditedYears);

                // update DB
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"
                UPDATE event
                SET event_name=@ename,
                    event_start_date=@start,
                    event_end_date=@end,
                    participants_course=@courseCsv,
                    participants_year=@yearCsv
                WHERE event_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ename", newName);
                        cmd.Parameters.AddWithValue("@start", newStart);
                        cmd.Parameters.AddWithValue("@end", newEnd);
                        cmd.Parameters.AddWithValue("@courseCsv", newCourseCsv);
                        cmd.Parameters.AddWithValue("@yearCsv", newYearCsv);
                        cmd.Parameters.AddWithValue("@id", row.event_id);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Event updated successfully!");
                // Reload the DataGrid
                LoadEvents();
            }
            else
            {
                // user canceled
            }
        }



        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var rowData = btn.DataContext as DPFPSampleWPF.Models.EventModel;
            if (rowData == null) return;

            // confirm
            if (MessageBox.Show("Delete this event?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "DELETE FROM event WHERE event_id=@eid";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@eid", rowData.event_id);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoadEvents();
            }
        }

        private void chkSelectAllCourses_Click(object sender, RoutedEventArgs e)
        {
            if (chkSelectAllCourses.IsChecked == true)
            {
                // Mark all items as selected
                lstCourses.SelectAll();
            }
            else
            {
                // Deselect everything
                lstCourses.UnselectAll();
            }
        }

        private void chkSelectAllYears_Click(object sender, RoutedEventArgs e)
        {
            if (chkSelectAllYears.IsChecked == true)
            {
                lstYears.SelectAll();
            }
            else
            {
                lstYears.UnselectAll();
            }
        }


        // 2) COURSES TAB

        private void LoadCourses()
        {
            _coursesList.Clear();
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "SELECT course_id, course_name FROM course";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            _coursesList.Add(new CourseModel
                            {
                                course_id = dr.GetInt32("course_id"),
                                course_name = dr.GetString("course_name")
                            });
                        }
                    }
                }
            }
            dgCourses.ItemsSource = _coursesList;
        }

        private void btnAddCourse_Click(object sender, RoutedEventArgs e)
        {
            string courseName = txtCourseName.Text.Trim();
            if (string.IsNullOrEmpty(courseName))
            {
                MessageBox.Show("Course name is required!");
                return;
            }
            // Insert
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "INSERT INTO course (course_name) VALUES (@cname)";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cname", courseName);
                    cmd.ExecuteNonQuery();
                }
            }
            MessageBox.Show("Course added!");
            txtCourseName.Clear();
            LoadCourses();
        }

        private void EditCourse_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var rowData = btn?.DataContext as CourseModel;
            if (rowData == null) return;

            // Confirm
            if (MessageBox.Show($"Edit course \"{rowData.course_name}\"?",
                                "Confirm",
                                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            // Option A: Use a quick prompt (like VB’s InputBox) 
            //           or a small custom WPF window:
            //-----------
            // Using a quick InputBox from VB libraries:
            //    string newName = Microsoft.VisualBasic.Interaction.InputBox(
            //        "Enter new course name:",
            //        "Edit Course",
            //        rowData.course_name);
            //
            // If that doesn't work (missing ref), see Option B or 
            // add reference to "Microsoft.VisualBasic".
            //-----------

            // Option B: Roll your own small WPF window to gather the new name:
            //-----------
            string newName = ShowEditCourseDialog(rowData.course_name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                // user canceled or left blank
                return;
            }

            // 2) Update DB
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "UPDATE course SET course_name=@cname WHERE course_id=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cname", newName);
                    cmd.Parameters.AddWithValue("@id", rowData.course_id);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("Course updated successfully!");

            // 3) Refresh DataGrid
            LoadCourses();
        }
        private string ShowEditCourseDialog(string currentName)
        {
            // Create a small window on the fly
            Window dialog = new Window
            {
                Title = "Edit Course",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Owner = Window.GetWindow(this)  // keep parent-child relationship
            };

            // StackPanel + child controls
            var stack = new StackPanel { Margin = new Thickness(10) };
            var tbLabel = new TextBlock
            {
                Text = "Enter new course name:",
                Margin = new Thickness(0, 0, 0, 5)
            };
            var txtInput = new TextBox
            {
                Text = currentName,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnOK = new Button { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Width = 60 };

            stack.Children.Add(tbLabel);
            stack.Children.Add(txtInput);
            btnPanel.Children.Add(btnOK);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;

            // Hook up events
            string result = null;
            btnOK.Click += (s, e) =>
            {
                result = txtInput.Text.Trim();
                dialog.DialogResult = true;
                dialog.Close();
            };
            btnCancel.Click += (s, e) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            // Show Dialog
            bool? diagRes = dialog.ShowDialog();
            if (diagRes == true)
            {
                return result;  // user pressed OK
            }
            else
            {
                // user canceled
                return null;
            }
        }



        private void DeleteCourse_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var rowData = btn.DataContext as CourseModel;
            if (rowData == null) return;

            if (MessageBox.Show("Delete this course?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "DELETE FROM course WHERE course_id=@cid";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@cid", rowData.course_id);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoadCourses();
            }
        }

        // 3) TIME SCHEDULE TAB

        private void LoadTimeSchedule()
        {
            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "SELECT * FROM time_schedule WHERE time_id = 1 LIMIT 1";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                // Pull each TIME column as a TimeSpan
                                TimeSpan timeInAMStart = dr.GetTimeSpan("time_in_am_start");
                                TimeSpan timeInAMEnd = dr.GetTimeSpan("time_in_am_end");
                                TimeSpan timeOutAMStart = dr.GetTimeSpan("time_out_am_start");
                                TimeSpan timeOutAMEnd = dr.GetTimeSpan("time_out_am_end");
                                TimeSpan timeInPMStart = dr.GetTimeSpan("time_in_pm_start");
                                TimeSpan timeInPMEnd = dr.GetTimeSpan("time_in_pm_end");
                                TimeSpan timeOutPMStart = dr.GetTimeSpan("time_out_pm_start");
                                TimeSpan timeOutPMEnd = dr.GetTimeSpan("time_out_pm_end");

                                // Convert TimeSpan to string in "HH:mm:ss" format
                                txtTimeInAMStart.Text = timeInAMStart.ToString(@"hh\:mm\:ss");
                                txtTimeInAMEnd.Text = timeInAMEnd.ToString(@"hh\:mm\:ss");
                                txtTimeOutAMStart.Text = timeOutAMStart.ToString(@"hh\:mm\:ss");
                                txtTimeOutAMEnd.Text = timeOutAMEnd.ToString(@"hh\:mm\:ss");
                                txtTimeInPMStart.Text = timeInPMStart.ToString(@"hh\:mm\:ss");
                                txtTimeInPMEnd.Text = timeInPMEnd.ToString(@"hh\:mm\:ss");
                                txtTimeOutPMStart.Text = timeOutPMStart.ToString(@"hh\:mm\:ss");
                                txtTimeOutPMEnd.Text = timeOutPMEnd.ToString(@"hh\:mm\:ss");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading time schedule: " + ex.Message);
            }
        }

        private void btnSaveTime_Click(object sender, RoutedEventArgs e)
        {
            // Example of reading text back as TimeSpan
            // if user typed "08:00:00" we parse it, then update DB
            try
            {
                TimeSpan inAMStart = TimeSpan.Parse(txtTimeInAMStart.Text);
                TimeSpan inAMEnd = TimeSpan.Parse(txtTimeInAMEnd.Text);
                TimeSpan outAMStart = TimeSpan.Parse(txtTimeOutAMStart.Text);
                TimeSpan outAMEnd = TimeSpan.Parse(txtTimeOutAMEnd.Text);
                TimeSpan inPMStart = TimeSpan.Parse(txtTimeInPMStart.Text);
                TimeSpan inPMEnd = TimeSpan.Parse(txtTimeInPMEnd.Text);
                TimeSpan outPMStart = TimeSpan.Parse(txtTimeOutPMStart.Text);
                TimeSpan outPMEnd = TimeSpan.Parse(txtTimeOutPMEnd.Text);

                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    // check if row with time_id=1 exists
                    string checkSql = "SELECT COUNT(*) FROM time_schedule WHERE time_id=1";
                    long count;
                    using (var cmdCheck = new MySqlCommand(checkSql, conn))
                    {
                        count = (long)cmdCheck.ExecuteScalar();
                    }

                    if (count > 0)
                    {
                        // update
                        string updateSql = @"
                            UPDATE time_schedule
                            SET 
                              time_in_am_start  = @inAMStart,
                              time_in_am_end    = @inAMEnd,
                              time_out_am_start = @outAMStart,
                              time_out_am_end   = @outAMEnd,
                              time_in_pm_start  = @inPMStart,
                              time_in_pm_end    = @inPMEnd,
                              time_out_pm_start = @outPMStart,
                              time_out_pm_end   = @outPMEnd
                            WHERE time_id = 1
                        ";
                        using (var cmdUpd = new MySqlCommand(updateSql, conn))
                        {
                            cmdUpd.Parameters.AddWithValue("@inAMStart", inAMStart);
                            cmdUpd.Parameters.AddWithValue("@inAMEnd", inAMEnd);
                            cmdUpd.Parameters.AddWithValue("@outAMStart", outAMStart);
                            cmdUpd.Parameters.AddWithValue("@outAMEnd", outAMEnd);
                            cmdUpd.Parameters.AddWithValue("@inPMStart", inPMStart);
                            cmdUpd.Parameters.AddWithValue("@inPMEnd", inPMEnd);
                            cmdUpd.Parameters.AddWithValue("@outPMStart", outPMStart);
                            cmdUpd.Parameters.AddWithValue("@outPMEnd", outPMEnd);
                            cmdUpd.ExecuteNonQuery();
                        }
                        MessageBox.Show("Time schedule updated!");
                    }
                    else
                    {
                        // insert new row
                        string insertSql = @"
                            INSERT INTO time_schedule
                            (time_id,
                             time_in_am_start, time_in_am_end,
                             time_out_am_start, time_out_am_end,
                             time_in_pm_start, time_in_pm_end,
                             time_out_pm_start, time_out_pm_end)
                            VALUES
                            (1,
                             @inAMStart, @inAMEnd,
                             @outAMStart, @outAMEnd,
                             @inPMStart, @inPMEnd,
                             @outPMStart, @outPMEnd)
                        ";
                        using (var cmdIns = new MySqlCommand(insertSql, conn))
                        {
                            cmdIns.Parameters.AddWithValue("@inAMStart", inAMStart);
                            cmdIns.Parameters.AddWithValue("@inAMEnd", inAMEnd);
                            cmdIns.Parameters.AddWithValue("@outAMStart", outAMStart);
                            cmdIns.Parameters.AddWithValue("@outAMEnd", outAMEnd);
                            cmdIns.Parameters.AddWithValue("@inPMStart", inPMStart);
                            cmdIns.Parameters.AddWithValue("@inPMEnd", inPMEnd);
                            cmdIns.Parameters.AddWithValue("@outPMStart", outPMStart);
                            cmdIns.Parameters.AddWithValue("@outPMEnd", outPMEnd);
                            cmdIns.ExecuteNonQuery();
                        }
                        MessageBox.Show("Time schedule inserted!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving time schedule: " + ex.Message);
            }
        }



        // Just an example of a tab change event
        private void tabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Possibly reload data if needed
        }
        public class CourseItem
        {
            public int ID { get; set; }
            public string Name { get; set; }

            // So ListBox displays the name text
            public override string ToString() => Name;
        }

        public class YearItem
        {
            public int ID { get; set; }
            public string Name { get; set; }

            public override string ToString() => Name;
        }
    }
}
