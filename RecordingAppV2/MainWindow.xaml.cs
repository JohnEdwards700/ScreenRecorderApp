using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using RecordApp.FFmpegRecorder;
using RecordApp.Config;
using RecordApp.Models;
using System.Linq.Expressions;

namespace RecordingAppV2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /*readonly string config = new AppConfig().OutputDirectory;*/
        private readonly FFmpegRecorder _recorder;
        public MainWindow()
        {
            InitializeComponent();
            var config = new AppConfig();
            // configuration for the app
            _recorder = new FFmpegRecorder(config.OutputDirectory);
            // recorder to use from gui
            this.Topmost = true;
        }

        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            await _recorder.TakeScreenshotAsync();
            Console.WriteLine("Screenshot taken");
            /*var api = new ApiService();
            try
            {
                Console.WriteLine("Taking screenshot...");
                string response = await api.TakeScreenShot();
                Console.WriteLine(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }*/
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            /*var api = new ApiService();*/
            var listOfAudioDevices = await _recorder.GetAvailableAudioDevicesAsync();
            var defaultAudioDevice = listOfAudioDevices.FirstOrDefault();
            // When the console is on:
            //  On click the record button, it should start recording the screen and audio.
            //  The button should change to "Stop Recording" or just "Stop".
            var button = sender as Button;
            if (button != null)
            {
                if (button.Content.ToString() == "Rec")
                {
                    await _recorder.StartCombinedRecordingAsync("low", "low", defaultAudioDevice, 0); //default settings
                    button.Content = "Stop";
            
                }
                else
                {
                    await _recorder.StopRecordingAsync(); // Stop the recording
                    button.Content = "Rec";
                }
            }
        }

        private async void StartWebApiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("Starting Web API server...");
                var button = sender as Button;
                if (button.Visibility == Visibility.Collapsed)
                {
                    MessageBox.Show("Web API server is already running.");
                    return;
                }

                var config = new AppConfig();
                var apiServer = new RecorderService(config.OutputDirectory, config.WebApiUrl);
                button.Visibility = Visibility.Collapsed;
                await apiServer.StartAsync(CancellationToken.None);
                Console.WriteLine("Web API server started.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

    }

    /*public static class LocalApiServer // Unused Server Class
    {
        public static async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();

            var app = builder.Build();

            app.MapGet("/api/status", () => Results.Json(new { status = "ok" }));

            app.MapPost("/api/record", async (HttpContext context) =>
            {
                var request = await context.Request.ReadFromJsonAsync<RecordingCommand>();
                // You can call your FFmpegRecorder class here
                var action = request?.Action.ToLower();
                switch (action):
                {
                    case "start":
                        // Call start recording method
                        Console.WriteLine("Starting recording...");
                        break;
                    case "stop":
                        // Call stop recording method
                        Console.WriteLine("Stopping recording...");
                        break;
                    default:
                        Console.WriteLine($"Unknown action: {action}");
                        break;
                    }
                    Console.WriteLine($"Received command: {request?.Action}");

                    return Results.Json(new { message = "Command received" });
                });
            app.MapPost("/api/screenshot", async (HttpContext context) =>
            {
                var request = await context.Request.ReadFromJsonAsync<RecordingCommand>();
                // You can call your FFmpegRecorder class here
                Console.WriteLine($"Received command: {request?.Action}");

                return Results.Json(new { message = "Command received" });
            });

            await app.RunAsync("http://localhost:5000");
        }
    }


    public class ApiService // Unused Api Reciever
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://localhost:5000/api/");
        }
        public async Task<string> GetRecordingStatusAsync()
        {
            var response = await _httpClient.GetAsync("recording/current-status");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> StartRecordingAsync()
        {
            var command = new
            {
                action = "start",
                type = "combined",
                duration = 0,
                quality = "medium"
            };

            string json = JsonSerializer.Serialize(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("recording/command", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> StopRecordingAsync()
        {
            var command = new
            {
                action = "stop",
                type = "",
                duration = 0,
                quality = ""
            };

            string json = JsonSerializer.Serialize(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("recording/command", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> TakeScreenShot()
        {
            var command = new
            {
                action = "screenshot",
                type = "screen",
                duration = 0,
                quality = "medium"
            };

            string json = JsonSerializer.Serialize(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("recording/command", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> TestCall()
        {
            var data = new
            {
                message = "Hello, this is a test call from WPF application!"
            };
            string json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("recording/testcall", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
*/
}