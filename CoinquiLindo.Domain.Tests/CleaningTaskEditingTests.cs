using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class CleaningTaskEditingTests
    {
        private static Room RoomWith(params string[] tasks)
        {
            var room = new Room("Cucina");
            foreach (var task in tasks)
            {
                room.AddCleaningTask(new CleaningTask(task));
            }

            return room;
        }

        private static Shift ShiftOf(Room room) => new(Guid.NewGuid(), new Tenant("Salvo"), room,
            new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), TimeZoneInfo.Utc);

        [Fact]
        public void CleaningTask_Should_TrimItsDescription()
        {
            new CleaningTask("  Lavare i piatti  ").Description.Should().Be("Lavare i piatti");
        }

        [Fact]
        public void AddCleaningTask_Should_RejectDuplicates_IgnoringCaseAndSpaces()
        {
            var room = RoomWith("Lavare i piatti");

            var duplicate = () => room.AddCleaningTask(new CleaningTask(" lavare I PIATTI "));

            duplicate.Should().Throw<CleaningTaskAlreadyExistsException>().Which.Code.Should().Be("Room.CleaningTaskAlreadyExists");
            room.CleaningTasks.Should().ContainSingle();
        }

        [Fact]
        public void RenameCleaningTask_Should_ChangeDescription_AndReturnThePreviousOne()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");
            var task = room.CleaningTasks.First();

            var previous = room.RenameCleaningTask(task.Id, "  Lavare e asciugare i piatti ");

            previous.Should().Be("Lavare i piatti");
            task.Description.Should().Be("Lavare e asciugare i piatti");
            // Changing only the case of its own description is allowed
            room.RenameCleaningTask(task.Id, "LAVARE E ASCIUGARE I PIATTI");
            task.Description.Should().Be("LAVARE E ASCIUGARE I PIATTI");
        }

        [Fact]
        public void RenameCleaningTask_Should_RejectDuplicates_EmptyDescriptions_AndUnknownTasks()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");
            var task = room.CleaningTasks.First();

            var duplicate = () => room.RenameCleaningTask(task.Id, "pulire i fornelli");
            var empty = () => room.RenameCleaningTask(task.Id, "   ");
            var unknown = () => room.RenameCleaningTask(Guid.NewGuid(), "Spazzare");

            duplicate.Should().Throw<CleaningTaskAlreadyExistsException>();
            empty.Should().Throw<DomainValidationException>().Which.Code.Should().Be("CleaningTask.DescriptionRequired");
            unknown.Should().Throw<NotFoundException>().Which.Code.Should().Be("CleaningTask.NotFound");
            task.Description.Should().Be("Lavare i piatti");
        }

        [Fact]
        public void RemoveCleaningTask_Should_RemoveAndReturnTheTask_OrThrowNotFound()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");
            var task = room.CleaningTasks.First();

            room.RemoveCleaningTask(task.Id).Should().BeSameAs(task);
            var again = () => room.RemoveCleaningTask(task.Id);

            room.CleaningTasks.Should().ContainSingle().Which.Description.Should().Be("Pulire i fornelli");
            again.Should().Throw<NotFoundException>().Which.Code.Should().Be("CleaningTask.NotFound");
        }

        [Fact]
        public void Shift_Should_LinkEveryChecklistItemToItsCleaningTask()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");

            var shift = ShiftOf(room);

            shift.Checklist.Select(i => (i.CleaningTaskId, i.Description)).Should().BeEquivalentTo(
                room.CleaningTasks.Select(t => ((Guid?)t.Id, t.Description)));
        }

        [Fact]
        public void AddChecklistItem_Should_AddALinkedItem_OnlyOnce()
        {
            var room = RoomWith("Lavare i piatti");
            var shift = ShiftOf(room);
            shift.MarkItemAsCompleted(shift.Checklist.Single().Id);
            var task = new CleaningTask("Pulire i vetri");

            shift.AddChecklistItem(task);
            shift.AddChecklistItem(task);

            shift.Checklist.Should().HaveCount(2);
            shift.Checklist.Single(i => i.CleaningTaskId == task.Id).Description.Should().Be("Pulire i vetri");
            shift.IsShiftFullyCompleted.Should().BeFalse();
        }

        [Fact]
        public void RenameChecklistItem_Should_KeepTheCompletionState()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");
            var task = room.CleaningTasks.First();
            var shift = ShiftOf(room);
            var item = shift.Checklist.Single(i => i.CleaningTaskId == task.Id);
            shift.MarkItemAsCompleted(item.Id);

            shift.RenameChecklistItem(task.Id, "Lavare e asciugare i piatti");
            shift.RenameChecklistItem(Guid.NewGuid(), "Ignorato");

            item.Description.Should().Be("Lavare e asciugare i piatti");
            item.IsCompleted.Should().BeTrue();
            shift.Checklist.Should().HaveCount(2);
        }

        [Fact]
        public void RemoveChecklistItem_Should_RemoveTheLinkedItem_AndIgnoreUnknownTasks()
        {
            var room = RoomWith("Lavare i piatti", "Pulire i fornelli");
            var task = room.CleaningTasks.First();
            var shift = ShiftOf(room);

            shift.RemoveChecklistItem(task.Id);
            shift.RemoveChecklistItem(Guid.NewGuid());

            shift.Checklist.Should().ContainSingle().Which.Description.Should().Be("Pulire i fornelli");
        }
    }
}
