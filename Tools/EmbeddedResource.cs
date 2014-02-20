using System;
using System.IO;
using System.Reflection;

namespace ClassLibrary1.Tools
{
    class EmbeddedResource
    {
        public static void Extract(string resourceName, string destinationFile)
        {
            var inputStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(EmbeddedResource), resourceName);
            if (inputStream == null) throw new ArgumentException(string.Format("Invalid resource name '{0}'.", resourceName), "resourceName");
            using (inputStream)
            using (var outputStream = File.Create(destinationFile))
            {
                inputStream.CopyTo(outputStream);
            }

        }
    }
}
