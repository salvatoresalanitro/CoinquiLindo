using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class RotationEndpointsTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        private sealed record Setup(HttpClient Owner, List<HttpClient> Members, Guid HouseId, HouseResponse House, List<Guid> RoomIds);

        // Monday of the current week in the house time zone (default Europe/Rome)
        private static DateOnly CurrentMonday()
        {
            var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, rome));
            return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        }

        private async Task<Setup> ArrangeAsync(int extraMembers, int rooms)
        {
            var owner = await factory.CreateAuthenticatedClientAsync(displayName: "Salvo");
            var houseId = await owner.CreateHouseAsync("Casa rotazione");
            var inviteCode = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.InviteCode;

            var members = new List<HttpClient> { owner };
            for (var i = 1; i <= extraMembers; i++)
            {
                var member = await factory.CreateAuthenticatedClientAsync(displayName: $"Coinquilino {i}");
                (await member.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, null))).EnsureSuccessStatusCode();
                members.Add(member);
            }

            var roomIds = new List<Guid>();
            for (var i = 1; i <= rooms; i++)
            {
                var response = await owner.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest($"Stanza {i}", ["Pulire"]));
                roomIds.Add(await response.Content.ReadFromJsonAsync<Guid>());
            }

            var house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            return new Setup(owner, members, houseId, house, roomIds);
        }

        private static List<Guid> TenantOrder(Setup s, params string[] names) =>
            names.Select(name => s.House.Tenants.Single(t => t.Name == name).Id).ToList();

        [Fact]
        public async Task Rotation_Should_BeSaved_GenerateShifts_AndBeDeleted()
        {
            var s = await ArrangeAsync(extraMembers: 1, rooms: 2);
            var monday = CurrentMonday();
            var tenants = TenantOrder(s, "Salvo", "Coinquilino 1");
            var route = $"/api/houses/{s.HouseId}/rotation";

            (await s.Owner.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var save = await s.Owner.PutAsJsonAsync(route, new SaveRotationPlanRequest(tenants, s.RoomIds, monday));

            save.StatusCode.Should().Be(HttpStatusCode.OK);
            var plan = (await save.Content.ReadFromJsonAsync<RotationPlanResponse>())!;
            plan.StartDate.Should().Be(monday);
            plan.Tenants.Select(t => t.Id).Should().Equal(tenants);
            plan.Weeks.Should().HaveCount(2);

            var shifts = (await s.Owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!;
            shifts.Should().HaveCount(4).And.OnlyContain(x => x.IsFromRotation);
            shifts.Where(x => x.StartDate == monday).Should().HaveCount(2);
            shifts.Single(x => x.StartDate == monday.AddDays(7) && x.AssignedTenantId == tenants[0]).AssignedRoomId
                .Should().Be(s.RoomIds[1]);

            // Saving again does not duplicate the shifts
            (await s.Owner.PutAsJsonAsync(route, new SaveRotationPlanRequest(tenants, s.RoomIds, monday))).EnsureSuccessStatusCode();
            (await s.Members[1].GetFromJsonAsync<RotationPlanResponse>(route))!.Id.Should().Be(plan.Id);
            (await s.Owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!.Should().HaveCount(4);

            var delete = await s.Owner.DeleteAsync(route);

            delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await s.Owner.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            // The week in progress stays, the next one is gone
            (await s.Owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!
                .Should().HaveCount(2).And.OnlyContain(x => x.StartDate == monday);
        }

        [Fact]
        public async Task SaveRotation_Should_ReturnProblemDetails_ForInvalidPlans_AndNonMembers()
        {
            var s = await ArrangeAsync(extraMembers: 0, rooms: 1);
            var tenants = TenantOrder(s, "Salvo");
            var route = $"/api/houses/{s.HouseId}/rotation";
            var outsider = await factory.CreateAuthenticatedClientAsync();

            var empty = await s.Owner.PutAsJsonAsync(route, new SaveRotationPlanRequest([], s.RoomIds, CurrentMonday()));
            var unknownRoom = await s.Owner.PutAsJsonAsync(route, new SaveRotationPlanRequest(tenants, [Guid.NewGuid()], CurrentMonday()));
            var forbidden = await outsider.PutAsJsonAsync(route, new SaveRotationPlanRequest(tenants, s.RoomIds, CurrentMonday()));

            (await empty.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("RotationPlan.TenantsRequired");
            (await unknownRoom.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Room.NotFound");
            (await forbidden.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            (await (await outsider.GetAsync(route)).ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            (await (await outsider.DeleteAsync(route)).ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
        }

        [Fact]
        public async Task ShiftHelper_Should_BeChosenAmongRestingTenants_AndRemoved()
        {
            // 3 tenants, 2 rooms: in week 0 "Coinquilino 2" rests
            var s = await ArrangeAsync(extraMembers: 2, rooms: 2);
            var monday = CurrentMonday();
            var tenants = TenantOrder(s, "Salvo", "Coinquilino 1", "Coinquilino 2");
            (await s.Owner.PutAsJsonAsync($"/api/houses/{s.HouseId}/rotation",
                new SaveRotationPlanRequest(tenants, s.RoomIds, monday))).EnsureSuccessStatusCode();
            var ownShift = (await s.Owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!
                .Single(x => x.StartDate == monday && x.AssignedTenantId == tenants[0]);
            var helperRoute = $"/api/houses/{s.HouseId}/shifts/{ownShift.Id}/helper";

            ownShift.HelperCandidates.Should().ContainSingle().Which.Id.Should().Be(tenants[2]);

            var notResting = await s.Owner.PutAsJsonAsync(helperRoute, new SetShiftHelperRequest(tenants[1]));
            var notAssignee = await s.Members[1].PutAsJsonAsync(helperRoute, new SetShiftHelperRequest(tenants[2]));
            var chosen = await s.Owner.PutAsJsonAsync(helperRoute, new SetShiftHelperRequest(tenants[2]));

            (await notResting.ReadProblemAsync(HttpStatusCode.UnprocessableEntity)).ErrorCode.Should().Be("Shift.HelperNotAllowed");
            (await notAssignee.ReadProblemAsync(HttpStatusCode.UnprocessableEntity)).ErrorCode.Should().Be("Shift.HelperNotAllowed");
            chosen.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var seenByHelper = (await s.Members[2].GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!
                .Single(x => x.Id == ownShift.Id);
            seenByHelper.HelperTenantId.Should().Be(tenants[2]);
            seenByHelper.HelperTenantName.Should().Be("Coinquilino 2");
            seenByHelper.CanRemoveHelper.Should().BeTrue();
            (await s.Members[2].GetStringAsync("/api/me/notifications")).Should().Contain("ShiftHelperChosen");

            var removed = await s.Members[2].DeleteAsync(helperRoute);

            removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await s.Owner.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{s.HouseId}/shifts"))!
                .Single(x => x.Id == ownShift.Id).HelperTenantId.Should().BeNull();
        }
    }
}
