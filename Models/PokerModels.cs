namespace ScrumPokerApp.Models;

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public string Vote { get; set; } = "";
    public bool HasVoted => !string.IsNullOrEmpty(Vote);
}

public class ScrumTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public class CompletedTask
{
    public string Title { get; set; } = "";
    public double? Average { get; set; } // YENİ: Ortalama puan (Free text değilse)
    public List<PlayerResult> Results { get; set; } = new();
}

public class PlayerResult
{
    public string Name { get; set; } = "";
    public string Vote { get; set; } = "";
}

public class PokerRoom
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string AdminConnectionId { get; set; } = "";
    public bool IsStarted { get; set; } = false;
    public bool IsFreeText { get; set; } = false;
    public List<string> EstimateOptions { get; set; } = new();
    public List<ScrumTask> Tasks { get; set; } = new();
    public int CurrentTaskIndex { get; set; } = 0;
    public bool IsVotesRevealed { get; set; }
    public List<Player> Players { get; set; } = new();
    public List<CompletedTask> History { get; set; } = new();
    public ScrumTask? ActiveTask => (IsStarted && Tasks.Count > CurrentTaskIndex) ? Tasks[CurrentTaskIndex] : null;
}