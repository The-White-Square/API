namespace GameApp.Models
{
    public struct Time
    {
        public int Minutes { get; set; }
        public int Seconds { get; set; }

        public Time(int minutes, int seconds)
        {
            Minutes = minutes;
            Seconds = seconds;
        }

        public int TotalSeconds => Minutes * 60 + Seconds;
        public bool IsZero => Minutes == 0 && Seconds == 0;
        public string Display => $"{Minutes}:{Seconds:D2}";
    }
}