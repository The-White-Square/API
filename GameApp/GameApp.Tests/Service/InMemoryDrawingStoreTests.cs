using GameApp.Service.Dtos;
using GameApp.Service.Services;

namespace GameApp.Tests.Service;

public class InMemoryDrawingStoreTests
{
    private InMemoryDrawingStore CreateStore() => new InMemoryDrawingStore();

    [Fact]
    public void GetActiveEvents_EmptyLobby_ReturnsEmptyList()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";

        // Act
        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void AppendStrokeStarted_AddsEventAndIncrementsActiveCount()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";

        // Act
        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.5, "pen");
        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.Single(events);
        var strokeEvent = Assert.IsType<StrokeStartedEvent>(events[0]);
        Assert.Equal("stroke1", strokeEvent.StrokeId);
        Assert.Equal("#FF0000", strokeEvent.Color);
        Assert.Equal(2.5, strokeEvent.Width);
        Assert.Equal("pen", strokeEvent.Tool);
    }

    [Fact]
    public void UndoLast_WithOneAction_SetsActiveCountToZero()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";
        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        // Act
        var result = store.UndoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.True(result);
        Assert.Empty(events);
    }

    [Fact]
    public void UndoLast_EmptyTimeline_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";

        // Act
        var result = store.UndoLast(lobbyId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RedoLast_AfterUndo_RestoresEvents()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";
        store.AppendStrokeStarted(lobbyId, "stroke1", "#0000FF", 3.0, "marker");
        store.AppendStrokeEnded(lobbyId, "stroke1");
        store.UndoLast(lobbyId);

        // Act
        var result = store.RedoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.True(result);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void RedoLast_WithoutUndo_ReturnsFalse()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";
        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        // Act
        var result = store.RedoLast(lobbyId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AppendAfterUndo_ClearsRedoTail()
    {
        // Arrange
        var store = CreateStore();
        var lobbyId = "lobby1";

        // Добавляем первый stroke
        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        // Добавляем второй stroke
        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        // Undo the second stroke
        store.UndoLast(lobbyId);

        // Add a new stroke
        store.AppendStrokeStarted(lobbyId, "stroke3", "#0000FF", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke3");

        // Try to redo
        var canRedo = store.RedoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.False(canRedo); // redo невозможен, т.к. хвост очищен
        Assert.Equal(4, events.Count); // stroke1 (2 события) + stroke3 (2 события)
    }

    [Fact]
    public void DifferentLobbies_AreIsolated()
    {
        var store = CreateStore();
        var lobby1 = "lobby1";
        var lobby2 = "lobby2";

        store.AppendStrokeStarted(lobby1, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobby1, "stroke1");

        store.AppendStrokeStarted(lobby2, "stroke2", "#00FF00", 3.0, "brush");
        store.AppendStrokeEnded(lobby2, "stroke2");

        var events1 = store.GetActiveEvents(lobby1);
        var events2 = store.GetActiveEvents(lobby2);

        Assert.Equal(2, events1.Count);
        Assert.Equal(2, events2.Count);

        var stroke1 = Assert.IsType<StrokeStartedEvent>(events1[0]);
        var stroke2 = Assert.IsType<StrokeStartedEvent>(events2[0]);

        Assert.Equal("stroke1", stroke1.StrokeId);
        Assert.Equal("stroke2", stroke2.StrokeId);
    }

    [Fact]
    public void MultipleUndo_RestoresCorrectState()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        store.AppendStrokeStarted(lobbyId, "stroke3", "#0000FF", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke3");

        store.UndoLast(lobbyId);
        store.UndoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        Assert.Equal(2, events.Count); // только stroke1
        var strokeEvent = Assert.IsType<StrokeStartedEvent>(events[0]);
        Assert.Equal("stroke1", strokeEvent.StrokeId);
    }

    [Fact]
    public void MultipleRedo_RestoresCorrectState()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        store.UndoLast(lobbyId);
        store.UndoLast(lobbyId);

        store.RedoLast(lobbyId);
        store.RedoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        Assert.Equal(4, events.Count); // оба stroke восстановлены
    }

    [Fact]
    public void UndoAll_ThenRedoAll_RestoresFullState()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        store.AppendCanvasCleared(lobbyId);

        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        store.UndoLast(lobbyId);
        store.UndoLast(lobbyId);
        store.UndoLast(lobbyId);

        var emptyEvents = store.GetActiveEvents(lobbyId);

        store.RedoLast(lobbyId);
        store.RedoLast(lobbyId);
        store.RedoLast(lobbyId);

        var restoredEvents = store.GetActiveEvents(lobbyId);

        Assert.Empty(emptyEvents);
        Assert.Equal(5, restoredEvents.Count);
    }

    [Fact]
    public void ResetLobby_ClearsAllData()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        store.ResetLobby(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        Assert.Empty(events);
    }

    [Fact]
    public void ResetLobby_DoesNotAffectOtherLobbies()
    {
        var store = CreateStore();
        var lobby1 = "lobby1";
        var lobby2 = "lobby2";

        store.AppendStrokeStarted(lobby1, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobby1, "stroke1");

        store.AppendStrokeStarted(lobby2, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobby2, "stroke2");

        store.ResetLobby(lobby1);

        var events1 = store.GetActiveEvents(lobby1);
        var events2 = store.GetActiveEvents(lobby2);

        Assert.Empty(events1);
        Assert.Equal(2, events2.Count);
    }

    [Fact]
    public void CanvasCleared_ThenUndo_RestoresPreviousStrokes()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        store.AppendCanvasCleared(lobbyId);

        store.UndoLast(lobbyId);
        var events = store.GetActiveEvents(lobbyId);

        Assert.Equal(4, events.Count); // оба stroke восстановлены
        Assert.IsType<StrokeStartedEvent>(events[0]);
        Assert.IsType<StrokeEndedEvent>(events[1]);
    }

    [Fact]
    public void AppendStrokePoints_WithEmptyList_AddsEvent()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";
        var emptyPoints = new List<PointDto>();

        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokePoints(lobbyId, "stroke1", emptyPoints);
        var events = store.GetActiveEvents(lobbyId);

        Assert.Equal(2, events.Count);
        var pointsEvent = Assert.IsType<StrokePointsEvent>(events[1]);
        Assert.Empty(pointsEvent.Points);
    }

    [Fact]
    public void ComplexWorkflow_MixedOperations_WorksCorrectly()
    {
        var store = CreateStore();
        var lobbyId = "lobby1";

        // 1. Draw a stroke
        store.AppendStrokeStarted(lobbyId, "stroke1", "#FF0000", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke1");

        // 2. Draw one more
        store.AppendStrokeStarted(lobbyId, "stroke2", "#00FF00", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke2");

        // 3. Undo the second one
        store.UndoLast(lobbyId);

        // 4. Draw one more stroke
        store.AppendStrokeStarted(lobbyId, "stroke3", "#0000FF", 2.0, "pen");
        store.AppendStrokeEnded(lobbyId, "stroke3");

        // 5. Clear canvas
        store.AppendCanvasCleared(lobbyId);

        // 6. Undo clearing
        store.UndoLast(lobbyId);

        var events = store.GetActiveEvents(lobbyId);

        // Assert
        Assert.Equal(4, events.Count); // stroke1 + stroke3
        var stroke1 = Assert.IsType<StrokeStartedEvent>(events[0]);
        var stroke3 = Assert.IsType<StrokeStartedEvent>(events[2]);
        Assert.Equal("stroke1", stroke1.StrokeId);
        Assert.Equal("stroke3", stroke3.StrokeId);
    }
}