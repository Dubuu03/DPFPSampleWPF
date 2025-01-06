using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DPFPSampleWPF
{
    public partial class EditEventWindow : Window
    {
        // For returning the user’s changes
        public string EditedName { get; private set; }
        public DateTime? EditedStart { get; private set; }
        public DateTime? EditedEnd { get; private set; }
        public List<string> EditedCourses { get; private set; } = new List<string>();
        public List<string> EditedYears { get; private set; } = new List<string>();

        public EditEventWindow()
        {
            InitializeComponent();
        }

        // Provide a method to fill the window with the existing data
        public void LoadEventData(string eventName, DateTime? start, DateTime? end,
                                  List<string> coursesAll, List<string> yearsAll,
                                  IEnumerable<string> selectedCourses,
                                  IEnumerable<string> selectedYears)
        {
            // 1) Prefill the fields
            txtEventName.Text = eventName;
            dtpStart.SelectedDate = start;
            dtpEnd.SelectedDate = end;

            // 2) Load list of all courses
            lstCourses.Items.Clear();
            foreach (var c in coursesAll)
            {
                lstCourses.Items.Add(c);
            }
            // then select the ones that match
            if (selectedCourses != null)
            {
                foreach (var item in selectedCourses)
                {
                    int index = lstCourses.Items.IndexOf(item);
                    if (index >= 0) lstCourses.SelectedItems.Add(lstCourses.Items[index]);
                }
            }

            // 3) Similarly for year
            lstYears.Items.Clear();
            foreach (var y in yearsAll)
            {
                lstYears.Items.Add(y);
            }
            if (selectedYears != null)
            {
                foreach (var item in selectedYears)
                {
                    int index = lstYears.Items.IndexOf(item);
                    if (index >= 0) lstYears.SelectedItems.Add(lstYears.Items[index]);
                }
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // read from the fields
            EditedName = txtEventName.Text.Trim();
            EditedStart = dtpStart.SelectedDate;
            EditedEnd = dtpEnd.SelectedDate;

            // get selected courses as List<string>
            EditedCourses.Clear();
            foreach (var item in lstCourses.SelectedItems)
            {
                EditedCourses.Add(item.ToString());
            }

            // get selected year
            EditedYears.Clear();
            foreach (var item in lstYears.SelectedItems)
            {
                EditedYears.Add(item.ToString());
            }

            if (string.IsNullOrEmpty(EditedName))
            {
                MessageBox.Show("Event Name cannot be empty!");
                return;
            }
            if (!EditedStart.HasValue || !EditedEnd.HasValue)
            {
                MessageBox.Show("Must pick Start and End date!");
                return;
            }

            this.DialogResult = true;  // indicates success
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

    }
}
