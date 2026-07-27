#pragma warning disable CA2000, CA1812

using Cascade.UI;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace Cascade.UI.Tests;

public class CalendarTests
{
    private static Bindable<DateOnly> CreateDateBinding(DateOnly initial)
    {
        DateOnly captured = initial;
        return new Bindable<DateOnly>(captured, v => { captured = v; });
    }

    [Test]
    public async Task Constructor_DefaultViewIsMonth()
    {
        var calendar = new Calendar();

        var view = calendar.View;
        await Assert.That(view).IsEqualTo(CalendarView.Month);
    }

    [Test]
    public async Task Constructor_CustomView()
    {
        var calendar = new Calendar(view: CalendarView.Week);

        var view = calendar.View;
        await Assert.That(view).IsEqualTo(CalendarView.Week);
    }

    [Test]
    public async Task Constructor_EventsDefaultToEmptyList()
    {
        var calendar = new Calendar();

        var count = calendar.Events.Count;
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithEvents()
    {
        var events = new List<CalendarEvent>
        {
            new CalendarEvent
            {
                Id = "evt-1",
                Title = "Meeting",
                Start = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2024, 6, 15, 11, 0, 0, TimeSpan.Zero)
            }
        };
        var calendar = new Calendar(events: events);

        var count = calendar.Events.Count;
        var firstTitle = calendar.Events[0].Title;
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(firstTitle).IsEqualTo("Meeting");
    }

    [Test]
    public async Task Constructor_OnDayClickCallbackStored()
    {
        DateOnly? clickedDate = null;
        var calendar = new Calendar(onDayClick: d => { clickedDate = d; });

        var callback = calendar.OnDayClick;
        await Assert.That(callback).IsNotNull();
        callback!(new DateOnly(2024, 6, 15));
        await Assert.That(clickedDate).IsEqualTo(new DateOnly(2024, 6, 15));
    }

    [Test]
    public async Task Constructor_OnEventClickCallbackStored()
    {
        CalendarEvent? clickedEvent = null;
        var calendar = new Calendar(onEventClick: e => { clickedEvent = e; });

        var callback = calendar.OnEventClick;
        await Assert.That(callback).IsNotNull();

        var testEvent = new CalendarEvent
        {
            Id = "test",
            Title = "Test Event",
            Start = DateTimeOffset.Now,
            End = DateTimeOffset.Now.AddHours(1)
        };
        callback!(testEvent);

        var clickedId = clickedEvent?.Id;
        await Assert.That(clickedId).IsEqualTo("test");
    }

    [Test]
    public async Task Constructor_ShowNavigationDefaultsToTrue()
    {
        var calendar = new Calendar();

        var showNav = calendar.ShowNavigation;
        await Assert.That(showNav).IsTrue();
    }

    [Test]
    public async Task Constructor_ShowNavigationFalse()
    {
        var calendar = new Calendar(showNavigation: false);

        var showNav = calendar.ShowNavigation;
        await Assert.That(showNav).IsFalse();
    }

    [Test]
    public async Task Constructor_CategoriesStored()
    {
        var categories = new List<CalendarCategory>
        {
            new CalendarCategory("Work", ColorValue.Transparent),
            new CalendarCategory("Personal", default)
        };
        var calendar = new Calendar(categories: categories);

        var count = calendar.Categories.Count;
        var firstName = calendar.Categories[0].Name;
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(firstName).IsEqualTo("Work");
    }

    [Test]
    public async Task DragToCreate_StoresCallback()
    {
        DateTimeOffset? createdStart = null;
        DateTimeOffset? createdEnd = null;

        var calendar = new Calendar()
            .DragToCreate((start, end) =>
            {
                createdStart = start;
                createdEnd = end;
            });

        var callback = calendar.OnDragCreate;
        await Assert.That(callback).IsNotNull();

        var start = new DateTimeOffset(2024, 6, 15, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
        callback!(start, end);

        await Assert.That(createdStart).IsEqualTo(start);
        await Assert.That(createdEnd).IsEqualTo(end);
    }

    [Test]
    public async Task DragToMove_StoresCallback()
    {
        CalendarEvent? movedEvent = null;
        DateTimeOffset? movedTo = null;

        var calendar = new Calendar()
            .DragToMove((evt, newTime) =>
            {
                movedEvent = evt;
                movedTo = newTime;
            });

        var callback = calendar.OnDragMove;
        await Assert.That(callback).IsNotNull();

        var testEvent = new CalendarEvent
        {
            Id = "move-test",
            Title = "Movable",
            Start = DateTimeOffset.Now,
            End = DateTimeOffset.Now.AddHours(1)
        };
        var newTime = new DateTimeOffset(2024, 6, 16, 14, 0, 0, TimeSpan.Zero);
        callback!(testEvent, newTime);

        var movedId = movedEvent?.Id;
        await Assert.That(movedId).IsEqualTo("move-test");
        await Assert.That(movedTo).IsEqualTo(newTime);
    }

    [Test]
    public async Task DragToResize_StoresCallback()
    {
        CalendarEvent? resizedEvent = null;
        DateTimeOffset? resizedTo = null;

        var calendar = new Calendar()
            .DragToResize((evt, newEnd) =>
            {
                resizedEvent = evt;
                resizedTo = newEnd;
            });

        var callback = calendar.OnDragResize;
        await Assert.That(callback).IsNotNull();

        var testEvent = new CalendarEvent
        {
            Id = "resize-test",
            Title = "Resizable",
            Start = DateTimeOffset.Now,
            End = DateTimeOffset.Now.AddHours(1)
        };
        var newEnd = new DateTimeOffset(2024, 6, 15, 17, 0, 0, TimeSpan.Zero);
        callback!(testEvent, newEnd);

        var resizedId = resizedEvent?.Id;
        await Assert.That(resizedId).IsEqualTo("resize-test");
        await Assert.That(resizedTo).IsEqualTo(newEnd);
    }

    [Test]
    public async Task ExtensionMethods_ReturnSameInstance()
    {
        var calendar = new Calendar();
        Action<DateTimeOffset, DateTimeOffset> createHandler = (_, _) => { };
        Action<CalendarEvent, DateTimeOffset> moveHandler = (_, _) => { };
        Action<CalendarEvent, DateTimeOffset> resizeHandler = (_, _) => { };

        var afterCreate = calendar.DragToCreate(createHandler);
        var afterMove = afterCreate.DragToMove(moveHandler);
        var afterResize = afterMove.DragToResize(resizeHandler);

        var sameAfterCreate = ReferenceEquals(calendar, afterCreate);
        var sameAfterMove = ReferenceEquals(calendar, afterMove);
        var sameAfterResize = ReferenceEquals(calendar, afterResize);
        await Assert.That(sameAfterCreate).IsTrue();
        await Assert.That(sameAfterMove).IsTrue();
        await Assert.That(sameAfterResize).IsTrue();
    }

    [Test]
    public async Task Constructor_DayViewMode()
    {
        var calendar = new Calendar(view: CalendarView.Day);

        var view = calendar.View;
        await Assert.That(view).IsEqualTo(CalendarView.Day);
    }

    [Test]
    public async Task Constructor_AgendaViewMode()
    {
        var calendar = new Calendar(view: CalendarView.Agenda);

        var view = calendar.View;
        await Assert.That(view).IsEqualTo(CalendarView.Agenda);
    }
}
