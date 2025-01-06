using System;
using System.Windows;
using MySql.Data.MySqlClient;

namespace DPFPSampleWPF
{
    public partial class AdminLoginWindow : Window
    {
        // The status of the user who logged in: "Admin" or "Student".
        public string LoggedInStatus { get; private set; }
        public int? LoggedInUserID { get; private set; }

        private readonly string connStr = "server=localhost;database=testdb;uid=root;pwd=1234;";

        // If true, we require that the user must be Admin.
        // If false, we accept either Admin or Student.
        private bool _requireAdmin;

        public AdminLoginWindow(bool requireAdmin = true)
        {
            InitializeComponent();
            _requireAdmin = requireAdmin;
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtAccountID.Text.Trim();  // e.g., student_id
            string password = txtPassword.Password.Trim();

            try
            {
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();

                    // Query for matching account in the DB
                    string sql = @"SELECT id, status
                                   FROM account
                                   WHERE student_id = @studentID
                                     AND password   = @pass
                                   LIMIT 1";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentID", username);
                        cmd.Parameters.AddWithValue("@pass", password);

                        using (var dr = cmd.ExecuteReader())
                        {
                            if (!dr.Read())
                            {
                                // No row found => invalid credentials
                                MessageBox.Show("Invalid credentials!");
                                return;
                            }

                            // Found a match
                            int userId = dr.GetInt32("id");
                            string status = dr.GetString("status");  // e.g., "Admin" or "Student"

                            if (_requireAdmin)
                            {
                                // SCENARIO 1: We only allow Admin
                                if (status.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                                {
                                    LoggedInStatus = "Admin";
                                    LoggedInUserID = userId;
                                    this.DialogResult = true; // success
                                }
                                else
                                {
                                    // Not admin => fail
                                    MessageBox.Show("Only Admin is allowed for this action.");
                                }
                            }
                            else
                            {
                                // SCENARIO 2: Admin or Student is acceptable
                                // If you want to allow only those two statuses,
                                // check specifically for them.
                                if (status.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                                {
                                    LoggedInStatus = "Admin";
                                    LoggedInUserID = userId;
                                    this.DialogResult = true;
                                }
                                else if (status.Equals("Student", StringComparison.OrdinalIgnoreCase))
                                {
                                    LoggedInStatus = "Student";
                                    LoggedInUserID = userId;
                                    this.DialogResult = true;
                                }
                                else
                                {
                                    // Some other status => not allowed
                                    MessageBox.Show($"Your status \"{status}\" is not permitted.");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("DB error: " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;  // user canceled
        }
    }
}
