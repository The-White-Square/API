namespace GameApp.Models
{
    public struct GameTimer
    {
        public Time StartTime { get; set; }
        public Time CurrentTime { get; set; }
        public GameTimer(int minutes, int seconds)
        {
            StartTime = new Time(minutes, seconds);
            CurrentTime = new Time(minutes, seconds);
        }

        public int RemainingSeconds => CurrentTime.TotalSeconds;
        public bool IsFinished => CurrentTime.IsZero;
        public string TimeLeft => CurrentTime.Display;

        public void Tick()
        {
            if (CurrentTime.TotalSeconds > 0)
            {
                var totalSeconds = CurrentTime.TotalSeconds - 1;
                CurrentTime = new Time(totalSeconds / 60, totalSeconds % 60);
            }
        }
    }
}