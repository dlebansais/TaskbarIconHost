# Taskbar Icon Host
Host for plugins that want to be on the Windows 10 taskbar. 

# Using the program
Copy binaries from the latest release [here](https://github.com/dlebansais/TaskbarIconHost/releases) in a directory, then run TaskbarIconHost.exe. This will load plugins copied in the same directory, and if any create a little icon in the task bar where all plugins can interact with the user.

Right-click the icon to pop a menu with the following items:

- Load at startup. When checked, the application is loaded when a user logs in.
- Plugins menus (if there is only one, it is accessible directly, otherwise each of them has its own menu)
- Exit

# Known plugins
Here is a non exhaustive list of plugins:

- [Insta-Unblock](https://github.com/dlebansais/Insta-Unblock): automatically unblocks downloaded files.  
- [Kill-Update](https://github.com/dlebansais/Kill-Update): prevents Windows 10 from upgrading, except when its convenient.  
- [PgMoon](https://github.com/dlebansais/PgMoon): displays informations related to moon phases in the [Project: Gorgon](https://projectgorgon.com/) MMORPG.  
- [PgMessenger](https://github.com/dlebansais/PgMessenger): displays the global and guild chat in *Project: Gorgon* even when offline.  

# Creating your own plugin
1. Create a C# class library project with Visual Studio, and add a reference to the TaskbarIconShared assembly.
2. Create a class that inherits from IPluginClient and implement the interface.
3. Copy your assembly in the same folder as TaskbarIconHost.exe (there is no need to copy TaskbarIconShared.dll)
4. Restart TaskbarIconHost.exe and your plugin to be detected. If you make changes, you must exit and restart it to be able to replace the plugin with your modified binary. 

Note that you can have multiple plugins in the same assembly, as all types that inherit from IPluginClient will be instanced.

Look at plugins listed in the [Known plugins](#Known-plugins) section for source code examples. Also, read the documentation.

# Certification
This program is digitally signed with a [CAcert](https://www.cacert.org/) certificate.
