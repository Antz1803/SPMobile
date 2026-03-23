using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
using SPMobile.Models;
using System.ComponentModel;

namespace SPMobile.Views;

public partial class NotificationsPage : ContentPage, INotifyPropertyChanged
{
    FirebaseClient firebase = new FirebaseClient("https://attendancesystem-cb683-default-rtdb.firebaseio.com/");
    string studentId = Preferences.Default.Get("CurrentStudentId", "");

    private string _emptyStateText = "Loading...";
    public string EmptyStateText
    {
        get => _emptyStateText;
        set { _emptyStateText = value; OnPropertyChanged(nameof(EmptyStateText)); }
    }

    public class AttendanceAlert
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Date { get; set; }
        public Color AlertColor { get; set; }
    }

    public class AttendanceRecord
    {
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("studentID")] public string StudentID { get; set; }
        [JsonProperty("timestamp")] public string Timestamp { get; set; }
    }

    public NotificationsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load notifications from Firebase
        await LoadNotifications();

        // Mark notifications as read for this user AFTER loading
        SetLastViewed();
        NotificationHelper.ClearBadge();
    }

    private async Task LoadNotifications()
    {
        EmptyStateText = "Scanning all course records...";
        var alerts = new List<AttendanceAlert>();

        try
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                EmptyStateText = "No Student ID found.";
                return;
            }

            // Fetch all courses under 'attendance' node
            var allAttendanceNodes = await firebase.Child("attendance").OnceAsync<object>();
            if (allAttendanceNodes == null)
            {
                EmptyStateText = "No attendance data found.";
                return;
            }

            DateTime lastViewed = GetLastViewed(); // per-user last viewed

            // Iterate each course node
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
                    var records = JsonConvert.DeserializeObject<Dictionary<string, AttendanceRecord>>(json);
                    if (records == null) continue;

                    foreach (var record in records.Values)
                    {
                        if ((record.StudentID ?? "").Trim() == studentId.Trim())
                        {
                            string stat = record.Status?.ToUpper() ?? "";

                            if (stat == "ABSENT" || stat == "LATE")
                            {
                                DateTime recordTime = DateTime.MinValue;

                                if (!string.IsNullOrEmpty(record.Timestamp) &&
                                    DateTime.TryParse(record.Timestamp, out DateTime parsed))
                                {
                                    recordTime = parsed;
                                }

                                alerts.Add(new AttendanceAlert
                                {
                                    Title = stat == "ABSENT" ? "Absence Alert" : "Tardiness Alert",
                                    Message = $"You were marked {stat} in {courseName}.",
                                    Date = recordTime != DateTime.MinValue
                                            ? recordTime.ToString("MMMM dd, yyyy hh:mm tt")
                                            : dateNode.Key,
                                    AlertColor = stat == "ABSENT" ? Colors.Red : Colors.Orange
                                });
                            }
                        }
                    }
                }
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                cvNotifications.ItemsSource = alerts
                    .OrderByDescending(a => DateTime.TryParse(a.Date, out var d) ? d : DateTime.MinValue)
                    .ToList();

                // count only notifications newer than last viewed
                int unreadCount = alerts.Count(a =>
                {
                    DateTime.TryParse(a.Date, out var dt);
                    return dt > lastViewed;
                });

                EmptyStateText = unreadCount == 0 ? "Your attendance is all clear!" : "";
                NotificationHelper.SetBadge(unreadCount);
            });
        }
        catch (Exception ex)
        {
            EmptyStateText = "Error loading data.";
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: {ex.Message}");
        }
    }

    // Save last viewed notifications per user
    private void SetLastViewed()
    {
        if (string.IsNullOrWhiteSpace(studentId)) return;

        string lastViewedKey = $"LastViewedNotifications_{studentId}";
        Preferences.Default.Set(lastViewedKey, DateTime.UtcNow.ToString("o"));
    }

    // Retrieve the last time notifications were viewed for this user
    private DateTime GetLastViewed()
    {
        if (string.IsNullOrWhiteSpace(studentId)) return DateTime.MinValue;

        string lastViewedKey = $"LastViewedNotifications_{studentId}";
        var saved = Preferences.Default.Get(lastViewedKey, "");
        return DateTime.TryParse(saved, out var dt) ? dt : DateTime.MinValue;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}