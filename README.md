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

The original version of the application was based on .NET version 7.0 and the Maui Workload for .NET 7.0.  The app was upgraded to .NET 8 and the Maui workload for .NET 8 because this release is under long-term support from Microsoft (LTS).  

The [Couchbase Lite Nuget package](https://docs.couchbase.com/couchbase-lite/current/csharp/gs-install.html) was added to the [RealmToDo.csproj](https://github.com/couchbaselabs/cbl-template-app-maui-todo/blob/main/RealmTodo/RealmTodo.csproj#L48) file.  

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

## App Builder changes

