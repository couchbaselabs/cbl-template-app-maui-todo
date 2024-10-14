using RealmTodo.ViewModels;

namespace RealmTodo.Views;

public partial class ItemsPage : ContentPage
{
	public ItemsPage(ItemsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
