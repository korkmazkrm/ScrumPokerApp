using Microsoft.AspNetCore.SignalR;
using ScrumPokerApp.Models;
using System.Collections.Concurrent;

namespace ScrumPokerApp.Hubs;

public class PokerHub : Hub
{
    private static readonly ConcurrentDictionary<string, PokerRoom> Rooms = new();

    public async Task<string> CreateRoom(string roomName, string adminName, List<string> taskTitles, List<string> estimateOptions, bool isFreeText)
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
        room.Players.Add(new Player { ConnectionId = Context.ConnectionId, Name = adminName });
        Rooms[roomId] = room;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        return roomId;
    }

    public async Task JoinRoom(string roomId, string userName)
    {
        if (Rooms.TryGetValue(roomId, out var room))
        {
            if (!room.Players.Any(p => p.ConnectionId == Context.ConnectionId))
                room.Players.Add(new Player { ConnectionId = Context.ConnectionId, Name = userName });
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Oda bulunamadı!");
        }
    }

    // YENİ: Peek durumunu duyurur
    public async Task TogglePeekStatus(string roomId, bool isPeeking)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            await Clients.Group(roomId).SendAsync("PeekStatusChanged", isPeeking);
        }
    }

    public async Task StartRoom(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (room.Tasks.Count == 0)
            {
                await Clients.Caller.SendAsync("Error", "Önce en az bir task eklemelisiniz!");
                return;
            }
            room.IsStarted = true;
            await Clients.Group(roomId).SendAsync("RoomUpdated", room);
        }
    }

    public async Task UpdateTask(string roomId, string taskId, string newTitle)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (Guid.TryParse(taskId, out Guid taskGuid))
            {
                var taskIndex = room.Tasks.FindIndex(t => t.Id == taskGuid);
                if (taskIndex != -1)
                {
                    if (taskIndex < room.CurrentTaskIndex || (taskIndex == room.CurrentTaskIndex && room.IsVotesRevealed))
                    {
                        await Clients.Caller.SendAsync("Error", "Oylaması bitmiş veya açıklanmış bir taskı güncelleyemezsiniz!");
                        return;
                    }
                    room.Tasks[taskIndex].Title = newTitle;
                    await Clients.Group(roomId).SendAsync("RoomUpdated", room);
                }
            }
        }
    }

    public async Task DeleteTask(string roomId, string taskId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (Guid.TryParse(taskId, out Guid taskGuid))
            {
                var taskIndex = room.Tasks.FindIndex(t => t.Id == taskGuid);
                if (taskIndex != -1)
                {
                    if (taskIndex < room.CurrentTaskIndex || (taskIndex == room.CurrentTaskIndex && room.IsVotesRevealed))
                    {
                        await Clients.Caller.SendAsync("Error", "Oylaması bitmiş veya açıklanmış bir taskı silemezsiniz!");
                        return;
                    }
                    room.Tasks.RemoveAt(taskIndex);
                    if (taskIndex == room.CurrentTaskIndex)
                    {
                        room.IsVotesRevealed = false;
                        room.Players.ForEach(p => p.Vote = "");
                        await Clients.Group(roomId).SendAsync("NewTaskTriggered");
                    }
                    await Clients.Group(roomId).SendAsync("RoomUpdated", room);
                }
            }
        }
    }

    public async Task SendVote(string roomId, string vote)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.IsStarted)
        {
            var player = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                player.Vote = vote;
                await Clients.Group(roomId).SendAsync("RoomUpdated", room);
            }
        }
    }

    public async Task ShowVotes(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var room) && room.AdminConnectionId == Context.ConnectionId)
        {
            if (!room.Players.Any(p => p.HasVoted))
            {
                await Clients.Caller.SendAsync("Error", "Henüz kimse oy vermedi!");
                return;
            }
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
                var completed = new CompletedTask
                {
                    Title = room.ActiveTask.Title,
                    Results = room.Players.Select(p => new PlayerResult { Name = p.Name, Vote = p.Vote }).ToList()
                };
                room.History.Add(completed);
            }
            if (room.CurrentTaskIndex < room.Tasks.Count) room.CurrentTaskIndex++;
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