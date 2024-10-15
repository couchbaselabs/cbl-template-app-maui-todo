# Conversion Example of MongoDb Atlas Device Sync to Couchbase Lite for DotNet Maui Developers 

The original version of this [application](https://github.com/mongodb/template-app-maui-todo)  was built with the [MongoDb Atlas Device SDK](https://www.mongodb.com/docs/atlas/device-sdks/sdk/dotnet/) and [Atlas Device Sync](https://www.mongodb.com/docs/atlas/app-services/sync/). 

> **NOTE**
>The original application is a basic To Do list.  The original source code has it's own opinionated way of implementing an DotNet Maui application and communicating between different layers.  This conversion is by no means a best practice for Maui development or a show case on how to properly communicate between layers of an application.  It's more of an example of the process that a developer will have to go through to convert an application from one SDK to another.
>

Some minior UI changes were made to remove wording about Realm and replaced with Couchbase.

# Capella Configuration

Before running this application, you must have [Couchbase Capella App Services](https://docs.couchbase.com/cloud/get-started/configuring-app-services.html) set up.  Instructions for setting up Couchbase Capella App Services and updating the configuration file can be found in the [Capella.md](./Capella.md) file in this repository.  Please ensure you complete these steps first.

# App Overview
The following diagram shows the flow of the application

![App Flow](dotnet-todo-app-flow.png)

# Maui App Conversion

The following is information on the application conversion process and what files were changed.

## Nuget Changes

The application was initially built using .NET 7.0 and the .NET MAUI workload for that version. It has since been upgraded to .NET 8 and the corresponding .NET MAUI workload to take advantage of Microsoft’s long-term support (LTS) for this release.

The Realm SDK Nuget packages were removed from the project.  The [Couchbase Lite Nuget package](https://docs.couchbase.com/couchbase-lite/current/csharp/gs-install.html) was added to the [RealmToDo.csproj](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/RealmTodo.csproj#L48) file.  

```xml 
<ItemGroup>
  <PackageReference Include="Couchbase.Lite" Version="3.2.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="8.0.1" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  <PackageReference Include="CommunityToolkit.Maui" Version="9.1.0" />
 </ItemGroup>
```
## App Services Configuration File

The original source code had the configuration for Atlas App Services stored in the atlasConfig.json file located projects root folder.  This file was removed and the configuration for Capella App Services was added in the [capellaConfig.json](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/capellaConfig.json#L2).

You will need to modify this file to add your Couchbase Capella App Services endpoint URL, as outlined in the [Capella setup instructions](./Capella.md).

## FodyWeavers Removal
FodyWeawvers is used for the Realm Atlas Device SDK for LINQ support.  This was removed from the project as it is not needed for Couchbase Lite.

## Android Registration for Couchbase Lite
The Couchbase Lite SDK requires [registration of the SDK for Android](https://docs.couchbase.com/couchbase-lite/current/csharp/gs-install.html#activating-on-android-platform-only).  The [MainApplication](https://github.com/mongodb/template-app-maui-todo/blob/main/RealmTodo/Platforms/Android/MainApplication.cs) class was modified to register the Couchbase Lite SDK.

```csharp

```


## Changes to Services 

### Registering of Interfaces, Services, ViewModels, and Views

The original source code relied on static classes for accessing services, which made testing challenging. To improve testability, the standard Dependency Injection (DI) pattern available in .NET MAUI was adopted.  The [MauiProgram.cs](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/MauiProgram.cs#L27) file was updated to register all services, view models, and views with the DI container using the builder.

```csharp
builder.Services.AddSingleton<IDatabaseService, CouchbaseService>();
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
       
//add view models
builder.Services.AddTransient <EditItemViewModel>();
builder.Services.AddTransient <ItemsViewModel>();
builder.Services.AddTransient <LoginViewModel>();
        
//add the views
builder.Services.AddTransient <EditItemPage>();
builder.Services.AddTransient <ItemsPage>();
builder.Services.AddTransient <LoginPage>();
```

### Authentication

The original source code uses the [Realms.Sync.App](https://github.com/mongodb/template-app-maui-todo/blob/main/RealmTodo/Services/RealmService.cs#L12C24-L12C39) to [handle authentication](https://github.com/mongodb/template-app-maui-todo/blob/main/RealmTodo/Services/RealmService.cs#L75).  

The [Couchbase Lite SDK](https://docs.couchbase.com/couchbase-lite/current/android/replication.html#lbl-user-auth)  manages authentication differently than the [Mongo Realm SDK](https://www.mongodb.com/docs/atlas/device-sdks/sdk/dotnet/manage-users/authenticate/).  Code was added to deal with these differences. 

### Handling Authencation of the App

The authentication of the app is called from the [IAuthenticationService](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/IAuthenticationService.cs#L5) interface.  The implementation [AuthenticationService](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/AuthenticationService.cs#L6) was added to handle the authentication of the app. 

 Authentication is done via the Couchbase Capella App Services Endpoint public [REST API](https://docs.couchbase.com/cloud/app-services/references/rest_api_admin.html) in the CouchbaseService [LoginAsync method](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/CouchbaseService.cs#L249) to resolve the SDK differences between Realm SDK and Couchbase Lite SDK without having to refactor large chunks of code. 


> **NOTE**
>Registering new users is out of scope of the conversion, so this functionaliy was removed.  Capella App Services allows the creating of Users per endpoint via the [UI](https://docs.couchbase.com/cloud/app-services/user-management/create-user.html#usermanagement/create-app-role.adoc) or the [REST API](https://docs.couchbase.com/cloud/app-services/references/rest_api_admin.html).  For large scale applications it's highly recommended to use a 3rd party [OpendID Connect](https://docs.couchbase.com/cloud/app-services/user-management/set-up-authentication-provider.html) provider. 
>

### Create User Model

The Couchbase Lite SDK doesn't provide a user object for tracking the authenticated user, so a [new model](https://github.com/couchbaselabs/cbl-realm-template-app-kotlin-todo/blob/main/app/src/main/java/com/mongodb/app/domain/User.kt) was created. 

### Updating Item Domain Model

The [Item](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Models/Item.cs#L8) class was modified to remove the Realm annotations and to refactor some properties to meet standard .NET naming conventions.

The .NET serialization library makes it easy to convert the item class to a JSON string for storage in Couchbase Lite, so changes were made to the class to make it serializable by the .NET serialization library.

Some fields need to be "Observable" in order for the UI in the application to dynamically update if changes are detected.  Those fields the SetProperty and OnPropertyChanged methods were implemented from the [MVVM toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/). 


### IDatabaseService Interface

The original source code had a [RealmService](https://github.com/mongodb/template-app-maui-todo/blob/main/RealmTodo/Services/RealmService.cs#L8) class that was used to interact with the Realm Atlas Device SDK and was a static class.  This was replaced with an [IDatabaseService](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/IDatabaseService.cs#L6) interface and a [CouchbaseService](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/CouchbaseService.cs) class that implements the interface.  The `CouchbaseService` class is used to interact with the Couchbase Lite SDK. 

### Implementation of CouchbaseService 
A heavy amount of code was refactored to implement the [CouchbaseService](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/Services/CouchbaseService.cs) from the original [RealmService](https://github.com/mongodb/template-app-maui-todo/blob/main/RealmTodo/Services/RealmService.cs).  The highlights of those changes are as follows. 

### Init Method 

The Init method was updated to start Couchbase logging and to read in the file from the `capellaConfig.json` file.

```csharp
Database.Log.Console.Level = LogLevel.Debug;
Database.Log.Console.Domains = LogDomain.All;

//get the config from disk
await using var fileStream = await FileSystem.Current.OpenAppPackageFileAsync("capellaConfig.json");
using StreamReader reader = new(fileStream);
var fileContent = await reader.ReadToEndAsync();
AppConfig = JsonSerializer.Deserialize<CouchbaseAppConfig>(
  fileContent,
  new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
);
```
> **NOTE**
> For more information on logging in Couchbase Lite, please review the [Documentation](https://docs.couchbase.com/couchbase-lite/current/csharp/troubleshooting-logs.html).
