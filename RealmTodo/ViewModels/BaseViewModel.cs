using CommunityToolkit.Mvvm.ComponentModel;

namespace RealmTodo.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        protected bool isBusy;

        protected Action? CurrentDismissAction;

        partial void OnIsBusyChanged(bool value)
        {
            if (value)
            {
                CurrentDismissAction = Services.DialogService.ShowActivityIndicator();
            }
            else
            {
                CurrentDismissAction?.Invoke();
                CurrentDismissAction = null;
            }
        }

    }
}

