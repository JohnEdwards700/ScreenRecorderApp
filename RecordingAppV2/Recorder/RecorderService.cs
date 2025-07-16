// This class is responsible for interacting with a web API to receive recording commands.
// It acts as a client to a hypothetical web application.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using RecordApp.FFmpegRecorder;
using RecordApp.Models;
using RecordApp.Config;



/// <summary>
/// Service that polls a Web API for recording commands and
/// dispatches them to the FFmpegRecorder.
/// This is used when the application runs in "api" mode.
/// </summary>
internal class RecorderService
{
    private readonly string _outputDirectory = new AppConfig().OutputDirectory;
    private readonly string _webApiUrl;
    private readonly HttpClient _httpClient;
    private readonly FFmpegRecorder _recorder;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecorderService"/> class.
    /// </summary>
    /// <param name="outputDirectory">The base directory for recordings.</param>
    /// <param name="webApiUrl">The URL of the Web API to poll for commands.</param>
    public RecorderService(string outputDirectory, string webApiUrl)
    {
        _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        _webApiUrl = webApiUrl ?? throw new ArgumentNullException(nameof(webApiUrl));
        _httpClient = new HttpClient();
        _recorder = new FFmpegRecorder(_outputDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Starts polling the Web API for recording commands at regular intervals.
    /// This method will run indefinitely until the cancellation token is signaled.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the polling.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listOfAudioDevices = await _recorder.GetAvailableAudioDevicesAsync();
        var defaultAudioDevice = listOfAudioDevices.FirstOrDefault();
        Console.WriteLine($"Starting API polling mode, polling {_webApiUrl}/api/recording/command every second...");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Make an HTTP GET request to the correct command endpoint
                /*                var response = await _httpClient.GetStringAsync($"{_webApiUrl}/api/recording/command");
                */
                var response = await _httpClient.GetStringAsync($"http://localhost:5000/api/recording/command");
                Console.WriteLine($"DEBUG: Received response from API: {response}");

                // Get the list of available audio devices
                Console.WriteLine($"DEBUG: Default audio device: {defaultAudioDevice}");


                // Parse the JSON response into a RecordingCommand object
                var command = JsonSerializer.Deserialize<RecordingCommand>(response, _jsonOptions);


                // Only process if there's an actual command
                if (!string.IsNullOrEmpty(command?.Action))
                {
                    Console.WriteLine($"Received command: {command.Action} {command.Type}");

                    await SendStatusAsync("processing");

                    switch (command.Action.ToLower())
                    {
                        case "start":
                            if (command.Type.ToLower() == "screen")
                            {
                                await _recorder.StartScreenRecordingAsync(command.Duration, command.Quality);
                                Console.WriteLine($"Started screen recording for {command.Duration} seconds with {command.Quality} quality.");
                            }
                            else if (command.Type.ToLower() == "audio")
                            {
                                await _recorder.StartAudioRecordingAsync(command.Duration, command.Quality, defaultAudioDevice);
                                Console.WriteLine($"Started audio recording for {command.Duration} seconds with {command.Quality} quality.");

                            }
                            else if (command.Type.ToLower() == "combined")
                            {
                                await _recorder.StartCombinedRecordingAsync(command.Quality, command.Quality, defaultAudioDevice, command.Duration);
                                Console.WriteLine($"Started screen and audio recording for {command.Duration} seconds with {command.Quality} quality.");
                            }
                            else
                            {
                                Console.WriteLine($"Unknown recording type: {command.Type}");
                                await SendStatusAsync($"error: Unknown recording type '{command.Type}'");
                                return;
                            }
                            break;

                        case "stop":
                            await _recorder.StopRecordingAsync();
                            Console.WriteLine("Recording stopped.");
                            break;

                        case "screenshot":
                            Console.WriteLine("DEBUG: Before Screenshot");
                            await _recorder.TakeScreenshotAsync();
                            Console.WriteLine("Screenshot taken.");
                            break;

                        default:
                            Console.WriteLine($"Unknown command: {command.Action}");
                            await SendStatusAsync($"error: Unknown command '{command.Action}'");
                            return;
                    }

                    await SendStatusAsync("idle");
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"API Error: Could not connect or retrieve commands from {_webApiUrl}. {httpEx.Message}");
                await SendStatusAsync($"error: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON Error: Could not parse command response. {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during API polling: {ex.Message}");
                await SendStatusAsync($"error: {ex.Message}");
            }

            // Wait for a short duration before polling again, respecting cancellation.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // This is expected when the cancellation token is signaled during the delay.
                break;
            }
        }
        Console.WriteLine("API polling stopped.");
    }

    private async Task SendStatusAsync(string status)
    {
        try
        {
            var statusObj = new RecordingStatus
            {
                Status = status,
                CurrentFile = _outputDirectory, //previously was _outputDirectory
                StartTime = DateTime.Now,
                Duration = TimeSpan.Zero
            };

            var json = JsonSerializer.Serialize(statusObj, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Use the correct status endpoint
            var response = await _httpClient.PostAsync($"{_webApiUrl}/api/recording/status", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Status '{status}' sent to API successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to send status. HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending status: {ex.Message}");
        }
    }

    public async Task UploadFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found for upload: {filePath}");
                return;
            }

            using var form = new MultipartFormDataContent();
            var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync($"{_webApiUrl}/api/recording/upload", form);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"File uploaded successfully: {Path.GetFileName(filePath)}");
            }
            else
            {
                Console.WriteLine($"Failed to upload file. HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
        }
    }



}

// Add the models if they're not defined elsewhere}