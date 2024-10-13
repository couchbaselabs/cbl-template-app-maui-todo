using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using RealmTodo.Services;
using RealmTodo.ViewModels;

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
        
        //add view models
        builder.Services.AddTransient <EditItemViewModel>();
        builder.Services.AddTransient <ItemsViewModel>();
        builder.Services.AddTransient <LoginViewModel>();
        
        return builder.Build();
    }
}

