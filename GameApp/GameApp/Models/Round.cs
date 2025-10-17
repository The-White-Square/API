namespace GameApp.Models
{
    public class Round
    {
        public GameTimer RoundTimer { get; }
        public bool IsEndedManually { get; set; }

        public Round(int minutes = 5, int seconds = 0)
        {
            RoundTimer = new GameTimer(minutes, seconds);
            IsEndedManually = false;
        }

        public void EndManually() => IsEndedManually = true;

        public bool IsEnded => RoundTimer.IsFinished || IsEndedManually;
    }
}