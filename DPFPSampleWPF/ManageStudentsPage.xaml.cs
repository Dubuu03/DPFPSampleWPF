using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DPFPSampleWPF.Models;
using Microsoft.VisualBasic;             // for Interaction.InputBox
using MySql.Data.MySqlClient;

namespace DPFPSampleWPF
{
    public partial class ManageStudentsPage : Page
    {
        private int? _userID;
        private string _userStatus;
        private string connStr = "server=localhost;database=testdb;uid=root;pwd=1234;";

        // For DataGrid binding
        private ObservableCollection<AccountModel> _accountsList
            = new ObservableCollection<AccountModel>();

        public ManageStudentsPage(int? userID, string userStatus)
        {
            InitializeComponent();
            _userID = userID;         // which user is logged in
            _userStatus = userStatus; // "Admin" or "Student"

            // Decide which UI to show
            if (_userStatus == "Admin")
            {
                dgAccounts.Visibility = Visibility.Visible;
                panelStudent.Visibility = Visibility.Collapsed;
                dgAccounts.ItemsSource = _accountsList;
                LoadAllAccounts(); // show all
            }
            else if (_userStatus == "Student")
            {
                dgAccounts.Visibility = Visibility.Collapsed;
                panelStudent.Visibility = Visibility.Visible;
                if (_userID.HasValue)
                {
                    LoadSingleAccount(_userID.Value); // show only that user
                }
            }
        }

        private void LoadAllAccounts()
        {
            _accountsList.Clear();
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT id, student_id, name, course, year, status
                    FROM account
                    ORDER BY id
                ";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            _accountsList.Add(new AccountModel
                            {
                                id = dr.GetInt32("id"),
                                student_id = dr.IsDBNull(dr.GetOrdinal("student_id"))
                                             ? "" : dr.GetString("student_id"),
                                name = dr.GetString("name"),
                                course = dr.IsDBNull(dr.GetOrdinal("course"))
                                             ? "" : dr.GetString("course"),
                                year = dr.IsDBNull(dr.GetOrdinal("year"))
                                             ? "" : dr.GetString("year"),
                                status = dr.GetString("status")
                            });
                        }
                    }
                }
            }
        }

        private void LoadSingleAccount(int userID)
        {
            // fetch that one row from account
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    SELECT student_id, name, course, year, password
                    FROM account
                    WHERE id=@uid
                    LIMIT 1
                ";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", userID);
                    using (var dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            txtStudentID.Text = dr.IsDBNull(dr.GetOrdinal("student_id"))
                                ? "" : dr.GetString("student_id");
                            txtName.Text = dr.GetString("name");
                            txtCourse.Text = dr.IsDBNull(dr.GetOrdinal("course"))
                                ? "" : dr.GetString("course");
                            txtYear.Text = dr.IsDBNull(dr.GetOrdinal("year"))
                                ? "" : dr.GetString("year");
                            // password not shown in plain text, but here we allow editing
                            txtPassword.Password = dr.IsDBNull(dr.GetOrdinal("password"))
                                ? "" : dr.GetString("password");
                        }
                    }
                }
            }
        }

        // Student => Save button
        private void btnSaveStudent_Click(object sender, RoutedEventArgs e)
        {
            if (!_userID.HasValue) return;

            // read fields
            string newName = txtName.Text.Trim();
            string newCourse = txtCourse.Text.Trim();
            string newYear = txtYear.Text.Trim();
            string newPass = txtPassword.Password.Trim(); // if changing password

            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"
                    UPDATE account
                    SET name=@n, course=@c, year=@y, password=@p
                    WHERE id=@uid
                ";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@n", newName);
                    cmd.Parameters.AddWithValue("@c", newCourse);
                    cmd.Parameters.AddWithValue("@y", newYear);
                    cmd.Parameters.AddWithValue("@p", newPass);
                    cmd.Parameters.AddWithValue("@uid", _userID.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("Your account info updated!");
        }

        // Admin => DataGrid's Edit
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_userStatus != "Admin") return; // safety check

            var btn = sender as FrameworkElement;
            var rowData = btn?.DataContext as AccountModel;
            if (rowData == null) return;

            // Quick input for Name (demo):
            string newName = Interaction.InputBox(
                "Enter new name:",
                "Edit Account",
                rowData.name
            );
            if (string.IsNullOrEmpty(newName)) return;

            // update DB
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = "UPDATE account SET name=@n WHERE id=@id";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@n", newName);
                    cmd.Parameters.AddWithValue("@id", rowData.id);
                    cmd.ExecuteNonQuery();
                }
            }
            MessageBox.Show("Name updated!");

            // refresh
            LoadAllAccounts();
        }

        // Admin => DataGrid's Delete
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_userStatus != "Admin") return; // safety check

            var btn = sender as FrameworkElement;
            var rowData = btn?.DataContext as AccountModel;
            if (rowData == null) return;

            if (MessageBox.Show("Delete this user?",
                                "Confirm",
                                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "DELETE FROM account WHERE id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", rowData.id);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("User deleted!");
                LoadAllAccounts();
            }
        }
        private void btnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the panel (or you can just set it Visible if you want always to do so)
            panelChangePassword.Visibility = Visibility.Visible;

            // Clear any previously typed passwords
            txtOldPassword.Clear();
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
        }
        private void btnSaveNewPassword_Click(object sender, RoutedEventArgs e)
        {
            if (!_userID.HasValue)
            {
                MessageBox.Show("No user ID found.");
                return;
            }

            string oldPass = txtOldPassword.Password.Trim();
            string newPass = txtNewPassword.Password.Trim();
            string confPass = txtConfirmPassword.Password.Trim();

            if (string.IsNullOrEmpty(oldPass) ||
                string.IsNullOrEmpty(newPass) ||
                string.IsNullOrEmpty(confPass))
            {
                MessageBox.Show("All password fields are required!");
                return;
            }

            if (newPass != confPass)
            {
                MessageBox.Show("New Password and Confirm Password do not match!");
                return;
            }

            // 1) Check if oldPass matches the account's current password in DB
            bool oldPassCorrect = false;
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string checkSql = "SELECT password FROM account WHERE id=@uid LIMIT 1";
                using (var cmdCheck = new MySqlCommand(checkSql, conn))
                {
                    cmdCheck.Parameters.AddWithValue("@uid", _userID.Value);
                    object result = cmdCheck.ExecuteScalar();
                    if (result != null)
                    {
                        string currentPassInDB = result.ToString();
                        oldPassCorrect = (currentPassInDB == oldPass);
                    }
                }
            }

            if (!oldPassCorrect)
            {
                MessageBox.Show("Old Password is incorrect!");
                return;
            }

            // 2) If old pass is correct, update DB with the new pass
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string updSql = @"
            UPDATE account
            SET password=@newPwd
            WHERE id=@uid
        ";
                using (var cmd = new MySqlCommand(updSql, conn))
                {
                    cmd.Parameters.AddWithValue("@newPwd", newPass);
                    cmd.Parameters.AddWithValue("@uid", _userID.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("Password updated successfully!");

            // Optionally hide the change-password panel:
            panelChangePassword.Visibility = Visibility.Collapsed;

            // Also update the read-only txtPassword field
            txtPassword.Password = newPass;
        }

    }
}
