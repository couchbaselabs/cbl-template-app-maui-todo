using Android.App;
using Android.Runtime;

namespace RealmTodo;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		Couchbase.Lite.Support.Droid.Activate(this);
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

