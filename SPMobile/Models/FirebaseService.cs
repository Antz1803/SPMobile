using Firebase.Database;
using Firebase.Database.Query;
using System.Diagnostics;
using Newtonsoft.Json.Linq; 

namespace SPMobile.Services;

public class FirebaseService
{
    private readonly FirebaseClient _client = new("https://attendancesystem-cb683-default-rtdb.firebaseio.com/");

    public async Task<bool> LoginUser(string studentId, string password)
    {
        try
        {
            // PATH MUST MATCH: used "users" to match RegisterUser
            var users = await _client.Child("users").OnceAsync<UserAccount>();

            if (users == null || !users.Any())
                return false;

            // Search for the user
            var user = users.FirstOrDefault(u =>
             u.Object.studentID == studentId.Trim() &&
             u.Object.password == password.Trim());

            if (user == null)
                return false;

            Preferences.Default.Set("CurrentStudentId", studentId);
            Preferences.Default.Set("CurrentStudentName", user.Object.fullName);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login Error: {ex.Message}");
            return false;
        }
    }

    public async Task RegisterUser(string fullName, string studentId, string email, string password)
    {
        // PATH MUST MATCH: changed to "users"
        await _client.Child("users").PostAsync(new UserAccount
        {
            fullName = fullName,
            studentID = studentId,
            password = password,
            gmail = email,
            role = "Student",
            createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        });
    }

    public class UserAccount
    {
        public string studentID { get; set; }
        public string password { get; set; }
        public string fullName { get; set; }
        public string gmail { get; set; }
        public string role { get; set; }
        public string createdAt { get; set; }

    }
}