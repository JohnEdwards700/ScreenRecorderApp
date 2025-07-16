
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RecordApp.Config;


/// <summary>
/// Manages screen and audio recording and screenshots using FFmpeg.
/// This class encapsulates all direct interactions with the FFmpeg executable.
/// </summary>
namespace RecordApp.FFmpegRecorder
{
    internal class FFmpegRecorder
    {
        private readonly string _outputDirectory = new AppConfig().OutputDirectory;
        private Process? _currentFFmpegProcess; // Stores the active FFmpeg process for recording.
        private string? _currentOutputFile; // Stores the current output file path for recording.

        /// <summary>
        /// Gets a value indicating whether a recording is currently in progress.
        /// </summary>
        public bool IsRecording => _currentFFmpegProcess != null && !_currentFFmpegProcess.HasExited;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegRecorder"/> class.
        /// </summary>
        /// <param name="outputDirectory">The directory where recorded files will be saved.</param>
        public FFmpegRecorder(string outputDirectory)
        {
            // Ensure the output directory exists.
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            else 
            {
                _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            }
        }

        /// <summary>
        /// Starts a screen recording using FFmpeg.
        /// </summary>
        /// <param name="duration">Duration of recording in seconds (0 for indefinite).</param>
        /// <param name="quality">Video quality setting ('low', 'medium', 'high').</param>
        /// <returns>The full path of the output file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a recording is already in progress.</exception>
        public async Task<string> StartScreenRecordingAsync(int duration, string quality)
        {
            if (IsRecording) throw new InvalidOperationException("Recording already in progress.");

            var outputFile = Path.Combine(_outputDirectory, $"screen_recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            var args = BuildFFmpegScreenRecordingArguments(duration, quality, outputFile);

            _currentFFmpegProcess = StartFFmpegProcess(args);
            // For indefinite recordings, the process runs until explicitly stopped.
            // For fixed duration, FFmpeg will exit on its own.
            if (duration > 0)
            {
                // Await the process completion for fixed duration recordings
                await _currentFFmpegProcess.WaitForExitAsync();
                _currentFFmpegProcess = null; // Clear process reference after completion
            }
            return outputFile;
        }

        /// <summary>
        /// Starts an audio recording using FFmpeg.
        /// </summary>
        /// <param name="duration">Duration of recording in seconds (0 for indefinite).</param>
        /// <param name="quality">Audio quality setting ('low', 'medium', 'high').</param>
        /// <param name="deviceName">Optional: Specific audio input device name or index.</param>
        /// <returns>The full path of the output file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a recording is already in progress.</exception>
        public async Task<string> StartAudioRecordingAsync(int duration, string quality, string? deviceName)
        {
            if (IsRecording) throw new InvalidOperationException("Recording already in progress.");

            var outputFile = Path.Combine(_outputDirectory, $"audio_recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
            var args = BuildFFmpegAudioRecordingArguments(duration, quality, deviceName, outputFile);

            _currentFFmpegProcess = StartFFmpegProcess(args);
            if (duration > 0)
            {
                await _currentFFmpegProcess.WaitForExitAsync();
                _currentFFmpegProcess = null;
            }
            return outputFile;
        }

        /// <summary>
        /// Starts a combined screen and audio recording using FFmpeg.
        /// </summary>
        /// <param name="duration">Duration of recording in seconds (0 for indefinite).</param>
        /// <param name="videoQuality">Video quality setting ('low', 'medium', 'high').</param>
        /// <param name="audioQuality">Audio quality setting ('low', 'medium', 'high').</param>
        /// <param name="deviceName">Optional: Specific audio input device name or index.</param>
        /// <returns>The full path of the output file.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a recording is already in progress.</exception>
        public async Task<string> StartCombinedRecordingAsync(string videoQuality , string audioQuality , string? deviceName, int duration = 0)
        {
            if (IsRecording) throw new InvalidOperationException("Recording already in progress.");
    
            if (duration > 0)
            {
                var outputFile = Path.Combine(_outputDirectory, $"combined_recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
                var args = BuildFFmpegCombinedRecordingArguments(duration, videoQuality, audioQuality, deviceName, outputFile);


                _currentFFmpegProcess = StartFFmpegProcess(args);
                await _currentFFmpegProcess.WaitForExitAsync();
                _currentFFmpegProcess = null;

                return outputFile;
            }
            else if (duration == 0)
            {

                // If duration is 0, we start the process and let it run indefinitely.
                var outputFile = Path.Combine(_outputDirectory, $"combined_recording_{DateTime.Now:yyyyMMdd_HHmmss}.mkv");
                var args = BuildFFmpegCombinedRecordingArguments(duration, videoQuality, audioQuality, deviceName, outputFile);
                _currentOutputFile = $"\"{outputFile}\""; // Store the output file path for later use
                _currentFFmpegProcess = StartFFmpegProcess(args);

                return _currentOutputFile; // Return the output file path immediately


                /*string convertedOutputFile = $"\"{outputFile}\"".Split('.')[0] + ".mp4"; // Convert to MP4 format
                *//*_currentFFmpegProcess = StartFFmpegProcess($"-i \"{outputFile}\" -c copy -y \"{convertedOutputFile}\"", redirectOutput: true, createNoWindow: true);*//*
                _currentFFmpegProcess = StartFFmpegProcess($"-i \"{outputFile}\" -c copy -y \"{convertedOutputFile}\"");
                await _currentFFmpegProcess.WaitForExitAsync(); // Wait for the conversion to complete
                _currentFFmpegProcess = null; // Clear the process reference after conversion*/

                /*return $"-i \"{mkvFile}\" -c copy -y \"{mp4File}\"";*/
            }
            else
            {
                throw new ArgumentException("Duration must be a non-negative integer.");
            }
            // Conversion command (separate FFmpeg call)

        }

        /// <summary>
        /// Stops any active FFmpeg recording process.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task StopRecordingAsync()
        {
            if (_currentFFmpegProcess != null && !IsRecording)
            {
                Console.WriteLine("Warning: FFmpeg process is not running or already stopped.");
                _currentFFmpegProcess = null; // Clear stale reference
                _currentOutputFile = null; // Clear output file reference
                return;
            }

            else if (_currentFFmpegProcess != null)
            {
                try
                {
                    try
                    {
                        _currentFFmpegProcess.StandardInput.WriteLine("q");

                        // Wait up to 5 seconds for graceful shutdown
                        var gracefulShutdown = _currentFFmpegProcess.WaitForExitAsync();
                        var timeoutTask = Task.Delay(5000);

                        if (await Task.WhenAny(gracefulShutdown, timeoutTask) == timeoutTask)
                        {
                            // Timeout - force kill
                            Console.WriteLine("Graceful shutdown timed out, force killing process...");
                            _currentFFmpegProcess.Kill();
                        }
                    }
                    catch
                    {
                        // If graceful shutdown fails, force kill
                        Console.WriteLine("Graceful shutdown failed, force killing process...");
                        _currentFFmpegProcess.Kill();
                    }
                    // FFmpeg usually handles termination gracefully with a 'q' or Ctrl+C signal.
                    // For a robust stop, we can try to send 'q' to its standard input,
                    // or just kill the process if it's not responding.
                    // For simplicity, directly killing the process is often effective for stopping recordings.
                    await _currentFFmpegProcess.WaitForExitAsync(); // Wait for it to fully exit.
                    _currentFFmpegProcess = null;
                    /*_currentFFmpegProcess = StartFFmpegProcess($"-i \"{outputFile}\" -c copy -y \"{convertedOutputFile}\"", redirectOutput: true, createNoWindow: true);*/
                    if (!string.IsNullOrEmpty(_currentOutputFile))
                    {

                        string convertedOutputFile = _currentOutputFile.Split('.')[0] + ".mp4"; // Convert to MP4 format
                        _currentFFmpegProcess = StartFFmpegProcess($"-i \"{_currentOutputFile}\" -c copy -y \"{convertedOutputFile}\"");
                        await _currentFFmpegProcess.WaitForExitAsync(); // Wait for the conversion to complete
                        _currentFFmpegProcess = null; // Clear the process reference after conversion
                        Console.WriteLine($"Recording Stopped and converted to: {_currentOutputFile}");
                    }
                    else
                    {
                        Console.WriteLine("Recording Stopped.");
                    }
                    _currentOutputFile = null; // Clear the output file reference
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error killing FFmpeg process: {ex.Message}");
                    // Potentially set _currentFFmpegProcess to null even on error to allow new recordings
                    _currentFFmpegProcess = null; // Clear stale reference
                    _currentOutputFile = null; // Clear output file reference
                    throw; // Re-throw to inform the caller
                }
            }
            else
            {
                Console.WriteLine("No FFmpeg recording process is active.");
            }
        }

        /// <summary>
        /// Takes a single screenshot using FFmpeg.
        /// </summary>
        /// <returns>The full path of the saved screenshot file.</returns>
        public async Task<string> TakeScreenshotAsync()
        {
            var outputFile = Path.Combine(_outputDirectory, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            // Arguments for taking a screenshot from the screen input.
            // -f gdigrab: Input format for screen grabbing on Windows.
            // -i desktop: Input from the entire desktop.
            // -vframes 1: Capture only 1 frame.
            // -q:v 2: Video quality (2 is generally good for images).
            // -y: Overwrite output file if it exists.
            var args = $"-f gdigrab -i desktop -vframes 1 -q:v 2 -y \"{outputFile}\"";

            using (var process = StartFFmpegProcess(args, redirectOutput: true))
            {
                await process.WaitForExitAsync(); // Wait for the screenshot process to complete.
                if (process.ExitCode != 0)
                {
                    var errorOutput = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"FFmpeg screenshot failed with exit code {process.ExitCode}: {errorOutput}");
                }
            }
            return outputFile;
        }

        /// <summary>
        /// Fetches a list of available audio input devices detected by FFmpeg.
        /// This uses the '-list_devices true' and '-f dshow' arguments for Windows DirectShow.
        /// </summary>
        /// <returns>A list of strings, where each string is a device name.</returns>
        public async Task<List<string>> GetAvailableAudioDevicesAsync()
        {
            // FFmpeg command to list DirectShow audio devices:
            // ffmpeg -list_devices true -f dshow -i dummy
            var args = "-list_devices true -f dshow -i dummy";
            var devices = new List<string>();

            using (var process = StartFFmpegProcess(args, redirectOutput: true, createNoWindow: true))
            {
                // Read FFmpeg's standard error output, which contains device information.
                var output = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse the output to extract device names. This parsing is OS-dependent.
                // Example output line: "[dshow @ 000002162621C1C0]   "Microphone Array (Realtek(R) Audio)" (audio)"
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("(audio)") && line.Contains("] \""))
                    {
                        // Extract the part between quotes, which is the device name.
                        var startIndex = line.IndexOf("] \"") + 3;
                        var endIndex = line.LastIndexOf("\" (audio)");
                        if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                        {
                            var deviceName = line.Substring(startIndex, endIndex - startIndex);
                            devices.Add(deviceName);
                        }
                    }
                }
            }
            return devices;
        }

        /// <summary>
        /// Starts an FFmpeg process with the given arguments.
        /// </summary>
        /// <param name="arguments">The command-line arguments to pass to FFmpeg.</param>
        /// <param name="redirectOutput">True to redirect standard output/error for reading, false otherwise.</param>
        /// <param name="createNoWindow">True to prevent creation of a new console window for the process.</param>
        /// <returns>The started <see cref="Process"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if FFmpeg process fails to start.</exception>
        private Process StartFFmpegProcess(string arguments, bool redirectOutput = false, bool createNoWindow = false)
        {
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput, // Often FFmpeg logs to stderr
                CreateNoWindow = createNoWindow
            };

            var process = new Process { StartInfo = startInfo };

            Console.WriteLine($"Executing FFmpeg: ffmpeg {arguments}"); // Log the command being run

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start FFmpeg process.");
            }

            return process;
        }

        /// <summary>
        /// Helper to build arguments for screen recording.
        /// </summary>
        private string BuildFFmpegScreenRecordingArguments(int duration, string quality, string outputFile)
        {
            string durationArg = duration > 0 ? $"-t {duration}" : "";
            string videoCodec = "";
            string pixelFormat = "";
            string crf = ""; // Constant Rate Factor for quality control

            switch (quality.ToLower())
            {
                case "low":
                    videoCodec = "libx264";
                    pixelFormat = "yuv420p"; // Broad compatibility
                    crf = "28"; // Higher CRF = lower quality, smaller file
                    break;
                case "medium":
                    videoCodec = "libx264";
                    pixelFormat = "yuv420p";
                    crf = "23"; // Good balance
                    break;
                case "high":
                    videoCodec = "libx264";
                    pixelFormat = "yuv420p";
                    crf = "18"; // Lower CRF = higher quality, larger file
                    break;
                default:
                    Console.WriteLine($"Warning: Unknown video quality '{quality}'. Using medium quality.");
                    videoCodec = "libx264";
                    pixelFormat = "yuv420p";
                    crf = "23";
                    break;
            }

            // -f gdigrab: Input format for screen grabbing (Windows)
            // -i desktop: Input from the entire desktop
            // -c:v {codec}: Video codec
            // -pix_fmt {format}: Pixel format
            // -crf {crf_value}: Quality setting
            // -preset veryfast: Encoding speed/compression ratio (veryfast is good balance)
            return $"-f gdigrab -i desktop {durationArg} -c:v {videoCodec} -pix_fmt {pixelFormat} -crf {crf} -preset veryfast \"{outputFile}\"";
        }

        /// <summary>
        /// Helper to build arguments for audio recording.
        /// </summary>
        private string BuildFFmpegAudioRecordingArguments(int duration, string quality, string? deviceName, string outputFile)
        {
            string durationArg = duration > 0 ? $"-t {duration}" : "";
            string audioCodec = "";
            string audioBitrate = "";

            switch (quality.ToLower())
            {
                case "low":
                    audioCodec = "libmp3lame";
                    audioBitrate = "64k";
                    break;
                case "medium":
                    audioCodec = "libmp3lame";
                    audioBitrate = "128k";
                    break;
                case "high":
                    audioCodec = "libmp3lame";
                    audioBitrate = "192k";
                    break;
                default:
                    Console.WriteLine($"Warning: Unknown audio quality '{quality}'. Using medium quality.");
                    audioCodec = "libmp3lame";
                    audioBitrate = "128k";
                    break;
            }

            // -f dshow: Input format for DirectShow devices (Windows)
            // -i audio="{device}": Specify audio input device
            // -acodec {codec}: Audio codec
            // -b:a {bitrate}: Audio bitrate
            // -y: Overwrite output file
            /*        string inputDevice = string.IsNullOrEmpty(deviceName) ? "audio=\"Microphone (Realtek(R) Audio)\"" : $"audio=\"{deviceName}\"";
            */
            string inputDevice = $"audio=\"{deviceName}\"";

            return $"-f dshow -i {inputDevice} {durationArg} -acodec {audioCodec} -b:a {audioBitrate} -y \"{outputFile}\"";
        }

        /// <summary>
        /// Helper to build arguments for combined screen and audio recording.
        /// </summary>
        private string BuildFFmpegCombinedRecordingArguments(int duration, string videoQuality, string audioQuality, string? deviceName, string outputFile)
        {
            Console.WriteLine("Device Name: " + deviceName);
            string durationArg = duration > 0 ? $" -t {duration}" : "";

            // Video settings - using more compatible options
            string videoCodec = "libx264";
            string videoPixelFormat = "yuv420p";
            string videoCrf = "23";
            /*switch (videoQuality.ToLower())
            {
                case "low": videoCodec = "libx264"; videoPixelFormat = "yuv420p"; videoCrf = "28"; break;
                case "medium": videoCodec = "libx264"; videoPixelFormat = "yuv420p"; videoCrf = "23"; break;
                case "high": videoCodec = "libx264"; videoPixelFormat = "yuv420p"; videoCrf = "18"; break;
                default:
                    Console.WriteLine($"Warning: Unknown video quality '{videoQuality}'. Using medium.");
                    videoCodec = "libx264"; videoPixelFormat = "yuv420p"; videoCrf = "23"; break;
            }*/

            // Audio settings - using more compatible codec
            string audioCodec = "aac";
            /*string audioCodec = "libmp3lame"; // MP3 codec for audio
            string audioCodec = "pcm_s16le"; // PCM codec for audio not used for mp4*/
            string audioBitrate = "128k"; // Default bitrate for audio
            /*string audioBitrate = audioQuality.ToLower() switch
            {
                "low" => "64k",
                "high" => "192k",
                "medium" or _ => "128k"
            };*/

            /*switch (audioQuality.ToLower())
            {
                case "low": audioCodec = "libmp3lame"; audioBitrate = "64k"; break;    // MP3 instead of AAC
                case "medium": audioCodec = "libmp3lame"; audioBitrate = "128k"; break;
                case "high": audioCodec = "libmp3lame"; audioBitrate = "192k"; break;
                default:
                    Console.WriteLine($"Warning: Unknown audio quality '{audioQuality}'. Using medium.");
                    audioCodec = "libmp3lame"; audioBitrate = "128k"; break;
            }*/

            /*        string inputDevice = string.IsNullOrEmpty(deviceName) ? "\"audio=Microphone Array (AMD Audio Device)\"" : $"\"audio={deviceName}\"";
            */
            string inputDevice = $"\"audio={deviceName}\"";
            return $"-f gdigrab -framerate 30 -i desktop " +
              $"-f dshow -i {inputDevice}{durationArg} " +
              $"-c:v {videoCodec} -pix_fmt {videoPixelFormat} -crf {videoCrf} -preset veryfast " +
              $"-c:a {audioCodec} -b:a {audioBitrate} -movflags +faststart -y \"{outputFile}\"";
        }

    }
}
