// AppConfig.cs
using System;
using System.IO;
/*using System.Linq;*/ // Required for .Any() and .Contains()

/// <summary>
/// Represents the configuration settings for the application,
/// </summary>
namespace RecordApp.Config
{
    internal class AppConfig
    {
        public string OutputDirectory { get; private set; }
        public string WebApiUrl { get; private set; }
        /*    public bool ShowHelp { get; private set; }
        */

        public AppConfig()
        {
            // Set default values.
            OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "recordings"); // Default output directory
            WebApiUrl = "http://localhost:5000"; // Default Web API URL
            /*        ShowHelp = false;
            */
        }

    }

}
