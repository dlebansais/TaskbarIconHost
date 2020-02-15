using System.Runtime.CompilerServices;

namespace TaskbarIconHost
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Misc tools.
    /// </summary>
    public static class ResourceTools
    {
        /// <summary>
        /// Returns an ImageSource object that can be used as the icon for a WPF window.
        /// </summary>
        /// <param name="iconName">The name of the icon "Embedded Resource" file in the project</param>
        public static ImageSource LoadEmbeddedIcon(string iconName)
        {
            Assembly ResourceAssembly = Assembly.GetCallingAssembly();
            string ResourceName = GetResourceName(ResourceAssembly, iconName);
            using Stream ResourceStream = ResourceAssembly.GetManifestResourceStream(ResourceName);

            //Decode the icon from the stream and set the first frame to the BitmapSource
            BitmapDecoder decoder = IconBitmapDecoder.Create(ResourceStream, BitmapCreateOptions.None, BitmapCacheOption.None);
            ImageSource Result = decoder.Frames[0];

            return Result;
        }

        /// <summary>
        /// Returns an object of type <typeparam name="T"/> loaded from embedded resources.
        /// </summary>
        /// <param name="name">The name of the "Embedded Resource" in the project.</param>
        public static T LoadEmbeddedResource<T>(string name)
        {
            Assembly ResourceAssembly = Assembly.GetCallingAssembly();
            string ResourceName = GetResourceName(ResourceAssembly, name);
            using Stream ResourceStream = ResourceAssembly.GetManifestResourceStream(ResourceName);

            T Result = (T)Activator.CreateInstance(typeof(T), ResourceStream);

            return Result;
        }

        private static string GetResourceName(Assembly resourceAssembly, string name)
        {
            string ResourceName = string.Empty;

            // Loads an "Embedded Resource".
            string[] ResourceNames = resourceAssembly.GetManifestResourceNames();
            foreach (string Item in ResourceNames)
                if (Item.EndsWith(name, StringComparison.InvariantCulture))
                    ResourceName = Item;

            return ResourceName;
        }
    }
}
