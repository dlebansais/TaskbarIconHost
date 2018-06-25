using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskbarIconHost
{
    public static class ResourceTools
    {
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
