// FFmpegUtils.cs
using System;
using System.Diagnostics;
using System.IO; // Required for Path.Combine, Directory.GetCurrentDirectory etc.

/// <summary>
/// Provides utility methods related to FFmpeg, such as checking its availability.
/// </summary>
internal static class FFmpegUtils
{
    /// <summary>
    /// Checks if the FFmpeg executable is available in the system's PATH.
    /// It does this by attempting to run 'ffmpeg -version' and checking the exit code.
    /// </summary>
    /// <returns>True if FFmpeg is found and executable, false otherwise.</returns>
    public static bool IsFFmpegAvailable()
    {
        try
        {
            // Create a ProcessStartInfo to run FFmpeg with the -version argument.
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",                // The executable name to look for in PATH.
                Arguments = "-version",             // Argument to get FFmpeg version information.
                UseShellExecute = false,            // Do not use the shell; directly execute the command.
                RedirectStandardOutput = true,      // Redirect output to capture version info (not read here, but good practice).
                CreateNoWindow = true               // Do not open a console window for this check.
            };

            // Start the FFmpeg process.
            using (var process = Process.Start(processStartInfo))
            {
                /*Console.WriteLine("This is the Process:", process);*/
                // Ensure the process actually started.
                if (process == null) return false;

                // Wait for the process to exit, with a timeout of 5 seconds.
                // Return true if the process started successfully, exited within the timeout,
                // and had an exit code of 0 (success).
                return process.WaitForExit(5000) && process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            // Any exception (e.g., file not found, permission denied, or other issues)
            // indicates that FFmpeg is not available or callable in the expected manner.
            Console.WriteLine($"Error checking FFmpeg availability: {ex.Message}");
            return false;
        }
    }
}
