using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealmTodo.Services;
using RealmTodo.Views;

namespace RealmTodo.ViewModels
{
    public partial class LoginViewModel 
        : BaseViewModel
    {
        private readonly IDatabaseService databaseService;

        [ObservableProperty]
        private string email; 

        [ObservableProperty]
        private string password; 
        
        public ICommand FillDefaultCredentialsCommand { get; init; } 

        public LoginViewModel(IDatabaseService databaseService)
        {
            this.databaseService = databaseService;
            this.FillDefaultCredentialsCommand = new RelayCommand(FillDefaultCredentials);
        }
        
        [RelayCommand]
        public async Task OnAppearing()
        {
            await databaseService.Init(); 

            if (databaseService.CurrentUser != null)
            {
                GoToMainPage();
            }
        }
        
        private void FillDefaultCredentials()
        {
            Email = "demo1@example.com";
            Password = "P@ssw0rd12";
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
                await DialogService.ShowAlertAsync("Login failed", ex.Message, "Ok");
                IsBusy = false;
                return;
            }

            GoToMainPage();
        }

        private async Task<bool> VerifyEmailAndPassword()
        {
            if (!string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password)) return true;
            await DialogService.ShowAlertAsync("Error", "Please specify both the email and the password", "Ok");
            return false;

        }

        private void GoToMainPage()
        {
            App.Current.MainPage = new AppShell();
        }

    }
}

