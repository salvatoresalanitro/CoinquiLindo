using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class CleaningTaskEndpointsTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        // Calendar day in the house time zone (default Europe/Rome)
        private static DateOnly Today() => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")));

        private async Task<(HttpClient Owner, HttpClient Flatmate, Guid HouseId, Guid RoomId)> ArrangeAsync()
        {
            var owner = await factory.CreateAuthenticatedClientAsync(displayName: "Salvo");
            var houseId = await owner.CreateHouseAsync("Casa attività");
            var house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var flatmate = await factory.CreateAuthenticatedClientAsync(displayName: "Lucia");
            (await flatmate.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(house.InviteCode, null))).EnsureSuccessStatusCode();
            var room = await owner.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest("Cucina", ["Lavare i piatti"]));
            return (owner, flatmate, houseId, await room.Content.ReadFromJsonAsync<Guid>());
        }

        [Fact]
        public async Task CleaningTasks_Should_BeAddedRenamedAndRemoved_UpdatingShiftsNotStarted_WithOneGroupedNotification()
        {
            var (owner, flatmate, houseId, roomId) = await ArrangeAsync();
            var ownerTenantId = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.Tenants.Single(t => t.Name == "Salvo").Id;
            var today = Today();
            (await owner.PostAsJsonAsync($"/api/houses/{houseId}/shifts",
                new AssignShiftRequest(ownerTenantId, roomId, today, today.AddDays(6)))).EnsureSuccessStatusCode();
            (await owner.PostAsJsonAsync($"/api/houses/{houseId}/shifts",
                new AssignShiftRequest(ownerTenantId, roomId, today.AddDays(7), today.AddDays(13)))).EnsureSuccessStatusCode();
            var tasksRoute = $"/api/houses/{houseId}/rooms/{roomId}/tasks";

            var added = await owner.PostAsJsonAsync(tasksRoute, new AddCleaningTaskRequest("Pulire i vetri"));
            added.StatusCode.Should().Be(HttpStatusCode.OK);
            var newTaskId = await added.Content.ReadFromJsonAsync<Guid>();
            var room = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.Rooms.Single();
            var washId = room.CleaningTasks.Single(t => t.Description == "Lavare i piatti").Id;
            room.CleaningTasks.Should().Contain(t => t.Id == newTaskId && t.Description == "Pulire i vetri");

            (await owner.PutAsJsonAsync($"{tasksRoute}/{washId}", new UpdateCleaningTaskRequest("Lavare e asciugare")))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var shifts = (await owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{houseId}/shifts"))!;
            shifts.Single(s => s.StartDate == today.AddDays(7)).Checklist.Select(i => i.Description)
                .Should().BeEquivalentTo(["Lavare e asciugare", "Pulire i vetri"]);
            // The shift in progress keeps its checklist
            shifts.Single(s => s.StartDate == today).Checklist.Select(i => i.Description).Should().Equal("Lavare i piatti");

            (await owner.DeleteAsync($"{tasksRoute}/{newTaskId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{houseId}/shifts"))!
                .Single(s => s.StartDate == today.AddDays(7)).Checklist.Select(i => i.Description).Should().Equal("Lavare e asciugare");
            (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.Rooms.Single().CleaningTasks
                .Select(t => t.Description).Should().Equal("Lavare e asciugare");

            // Three changes, one unread notification for the flatmate with the newest change on top; none for the author
            var notifications = (await flatmate.GetFromJsonAsync<List<NotificationResponse>>("/api/me/notifications?unreadOnly=true"))!
                .Where(n => n.Type == NotificationKind.CleaningTasksChanged).ToList();
            var grouped = notifications.Should().ContainSingle().Which;
            grouped.Body.Split('\n').Should().Equal(
                "Salvo ha eliminato «Pulire i vetri» da Cucina.",
                "Salvo ha modificato «Lavare i piatti» in «Lavare e asciugare» (Cucina).",
                "Salvo ha aggiunto «Pulire i vetri» in Cucina.");
            (await owner.GetFromJsonAsync<List<NotificationResponse>>("/api/me/notifications"))!
                .Should().NotContain(n => n.Type == NotificationKind.CleaningTasksChanged);
        }

        [Fact]
        public async Task CleaningTaskEndpoints_Should_ReturnProblemDetails()
        {
            var (owner, _, houseId, roomId) = await ArrangeAsync();
            var outsider = await factory.CreateAuthenticatedClientAsync();
            var tasksRoute = $"/api/houses/{houseId}/rooms/{roomId}/tasks";

            var duplicate = await owner.PostAsJsonAsync(tasksRoute, new AddCleaningTaskRequest(" lavare I piatti "));
            var empty = await owner.PostAsJsonAsync(tasksRoute, new AddCleaningTaskRequest("  "));
            var unknownRoom = await owner.PostAsJsonAsync($"/api/houses/{houseId}/rooms/{Guid.NewGuid()}/tasks", new AddCleaningTaskRequest("Spazzare"));
            var unknownTaskPut = await owner.PutAsJsonAsync($"{tasksRoute}/{Guid.NewGuid()}", new UpdateCleaningTaskRequest("Spazzare"));
            var unknownTaskDelete = await owner.DeleteAsync($"{tasksRoute}/{Guid.NewGuid()}");
            var forbidden = await outsider.PostAsJsonAsync(tasksRoute, new AddCleaningTaskRequest("Spazzare"));

            (await duplicate.ReadProblemAsync(HttpStatusCode.Conflict)).ErrorCode.Should().Be("Room.CleaningTaskAlreadyExists");
            (await empty.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("CleaningTask.DescriptionRequired");
            (await unknownRoom.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Room.NotFound");
            (await unknownTaskPut.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("CleaningTask.NotFound");
            (await unknownTaskDelete.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("CleaningTask.NotFound");
            (await forbidden.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
        }
    }
}
