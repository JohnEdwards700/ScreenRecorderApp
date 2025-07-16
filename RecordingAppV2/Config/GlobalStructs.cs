using System;

namespace RecordApp.Models
{
    public class RecordingCommand
    {
        public string Action { get; set; } = "";
        public string Type { get; set; } = "";
        public int Duration { get; set; } = 0;
        public string OutputPath { get; set; } = "";
        public string Quality { get; set; } = "medium";
    }

    public class RecordingStatus
    {
        public string Status { get; set; } = "idle";
        public string CurrentFile { get; set; } = "";
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string Error { get; set; } = "";
    }

    public class UploadedFile
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime UploadTime { get; set; }
        public long FileSize { get; set; }
    }

}
