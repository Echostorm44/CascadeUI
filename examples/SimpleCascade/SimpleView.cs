using Cascade.UI;

#pragma warning disable CA2000 // Dispose: UI framework manages component lifecycle
#pragma warning disable CA1812 // Internal class instantiated via generic constraint

namespace SimpleCascade;

internal sealed class SimpleView : Component
{
    protected override Node Render() =>
        new Center(
            new Column(
                spacing: 20,
                crossAxisAlignment: CrossAxisAlignment.Center,
                children:
                [
                    new Label("Gradient Path Test").FontSize(24),
                    CanvasFactory.Canvas(
                        new Size(200, 150),
                        (ctx, size) =>
                        {
                            var path = new PathBuilder()
                                .MoveTo(new Point(100, 10))
                                .LineTo(new Point(190, 140))
                                .LineTo(new Point(10, 140))
                                .Close()
                                .Build();

                            var gradient = Gradient.Linear(
                                new Point(0, 0),
                                new Point(200, 150),
                                new GradientStop(0.0f, new ColorValue("#FF0000")),
                                new GradientStop(1.0f, new ColorValue("#0000FF"))
                            );

                            ctx.DrawPath(path, gradient);
                        }
                    ),
                    new Button("Hi CascadeUI", () => { }).Width(200f),
                ]
            )
        );
}
