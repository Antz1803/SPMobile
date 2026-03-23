using SPMobile.Services;

namespace SPMobile.Views;

public partial class SignUpPage : ContentPage
{
    FirebaseService _service = new();

    public SignUpPage() => InitializeComponent();

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(NameEntry.Text) || string.IsNullOrWhiteSpace(IdEntry.Text) || string.IsNullOrWhiteSpace(PassEntry.Text))
        {
            await DisplayAlert("Error", "Please fill in all fields.", "OK");
            return;
        }

        if (PassEntry.Text != ConfirmEntry.Text)
        {
            await DisplayAlert("Error", "Passwords do not match.", "OK");
            return;
        }

        if (!TermsCheckBox.IsChecked)
        {
            await DisplayAlert("Notice", "Please accept the Terms.", "OK");
            return;
        }

        try
        {
            // 2. Attempt Firebase Save
            await _service.RegisterUser(NameEntry.Text, IdEntry.Text, EmailEntry.Text, PassEntry.Text);

            await DisplayAlert("Success", "Account created!", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            // 3. THIS WILL SHOW YOU THE ACTUAL ERROR
            await DisplayAlert("Firebase Error", ex.Message, "OK");
        }
    }

    private async void OnLoginTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}