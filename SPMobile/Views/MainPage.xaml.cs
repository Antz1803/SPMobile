using SPMobile.Services;

namespace SPMobile.Views;

public partial class MainPage : ContentPage
{
    FirebaseService _service = new();

    public MainPage()
    {
        InitializeComponent();

        LoadSavedCredentials();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string studentId = StudentIDEntry.Text;
        string pass = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(studentId) || string.IsNullOrWhiteSpace(pass))
        {
            await DisplayAlert("Error", "Please enter both Student ID and Password", "OK");
            return;
        }

      ((Button)sender).IsEnabled = false;

        try
        {
            bool isSuccess = await _service.LoginUser(studentId, pass);

            if (isSuccess)
            {
                // Save if Remember Me is checked
                if (RememberMeCheckBox.IsChecked)
                {
                    Preferences.Default.Set("saved_studentId", studentId);
                    Preferences.Default.Set("saved_password", pass);
                    Preferences.Default.Set("remember_me", true);
                }
                else
                {
                    Preferences.Default.Remove("saved_studentId");
                    Preferences.Default.Remove("saved_password");
                    Preferences.Default.Set("remember_me", false);
                }

                await Shell.Current.GoToAsync("//DashBoardPage");
            }
            else
            {
                await DisplayAlert("Login Failed", "Invalid Student ID or Password.", "OK");
            }
        }
        catch
        {
            // This usually happens when there is no internet or Firebase is unreachable
            await DisplayAlert("Error", "Please check your internet connection.", "OK");
        }

      ((Button)sender).IsEnabled = true;
    }
    private void LoadSavedCredentials()
    {
        bool remember = Preferences.Default.Get("remember_me", false);

        if (remember)
        {
            StudentIDEntry.Text = Preferences.Default.Get("saved_studentId", string.Empty);
            PasswordEntry.Text = Preferences.Default.Get("saved_password", string.Empty);
            RememberMeCheckBox.IsChecked = true;
        }
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        // Toggle the password visibility
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;

        // Cast the sender to an ImageButton so we can change its picture
        var btn = (ImageButton)sender;

        // Swap the image based on the state
        btn.Source = PasswordEntry.IsPassword ? "eyehide.png" : "eyeshow.png";
    }

    private async void OnSignUpTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("SignUpPage");
}