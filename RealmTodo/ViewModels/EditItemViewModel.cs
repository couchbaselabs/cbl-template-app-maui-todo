using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RealmTodo.Models;
using RealmTodo.Services;

namespace RealmTodo.ViewModels
{
    public partial class EditItemViewModel(IDatabaseService databaseService)
        : BaseViewModel, IQueryAttributable
    {
        [ObservableProperty] private Item? initialItem;

        [ObservableProperty] private string summary;

        [ObservableProperty] private string pageHeader;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.Count > 0 && query.TryGetValue("item", out var value)) // we're editing an Item
            {
                InitialItem = value as Item;
                if (InitialItem != null)
                {
                    Summary = InitialItem.Summary;
                    PageHeader = $"Modify Item {InitialItem.Id}";
                }
                else
                {
                    throw new InvalidOperationException("Invalid item");
                }
            }
            else // we're creating a new item
            {
                Summary = "";
                PageHeader = "Create a New Item";
            }
        }

        [RelayCommand]
        public async Task SaveItem()
        {
            if (InitialItem != null) // editing an item
            {
                InitialItem.Summary = Summary;
                databaseService.AddTask(InitialItem);
            }
            else // creating a new item
            {
                if (databaseService.CurrentUser == null) throw new InvalidOperationException("User not logged in");
                var item = new Item()
                {
                    OwnerId = databaseService.CurrentUser.Username,
                    Summary = Summary
                };
                databaseService.AddTask(item);
            }
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}