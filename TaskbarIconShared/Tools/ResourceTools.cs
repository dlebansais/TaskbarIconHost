namespace TaskbarIconHost
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Contracts;

    /// <summary>
    /// Misc tools.
    /// </summary>
    public static class ResourceTools
    {
        /// <summary>
        /// Returns a <see cref="ImageSource"/> object that can be used as the icon for a WPF window.
        /// </summary>
        /// <param name="iconName">The name of the icon "Embedded Resource" file in the project.</param>
        /// <returns>The <see cref="ImageSource"/>.</returns>
        public static ImageSource LoadEmbeddedIcon(string iconName)
        {
            Assembly ResourceAssembly = Assembly.GetCallingAssembly();
            string ResourceName = GetResourceName(ResourceAssembly, iconName);
            using Stream ResourceStream = ResourceAssembly.GetManifestResourceStream(ResourceName);

            // Decode the icon from the stream and set the first frame to the BitmapSource
            BitmapDecoder decoder = IconBitmapDecoder.Create(ResourceStream, BitmapCreateOptions.None, BitmapCacheOption.None);
            ImageSource Result = decoder.Frames[0];

            return Result;
        }

        /// <summary>
        /// Returns an object loaded from embedded resources.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="name">The name of the "Embedded Resource" in the project.</param>
        /// <returns>The loaded resource.</returns>
        public static T LoadEmbeddedResource<T>(string name)
        {
            Assembly ResourceAssembly = Assembly.GetCallingAssembly();
            string ResourceName = GetResourceName(ResourceAssembly, name);
            using Stream ResourceStream = ResourceAssembly.GetManifestResourceStream(ResourceName);

            T Result = (T)Activator.CreateInstance(typeof(T), ResourceStream);

            return Result;
        }

        /// <summary>
        /// Returns the full resource name from the resource name without path.
        /// </summary>
        /// <param name="resourceAssembly">Assembly where to look for the resource.</param>
        /// <param name="name">The name of the "Embedded Resource" in the project.</param>
        /// <returns>The full resource name.</returns>
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

        /// <summary>
        /// Returns an object loaded from embedded resources.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="embeddedAssemblyName">Name of the embedded assembly.</param>
        /// <param name="resourceName">Name of the "Embedded Resource" in the project.</param>
        /// <param name="result">The loaded resource.</param>
        /// <returns>True if the resource was found; otherwise, false.</returns>
        public static bool LoadEmbeddedResource<T>(string embeddedAssemblyName, string resourceName, out T result)
            where T : class
        {
            if (DecompressedAssembly == null)
                DecompressedAssembly = LoadEmbeddedAssemblyStream(embeddedAssemblyName);

            string ResourcePath = string.Empty;

            // Loads an "Embedded Resource" of type T (ex: Bitmap for a PNG file).
            // Make sure the resource is tagged as such in the resource properties.
            string[] ResourceNames = DecompressedAssembly.GetManifestResourceNames();
            foreach (string Item in ResourceNames)
                if (Item.EndsWith(resourceName, StringComparison.InvariantCulture))
                {
                    ResourcePath = Item;
                    break;
                }

            // If not found, it could be because it's not tagged as "Embedded Resource".
            if (ResourcePath.Length > 0)
            {
                using Stream ResourceStream = DecompressedAssembly.GetManifestResourceStream(ResourcePath);

                result = (T)Activator.CreateInstance(typeof(T), ResourceStream);
                return true;
            }
            else
            {
                Contract.Unused(out result);
                return false;
            }
        }

        private static Assembly LoadEmbeddedAssemblyStream(string embeddedAssemblyName)
        {
            Assembly assembly = Assembly.GetEntryAssembly();

            string EmbeddedAssemblyResourcePath = $"costura.{embeddedAssemblyName}.dll.compressed";
#pragma warning disable CA1308 // Normalize strings to uppercase
            EmbeddedAssemblyResourcePath = EmbeddedAssemblyResourcePath.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            using Stream CompressedStream = assembly.GetManifestResourceStream(EmbeddedAssemblyResourcePath);
            using Stream UncompressedStream = new DeflateStream(CompressedStream, CompressionMode.Decompress);
            using MemoryStream TemporaryStream = new MemoryStream();

            int Count;
            var Buffer = new byte[81920];
            while ((Count = UncompressedStream.Read(Buffer, 0, Buffer.Length)) != 0)
                TemporaryStream.Write(Buffer, 0, Count);

            TemporaryStream.Position = 0;

            byte[] array = new byte[TemporaryStream.Length];
            TemporaryStream.Read(array, 0, array.Length);

            return Assembly.Load(array);
        }

        private static Assembly? DecompressedAssembly;
    }
}
