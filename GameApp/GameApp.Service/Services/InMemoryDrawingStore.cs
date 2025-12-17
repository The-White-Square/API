using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GameApp.Service.Dtos;

namespace GameApp.Service.Services;

public sealed class InMemoryDrawingStore : IDrawingStore
{
    private sealed class Timeline
    {
        public List<DrawingEventBase> Events { get; } = new();
        public List<int> ActionIndices { get; } = new(); // indices where an action finished (StrokeEnded/CanvasCleared)
        public int ActiveCount { get; set; } = 0;
    }

    private readonly ConcurrentDictionary<string, Timeline> _timelines = new();

    private Timeline GetTimeline(string lobbyId) =>
        _timelines.GetOrAdd(lobbyId, _ => new Timeline());

    private static void TrimToActive(Timeline t)
    {
        // If user has undone some actions, ActiveCount < Events.Count.
        // Any new edit must clear the redo tail.
        if (t.ActiveCount < 0) t.ActiveCount = 0;
        if (t.ActiveCount > t.Events.Count) t.ActiveCount = t.Events.Count;
        if (t.ActiveCount == t.Events.Count) return;

        // Remove events beyond ActiveCount
        var removeCount = t.Events.Count - t.ActiveCount;
        if (removeCount > 0)
        {
            t.Events.RemoveRange(t.ActiveCount, removeCount);
        }

        // Keep only action boundaries that are <= ActiveCount (inclusive)
        t.ActionIndices.RemoveAll(i => i > t.ActiveCount);
    }

    public void AppendStrokeStarted(string lobbyId, string strokeId, string color, double width, string tool)
    {
        var t = GetTimeline(lobbyId);
        TrimToActive(t);
        t.Events.Add(new StrokeStartedEvent(strokeId, color, width, tool));
        t.ActiveCount = t.Events.Count;
    }

    public void AppendStrokePoints(string lobbyId, string strokeId, IReadOnlyList<PointDto> points)
    {
        var t = GetTimeline(lobbyId);
        TrimToActive(t);
        t.Events.Add(new StrokePointsEvent(strokeId, points));
        t.ActiveCount = t.Events.Count;
    }

    public void AppendStrokeEnded(string lobbyId, string strokeId)
    {
        var t = GetTimeline(lobbyId);
        TrimToActive(t);
        t.Events.Add(new StrokeEndedEvent(strokeId));
        t.ActiveCount = t.Events.Count;
        t.ActionIndices.Add(t.ActiveCount); // action boundary is inclusive
    }

    public void AppendCanvasCleared(string lobbyId)
    {
        var t = GetTimeline(lobbyId);
        TrimToActive(t);
        t.Events.Add(new CanvasClearedEvent());
        t.ActiveCount = t.Events.Count;
        t.ActionIndices.Add(t.ActiveCount);
    }

    public IReadOnlyList<DrawingEventBase> GetActiveEvents(string lobbyId)
    {
        var t = GetTimeline(lobbyId);
        var count = t.ActiveCount;
        if (count <= 0) return new List<DrawingEventBase>();
        return t.Events.Take(count).ToList();
    }

    public bool UndoLast(string lobbyId)
    {
        var t = GetTimeline(lobbyId);
        if (t.ActionIndices.Count == 0) return false;

        // current action index is the last <= ActiveCount
        var currentIdx = t.ActionIndices.FindLastIndex(i => i <= t.ActiveCount);
        if (currentIdx <= 0)
        {
            t.ActiveCount = 0;
            return true;
        }

        var previousActionInclusiveCount = t.ActionIndices[currentIdx - 1];
        t.ActiveCount = previousActionInclusiveCount;
        return true;
    }

    public bool RedoLast(string lobbyId)
    {
        var t = GetTimeline(lobbyId);
        if (t.ActionIndices.Count == 0) return false;

        var currentIdx = t.ActionIndices.FindLastIndex(i => i <= t.ActiveCount);
        var nextIdx = currentIdx + 1;
        if (nextIdx >= t.ActionIndices.Count) return false;

        t.ActiveCount = t.ActionIndices[nextIdx];
        return true;
    }

    public void ResetLobby(string lobbyId)
    {
        _timelines.TryRemove(lobbyId, out _);
    }
}