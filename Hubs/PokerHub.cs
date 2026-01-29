using Microsoft.AspNetCore.SignalR;
using ScrumPokerApp.Models;
using System.Collections.Concurrent;
using System.Globalization;

namespace ScrumPokerApp.Hubs;

public class PokerHub : Hub
{
    private static readonly ConcurrentDictionary<string, PokerRoom> Rooms = new();

    public async Task<string> CreateRoom(string roomName, string adminName, string avatarUrl, List<string> taskTitles, List<string> estimateOptions, bool isFreeText)
    {
        var roomId = Guid.NewGuid().ToString().Substring(0, 8);
        var room = new PokerRoom
        {
            RoomId = roomId,
            RoomName = roomName,
            AdminConnectionId = Context.ConnectionId,
            EstimateOptions = estimateOptions,
            IsFreeText = isFreeText,
            Tasks = taskTitles?.Select(t => new ScrumTask { Title = t }).ToList() ?? new List<ScrumTask>()
        };
        room.Players.Add(new Player { ConnectionId = Context.ConnectionId, Name = adminName, AvatarUrl = avatarUrl });
        Rooms[roomId] = room;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        return roomId;
    }

    public async Task JoinRoom(string roomId, string userName, string avatarUrl)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            if (!room.Players.Any(p => p.ConnectionId == Context.ConnectionId))
                room.Players.Add(new Player { ConnectionId = Context.ConnectionId, Name = userName, AvatarUrl = avatarUrl });
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task TogglePeekStatus(string roomId, bool isPeeking)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
            await Clients.Group(roomId).SendAsync("PeekStatusChanged", isPeeking);
    }

    public async Task StartRoom(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (room.Tasks.Count == 0) { await Clients.Caller.SendAsync("Error", "Önce task eklemelisiniz!"); return; }
            room.IsStarted = true;
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task UpdateTask(string roomId, string taskId, string newTitle)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            var task = room.Tasks.FirstOrDefault(t => t.Id.ToString() == taskId);
            if (task != null) { task.Title = newTitle; await Clients.Group(roomId).SendAsync("RoomUpdated", room); }
        }
    }

    public async Task DeleteTask(string roomId, string taskId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            room.Tasks.RemoveAll(t => t.Id.ToString() == taskId);
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task SendVote(string roomId, string vote)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.IsStarted)
        {
            var player = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null) { player.Vote = vote; await Clients.Group(roomId).SendAsync("RoomUpdated", room); }
        }
    }

    public async Task ShowVotes(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            room.IsVotesRevealed = true;
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task NextTask(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (room.ActiveTask != null)
            {
                double? avg = null;
                // Ortalama Hesaplama: Sadece Free Text değilse ve sayısal değerler varsa
                if (!room.IsFreeText)
                {
                    var numericVotes = room.Players
                        .Select(p => p.Vote)
                        .Select(v => {
                            bool success = double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double val);
                            return new { success, val };
                        })
                        .Where(x => x.success)
                        .Select(x => x.val)
                        .ToList();

                    if (numericVotes.Any()) avg = Math.Round(numericVotes.Average(), 1);
                }

                room.History.Add(new CompletedTask
                {
                    Title = room.ActiveTask.Title,
                    Average = avg,
                    Results = room.Players.Select(p => new PlayerResult { Name = p.Name, Vote = p.Vote }).ToList()
                });
            }
            room.CurrentTaskIndex++;
            room.IsVotesRevealed = false;
            room.Players.ForEach(p => p.Vote = "");
            await Clients.Group(roomId).SendAsync("NewTaskTriggered");
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task AddTasks(string roomId, List<string> newTaskTitles)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            room.Tasks.AddRange(newTaskTitles.Select(t => new ScrumTask { Title = t }));
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }
}