using RealmTodo.Services;
using RealmTodo.Views;

namespace RealmTodo;

public partial class App : Application
{
    public App(LoginPage page)
    {
        InitializeComponent();

        MainPage = page;
    }
}