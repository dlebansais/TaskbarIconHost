using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskbarIconHost
{
    /// <summary>
    /// Misc tools.
    /// </summary>
    public static class ResourceTools
    {
        /// <summary>
        /// Returns an ImageSource object that can be used as the icon for a WPF window.
        /// </summary>
        /// <param name="iconName">The name of the icon "Embedded Resource" file in the project</param>
        /// <returns></returns>
        public static ImageSource LoadEmbeddedIcon(string iconName)
        {
            Assembly assembly = Assembly.GetCallingAssembly();

            foreach (string ResourceName in assembly.GetManifestResourceNames())
                if (ResourceName.EndsWith(iconName))
                {
                    using (Stream rs = assembly.GetManifestResourceStream(ResourceName))
                    {
                        //Decode the icon from the stream and set the first frame to the BitmapSource
                        BitmapDecoder decoder = IconBitmapDecoder.Create(rs, BitmapCreateOptions.None, BitmapCacheOption.None);
                        ImageSource Result = decoder.Frames[0];

                        return Result;
                    }
                }

            return null;
        }
    }
}
