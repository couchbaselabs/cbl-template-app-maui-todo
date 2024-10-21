using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RealmTodo.Services;
using RealmTodo.ViewModels;
using RealmTodo.Views;

namespace RealmTodo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        //add services
        builder.Services.AddSingleton<IDatabaseService, CouchbaseService>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        
        builder.Services.AddTransient <LoginViewModel>();
        builder.Services.AddTransient <LoginPage>(); 
        
        builder.Services.AddTransient <ItemsViewModel>();
        builder.Services.AddTransient <ItemsPage>();
        
        builder.Services.AddTransient <EditItemViewModel>();
        builder.Services.AddTransient <EditItemPage>();
   
        return builder.Build();
    }
}

