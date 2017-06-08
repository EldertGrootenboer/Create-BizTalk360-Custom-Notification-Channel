using System;
using System.IO;
using System.Reflection;

namespace ServiceBusNotificationChannel
{
    /// <summary>
    /// Helpers for working with the notification channel.
    /// </summary>
    internal class Helper
    {
        /// <summary>
        /// Get contents of the property files.
        /// </summary>
        internal static string GetResourceFileContent(string filename)
        {
            // Get assembly
            var assembly = Assembly.GetExecutingAssembly();

            // Get assembly name
            var name = assembly.GetName().Name;

            // Get property file from the assembly resources
            using (var stream = assembly.GetManifestResourceStream(name + "." + filename))
            {
                // Check if property file could be found
                if (stream == null)
                {
                    throw new Exception(string.Format("Cannot read {0} make sure the file exists and it's an embeded resource", filename));
                }

                // Read contents from the property file
                using (var streamReader = new StreamReader(stream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }
}
