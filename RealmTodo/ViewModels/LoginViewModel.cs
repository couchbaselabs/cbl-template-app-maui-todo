using CommunityToolkit.Mvvm.Input;
using RealmTodo.Services;

namespace RealmTodo.ViewModels
{
    public partial class LoginViewModel(IDatabaseService databaseService) 
        : BaseViewModel
    {
        public string Email { get; set; } = "";

        public string Password { get; set; } = "";

        [RelayCommand]
        public async Task OnAppearing()
        {
            await databaseService.Init(); 

            if (databaseService.CurrentUser != null)
            {
                await GoToMainPage();
            }
        }

        [RelayCommand]
        public async Task Login()
        {
            if (!await VerifyEmailAndPassword())
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
                await databaseService.LoginAsync(Email, Password);
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

        private async Task<bool> VerifyEmailAndPassword()
        {
            if (!string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password)) return true;
            await DialogService.ShowAlertAsync("Error", "Please specify both the email and the password", "Ok");
            return false;

        }

        private async Task GoToMainPage()
        {
            await Shell.Current.GoToAsync($"//items");
        }

    }
}

