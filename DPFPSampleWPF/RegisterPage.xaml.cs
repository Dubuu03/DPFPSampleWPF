using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;                       // For System.Drawing.Image, Bitmap
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;                // For OpenFileDialog
using System.Windows.Media.Imaging;
using MySql.Data.MySqlClient;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using DirectShowLib;                       // For DsDevice, FilterCategory
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WpfImage = System.Windows.Controls.Image;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation; // Alias WPF Image to avoid confusion

namespace DPFPSampleWPF
{
    public partial class RegisterPage : Page
    {
        private const int FINGER_COUNT = 10;

        private string connStr = "server=localhost;database=testdb;uid=root;pwd=1234;";

        // DPFP-related
        private Capture[] _capturers = new Capture[FINGER_COUNT];
        private Enrollment[] _enrollers = new Enrollment[FINGER_COUNT];
        private byte[][] fingerprintData = new byte[FINGER_COUNT][];
        private bool[] isFingerCapturedOk = new bool[FINGER_COUNT];

        // Photo 
        private byte[] photoData;

        // For enumerated camera devices
        private (int Index, string Name)[] cameraDevices;

        public RegisterPage()
        {
            InitializeComponent();
            InitializeFingerprintCaptures();
            LoadCoursesFromDB();
            LoadYearsFromDB();
            LoadCameraDevices();
            CreateFingerUI();
        }

        private void InitializeFingerprintCaptures()
        {
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                try
                {
                    _capturers[i] = new Capture();
                    _enrollers[i] = new Enrollment();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"InitCapture error for finger {i + 1}: {ex.Message}");
                }
            }
        }

        private void CreateFingerUI()
        {
            // We'll dynamically create rows for each finger: a button, an Image (preview), and a status label
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var btn = new Button
                {
                    Content = $"Capture Finger {i + 1}",
                    Width = 130,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var imgPreview = new WpfImage
                {
                    Width = 80,
                    Height = 80,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var lbl = new TextBlock
                {
                    Text = "Status: Idle",
                    VerticalAlignment = VerticalAlignment.Center
                };

                stack.Children.Add(btn);
                stack.Children.Add(imgPreview);
                stack.Children.Add(lbl);

                // By default, only the first finger is enabled
                if (i > 0) btn.IsEnabled = false;

                int fingerIndex = i;
                btn.Click += (s, e) => StartCaptureFinger(fingerIndex, btn, imgPreview, lbl);

                fingerList.Items.Add(stack);
            }
        }

        private void StartCaptureFinger(int index, Button btn, WpfImage imgPreview, TextBlock lblStatus)
        {
            // Make sure user captured the previous finger if needed
            if (index > 0 && !isFingerCapturedOk[index - 1])
            {
                MessageBox.Show($"Must capture Finger {index} before capturing Finger {index + 1}!");
                return;
            }

            lblStatus.Text = "Status: Capturing...";
            lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);

            _capturers[index].EventHandler = new FingerCaptureHandler(
                _enrollers[index],
                imgPreview,
                data => { fingerprintData[index] = data; },
                success =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (success)
                        {
                            lblStatus.Text = "Status: Fingerprint OK!";
                            lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                            isFingerCapturedOk[index] = true;

                            // Enable the next finger button
                            if (index < FINGER_COUNT - 1)
                            {
                                var nextStack = (StackPanel)fingerList.Items[index + 1];
                                var nextBtn = (Button)nextStack.Children[0];
                                nextBtn.IsEnabled = true;
                            }
                        }
                        else
                        {
                            lblStatus.Text = "Status: Enrollment failed. Try again.";
                            lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                            isFingerCapturedOk[index] = false;
                        }
                    });
                }
            );

            try
            {
                _capturers[index].StartCapture();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot start capture for Finger {index + 1}: {ex.Message}");
            }
        }

        private void LoadCameraDevices()
        {
            try
            {
                var systemCams = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                cameraDevices = new (int, string)[systemCams.Length];
                comboBoxCameras.Items.Clear();

                for (int i = 0; i < systemCams.Length; i++)
                {
                    cameraDevices[i] = (i, systemCams[i].Name);
                    comboBoxCameras.Items.Add(systemCams[i].Name);
                }

                if (systemCams.Length > 0)
                {
                    comboBoxCameras.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error enumerating cameras: " + ex.Message);
            }
        }

        private void LoadCoursesFromDB()
        {
            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "SELECT course_name FROM course";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string cName = dr.GetString("course_name");
                                cmbCourse.Items.Add(cName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading courses: " + ex.Message);
            }
        }

        private void LoadYearsFromDB()
        {
            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "SELECT year_name FROM year_level";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string yName = dr.GetString("year_name");
                                cmbYear.Items.Add(yName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading year levels: " + ex.Message);
            }
        }

        private void btnBrowsePhoto_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    using (System.Drawing.Image sdi = System.Drawing.Image.FromFile(ofd.FileName))
                    {
                        using (var bmp = new Bitmap(sdi, new System.Drawing.Size(120, 120)))
                        {
                            imgPhoto.Source = BitmapToImageSource(bmp);
                        }

                        using (var ms = new MemoryStream())
                        {
                            sdi.Save(ms, sdi.RawFormat);
                            photoData = ms.ToArray();
                        }
                    }
                }
            }
        }

        private void btnOpenCamera_Click(object sender, RoutedEventArgs e)
        {
            if (comboBoxCameras.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a camera from the list!");
                return;
            }
            int cameraIndex = comboBoxCameras.SelectedIndex;

            CameraPage camPage = new CameraPage(cameraIndex);
            Window wnd = new Window
            {
                Title = "Camera Preview",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = camPage,
                Owner = Window.GetWindow(this) // keep parent-child relationship
            };

            if (wnd.ShowDialog() == true)
            {
                byte[] captured = camPage.CapturedPhoto;
                if (captured != null && captured.Length > 0)
                {
                    using (var ms = new MemoryStream(captured))
                    {
                        using (System.Drawing.Image sdi = System.Drawing.Image.FromStream(ms))
                        {
                            using (var bmp = new Bitmap(sdi, new System.Drawing.Size(120, 120)))
                            {
                                imgPhoto.Source = BitmapToImageSource(bmp);
                            }
                        }
                    }
                    photoData = captured;
                }
            }
        }

        private BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
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

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string pass = txtPassword.Password.Trim();
            string conf = txtConfirmPassword.Password.Trim();
            if (pass != conf)
            {
                MessageBox.Show("Passwords do not match!");
                return;
            }

            // At least one fingerprint
            bool atLeastOneCaptured = false;
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                if (isFingerCapturedOk[i])
                {
                    atLeastOneCaptured = true;
                    break;
                }
            }
            if (!atLeastOneCaptured)
            {
                MessageBox.Show("Please capture at least one fingerprint!");
                return;
            }

            string studentID = txtStudentID.Text.Trim();
            string name = txtName.Text.Trim();
            if (cmbCourse.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a course!");
                return;
            }
            string course = cmbCourse.SelectedItem.ToString();

            if (cmbYear.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a year!");
                return;
            }
            string year = cmbYear.SelectedItem.ToString();

            string section = txtSection.Text.Trim();
            string status = "Student";

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    // 1) Check if student_id already exists
                    string checkSql = "SELECT COUNT(*) FROM account WHERE student_id = @studId";
                    using (var cmdCheck = new MySqlCommand(checkSql, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@studId", studentID);
                        long existingCount = (long)cmdCheck.ExecuteScalar();
                        if (existingCount > 0)
                        {
                            MessageBox.Show($"Student ID '{studentID}' already exists! Please input another Student ID.");
                            return;
                        }
                    }

                    // 2) Insert new record
                    string sql = @"
                    INSERT INTO account
                    (student_id, name, password, new_password, course, year, section, profile_photo,
                     fingerprint1, fingerprint2, fingerprint3, fingerprint4, fingerprint5,
                     fingerprint6, fingerprint7, fingerprint8, fingerprint9, fingerprint10,
                     status)
                    VALUES
                    (@student_id, @name, @password, @new_password, @course, @year, @section, @photo,
                     @fp1, @fp2, @fp3, @fp4, @fp5, @fp6, @fp7, @fp8, @fp9, @fp10,
                     @status)";

                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@student_id", studentID);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@password", pass);
                        cmd.Parameters.AddWithValue("@new_password", pass);
                        cmd.Parameters.AddWithValue("@course", course);
                        cmd.Parameters.AddWithValue("@year", year);
                        cmd.Parameters.AddWithValue("@section", section);
                        cmd.Parameters.AddWithValue("@photo", (object)photoData ?? DBNull.Value);

                        for (int i = 0; i < FINGER_COUNT; i++)
                        {
                            string paramName = "@fp" + (i + 1);
                            if (fingerprintData[i] != null)
                                cmd.Parameters.AddWithValue(paramName, fingerprintData[i]);
                            else
                                cmd.Parameters.AddWithValue(paramName, DBNull.Value);
                        }

                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Successfully registered with at least one fingerprint!");

                // 3) Reset the fields for another student
                ResetFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB error: " + ex.Message);
            }
        }

        private void ResetFields()
        {
            // Clear text fields
            txtStudentID.Text = "";
            txtName.Text = "";
            txtPassword.Password = "";
            txtConfirmPassword.Password = "";

            cmbCourse.SelectedIndex = -1;
            cmbYear.SelectedIndex = -1;
            txtSection.Text = "";

            // Clear photo
            imgPhoto.Source = null;
            photoData = null;

            // Reset all fingerprint captures
            for (int i = 0; i < FINGER_COUNT; i++)
            {
                fingerprintData[i] = null;
                isFingerCapturedOk[i] = false;
                // Re-disable all captures except finger[0]
                var rowStack = (StackPanel)fingerList.Items[i];
                var btn = (Button)rowStack.Children[0];
                var lbl = (TextBlock)rowStack.Children[2];

                // "Capture Finger #"
                if (i == 0)
                    btn.IsEnabled = true;
                else
                    btn.IsEnabled = false;

                lbl.Text = "Status: Idle";
                lbl.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);

                // Clear the preview image
                var wpfImg = (WpfImage)rowStack.Children[1];
                wpfImg.Source = null;

                // If we had an Enroller, we can reset it
                _enrollers[i].Clear();
            }
        }
    }
}
