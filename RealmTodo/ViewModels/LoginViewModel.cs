using CommunityToolkit.Mvvm.Input;
using RealmTodo.Services;

namespace RealmTodo.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        public string Email { get; set; } = "";

        public string Password { get; set; } = "";

        [RelayCommand]
        public async Task OnAppearing()
        {
            await CouchbaseService.Init();

            if (CouchbaseService.CurrentUser != null)
            {
                await GoToMainPage();
            }
        }

        [RelayCommand]
        public async Task Login()
        {
            if (!await VeryifyEmailAndPassword())
            {
                return;
            }

            await DoLogin();
        }

        private async Task DoLogin()
        {
            try
            {
                IsBusy = true;
                await CouchbaseService.LoginAsync(Email, Password);
                IsBusy = false;
            }
            catch (Exception ex)
            {
                IsBusy = false;
                await DialogService.ShowAlertAsync("Login failed", ex.Message, "Ok");
                return;
            }

            await GoToMainPage();
        }

        private async Task<bool> VeryifyEmailAndPassword()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                await DialogService.ShowAlertAsync("Error", "Please specify both the email and the password", "Ok");
                return false;
            }

            return true;
        }

        private async Task GoToMainPage()
        {
            await Shell.Current.GoToAsync($"//items");
        }

    }
}

