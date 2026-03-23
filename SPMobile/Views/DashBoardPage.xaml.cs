using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using SPMobile.Models;
using System.Collections.ObjectModel;

namespace SPMobile.Views;

public partial class DashBoardPage : ContentPage
{
    FirebaseClient firebase = new FirebaseClient("https://attendancesystem-cb683-default-rtdb.firebaseio.com/");
    string currentUserName = Preferences.Default.Get("CurrentStudentName", "Unknown Student");
    string studentId = Preferences.Default.Get("CurrentStudentId", "");

    // handle the "Scanning..." and "All Clear" messages
    private string _emptyStateText;
    public string EmptyStateText
    {
        get => _emptyStateText;
        set { _emptyStateText = value; OnPropertyChanged(); }
    }

    public class AttendanceRecord
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("studentID")] public string StudentID { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
        public string Date { get; set; }
    }

    public class CourseAttendance : List<AttendanceRecord>
    {
        public string CourseName { get; set; }
        public CourseAttendance(string name, List<AttendanceRecord> records) : base(records) => CourseName = name;
    }

    public DashBoardPage()
    {
        InitializeComponent();
        lblStudentName.Text = $"On Campus: {currentUserName}";
        BindingContext = this; // Necessary for EmptyStateText binding
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // CALL BOTH METHODS HERE
        await LoadAttendanceData();
        await LoadNotifications();
    }

    private async Task LoadAttendanceData()
    {
        try
        {
            var studentEntries = await firebase.Child("students").OnceAsync<dynamic>();
            var myStudentRecords = studentEntries.Where(s => s.Object.studentID == studentId).ToList();

            if (myStudentRecords.Count == 0) return;

            var uniqueCourses = myStudentRecords
              .Select(r => (string)(r.Object.stuCourses?.ToString() ?? ""))
              .SelectMany(s => s.Split(','))
              .Select(c => c.Trim())
              .Where(c => !string.IsNullOrEmpty(c))
              .Distinct()
              .ToList();

            var groupedList = new List<CourseAttendance>();

            foreach (var courseName in uniqueCourses)
            {
                var tempRecords = new List<AttendanceRecord>();
                try
                {
                    var attendanceData = await firebase
                        .Child("attendance")
                        .Child(courseName)
                        .Child("Prelim")
                        .OnceAsync<IDictionary<string, AttendanceRecord>>();

                    if (attendanceData != null)
                    {
                        foreach (var dateNode in attendanceData)
                        {
                            string formattedDate = DateTime.TryParse(dateNode.Key, out DateTime d)
                                ? d.ToString("MMMM dd, yyyy") : dateNode.Key;

                            foreach (var recordEntry in dateNode.Object)
                            {
                                var record = recordEntry.Value;
                                if ((record.StudentID ?? "").Trim() == studentId.Trim())
                                {
                                    record.Date = formattedDate;
                                    tempRecords.Add(record);
                                }
                            }
                        }
                    }
                }
                catch { }

                var sorted = tempRecords
                    .OrderByDescending(r => DateTime.TryParse(r.Date, out var dt) ? dt : DateTime.MinValue)
                    .ToList();

                groupedList.Add(new CourseAttendance(courseName, sorted));
            }

            MainThread.BeginInvokeOnMainThread(() => cvCourses.ItemsSource = groupedList);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task LoadNotifications()
    {
        EmptyStateText = "Scanning all course records...";
        var alerts = new List<NotificationsPage.AttendanceAlert>();

        try
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                EmptyStateText = "No Student ID found.";
                return;
            }

            var allAttendanceNodes = await firebase.Child("attendance").OnceAsync<object>();
            if (allAttendanceNodes == null)
            {
                EmptyStateText = "No attendance data found.";
                return;
            }

            // Get per-user last viewed notifications timestamp
            string lastViewedKey = $"LastViewedNotifications_{studentId}";
            var lastViewedSaved = Preferences.Default.Get(lastViewedKey, "");
            DateTime lastViewed = DateTime.TryParse(lastViewedSaved, out var dt) ? dt : DateTime.MinValue;

            // Loop through courses
            foreach (var courseNode in allAttendanceNodes)
            {
                string courseName = courseNode.Key;

                var dateNodes = await firebase
                    .Child("attendance")
                    .Child(courseName)
                    .Child("Prelim")
                    .OnceAsync<object>();

                if (dateNodes == null) continue;

                foreach (var dateNode in dateNodes)
                {
                    var json = dateNode.Object.ToString();
                    var records = JsonConvert.DeserializeObject<Dictionary<string, NotificationsPage.AttendanceRecord>>(json);
                    if (records == null) continue;

                    foreach (var record in records.Values)
                    {
                        if ((record.StudentID ?? "").Trim() != studentId.Trim()) continue;

                        string stat = record.Status?.ToUpper() ?? "";
                        if (stat != "ABSENT" && stat != "LATE") continue;

                        DateTime recordTime = DateTime.MinValue;
                        if (!string.IsNullOrEmpty(record.Timestamp) &&
                            DateTime.TryParse(record.Timestamp, out DateTime parsed))
                        {
                            recordTime = parsed;
                        }

                        alerts.Add(new NotificationsPage.AttendanceAlert
                        {
                            Title = stat == "ABSENT" ? "Absence Recorded" : "Tardiness Alert",
                            Message = $"You were marked {stat} in {courseName}.",
                            Date = recordTime != DateTime.MinValue
                                ? recordTime.ToString("MMMM dd, yyyy hh:mm tt")
                                : DateTime.UtcNow.ToString("MMMM dd, yyyy hh:mm tt"), // fallback to now
                            AlertColor = stat == "ABSENT" ? Colors.Red : Colors.Orange
                        });
                    }
                }
            }

            // Update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Sort alerts by newest first
                var sortedAlerts = alerts
                    .OrderByDescending(a => DateTime.TryParse(a.Date, out var d) ? d : DateTime.MinValue)
                    .ToList();

                // Optional: show alerts in a CollectionView on Dashboard (if you have one)
                // cvDashboardNotifications.ItemsSource = sortedAlerts;

                // Count only unread alerts
                int unreadCount = sortedAlerts.Count(a =>
                {
                    DateTime.TryParse(a.Date, out var dt);
                    return dt > lastViewed;
                });

                EmptyStateText = unreadCount == 0 ? "Your attendance is all clear!" : $"{unreadCount} new notifications";
                NotificationHelper.SetBadge(unreadCount);
            });
        }
        catch (Exception ex)
        {
            EmptyStateText = "Error loading alerts.";
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        }
    }

    private async void OnCourseTapped(object sender, EventArgs e)
    {
        var selectedCourse = (CourseAttendance)((VisualElement)sender).BindingContext;
        if (selectedCourse == null) return;

        int present = selectedCourse.Count(r => r.Status.Equals("Present", StringComparison.OrdinalIgnoreCase));
        int absent = selectedCourse.Count(r => r.Status.Equals("Absent", StringComparison.OrdinalIgnoreCase));
        int late = selectedCourse.Count(r => r.Status.Equals("Late", StringComparison.OrdinalIgnoreCase));

        double percent = selectedCourse.Any() ? ((double)(present + late) / selectedCourse.Count) * 100 : 0;

        lblModalTitle.Text = selectedCourse.CourseName;
        lblPresentCount.Text = present.ToString();
        lblAbsentCount.Text = absent.ToString();
        lblLateCount.Text = late.ToString();
        lblPercentage.Text = $"{Math.Round(percent)}%";
        cvModalAttendance.ItemsSource = selectedCourse;

        AttendanceModal.IsVisible = true;
        ModalBackground.IsVisible = true;
        ModalBackground.Opacity = 0;
        AttendanceModal.Scale = 0.8;
        await Task.WhenAll(ModalBackground.FadeTo(0.6, 200), AttendanceModal.ScaleTo(1, 200, Easing.CubicOut));
    }

    private async void CloseModal(object sender, EventArgs e)
    {
        await Task.WhenAll(ModalBackground.FadeTo(0, 200), AttendanceModal.ScaleTo(0.8, 200, Easing.CubicIn));
        AttendanceModal.IsVisible = false;
        ModalBackground.IsVisible = false;
    }
}