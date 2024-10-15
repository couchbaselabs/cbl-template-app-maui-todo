using System.Collections.ObjectModel;
using System.Security.AccessControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealmTodo.Data;
using RealmTodo.Models;
using RealmTodo.Services;
using RealmTodo.Views;

namespace RealmTodo.ViewModels
{
    public partial class ItemsViewModel(IDatabaseService couchbaseService, LoginPage loginPage)
        : BaseViewModel
    {
        [ObservableProperty] 
        private string connectionStatusIcon = "wifi_on.png";

        [ObservableProperty] 
        private bool isShowAllTasks;

        [ObservableProperty] 
        private ObservableCollection<Item> items = [];

        private bool isOnline = true;

        [RelayCommand]
        public void OnAppearing()
        {
            if (couchbaseService.CurrentUser != null)
            {
                couchbaseService.SetTaskLiveQuery(SubscriptionType.Mine, UpdateItems);
            }
            else
            {
                throw new InvalidOperationException("User is not logged in");
            }
        }

        [RelayCommand]
        public void Logout()
        {
            IsBusy = true;
            Items.Clear();
            Items = new ObservableCollection<Item>();
            couchbaseService.Logout();
            IsBusy = false;

            App.Current.MainPage = loginPage;
        }

        [RelayCommand]
        public async Task AddItem()
        {
            await Shell.Current.GoToAsync($"itemEdit");
        }

        [RelayCommand]
        public async Task ToggleItemComplete(Item? item)
        {
            if (item != null)
            {
                if (!await CheckItemOwnership(item))
                {
                    return;
                }

                couchbaseService.ToggleIsComplete(item);
            }
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
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                switch (resultItems)
                {
                    case InitialResults<Item> initialItems:
                        Items.Clear();
                        foreach (var item in initialItems.List)
                        {
                            Items.Add(item);
                        }
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
                            var index = Items.IndexOf(existingItem);
                            Items[index] = item;
                        }
                        OnPropertyChanged(nameof(Items));
                        break;
                    }
                }
            });
        }

        private async Task<bool> CheckItemOwnership(Item? item)
        {
            if (item != null)
            {
                if (item.IsMine) return true;
                await DialogService.ShowAlertAsync("Error", "You cannot modify items not belonging to you", "OK");
                return false;
            }
            return true;
        }

        async partial void OnIsShowAllTasksChanged(bool value)
        {
            var status =  value ? SubscriptionType.All : SubscriptionType.Mine;
            couchbaseService.SetTaskLiveQuery(status, UpdateItems);
        }
    }
}