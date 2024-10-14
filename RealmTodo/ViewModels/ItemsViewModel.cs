using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealmTodo.Data;
using RealmTodo.Models;
using RealmTodo.Services;

namespace RealmTodo.ViewModels
{
    public partial class ItemsViewModel(IDatabaseService couchbaseService)
        : BaseViewModel
    {
        [ObservableProperty] 
        private string connectionStatusIcon = "wifi_on.png";

        [ObservableProperty] 
        private bool isShowAllTasks;

        [ObservableProperty] 
        private IList<Item> items = new List<Item>();

        private bool isOnline = true;

        [RelayCommand]
        public void OnAppearing()
        {
            if (couchbaseService.CurrentUser != null)
            {
                //setup live query
                couchbaseService.SetTaskLiveQuery(SubscriptionType.Mine, UpdateItems);
            }
            else
            {
                throw new InvalidOperationException("User is not logged in");
            }
        }

        [RelayCommand]
        public async Task Logout()
        {
            IsBusy = true;
            couchbaseService.Logout();
            IsBusy = false;

            await Shell.Current.GoToAsync($"//login");
        }

        [RelayCommand]
        public async Task AddItem()
        {
            await Shell.Current.GoToAsync($"itemEdit");
        }

        [RelayCommand]
        public async Task EditItem(Item item)
        {
            if (!await CheckItemOwnership(item))
            {
                return;
            }

            var itemParameter = new Dictionary<string, object>() { { "item", item } };
            await Shell.Current.GoToAsync($"itemEdit", itemParameter);
        }

        [RelayCommand]
        public async Task DeleteItem(Item item)
        {
            if (!await CheckItemOwnership(item))
            {
                return;
            }

            couchbaseService.DeleteTask(item);
        }

        [RelayCommand]
        public void ChangeConnectionStatus()
        {
            isOnline = !isOnline;

            if (isOnline)
            {
                couchbaseService.PauseSync();
            }
            else
            {
                couchbaseService.ResumeSync();
            }

            ConnectionStatusIcon = isOnline ? "wifi_on.png" : "wifi_off.png";
        }

        private void UpdateItems(IResultsChange<Item> resultItems)
        {
            switch (resultItems)
            {
                case InitialResults<Item> initialItems:
                    Items.Clear();
                    Items = initialItems.List;
                    break;
                case UpdatedResults<Item> updatedItems:
                {
                    foreach (var item in updatedItems.Insertions)
                    {
                        Items.Add(item);
                    }

                    foreach (var item in updatedItems.Deletions)
                    {
                        Items.Remove(item);
                    }

                    foreach (var item in updatedItems.Changes)
                    {
                    
                        var existingItem = Items.FirstOrDefault(i => i.Id == item.Id);
                        if (existingItem == null) continue;
                        Items.Remove(existingItem);
                        Items.Add(item);
                    }

                    break;
                }
            }
        }

        private async Task<bool> CheckItemOwnership(Item item)
        {
            if (item.IsMine) return true;
            await DialogService.ShowAlertAsync("Error", "You cannot modify items not belonging to you", "OK");
            return false;
        }

        async partial void OnIsShowAllTasksChanged(bool value)
        {
            var status =  value ? SubscriptionType.All : SubscriptionType.Mine;
            couchbaseService.SetTaskLiveQuery(status, UpdateItems);
        }
    }
}