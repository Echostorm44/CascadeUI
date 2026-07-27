// Golden Example 02b — Order Confirmation
//
// Demonstrates:
//   - OnMounted for entrance animation + data loading
//   - LifetimeToken for cancellation
//   - AnimatePresence for error banners
//   - NodeRef for capturing button bounds
//   - Particles for confetti celebration
//   - AnimatedValue for choreographed entrance sequences

using Cascade.UI;

namespace OrderConfirmation;

// ── Mock Domain Types ─────────────────────────────────────────────────────────

internal sealed record OrderLineItem(string Name, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

internal sealed record ShippingAddress(
    string Name,
    string Line1,
    string Line2,
    string City,
    string State,
    string PostalCode);

internal sealed record OrderDraft(
    string OrderId,
    string OrderNumber,
    IReadOnlyList<OrderLineItem> Items,
    ShippingAddress Address,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Tax,
    decimal Total);

// ── Order Confirmation Page ───────────────────────────────────────────────────

internal sealed partial class OrderConfirmationPage : Component
{
    private OrderDraft? order;
    private bool loading    = true;
    private bool confirming = false;
    private bool confirmed  = false;
    private string error    = "";

    private readonly NodeRef<Button> confirmButtonRef = new();
    private Point confettiOrigin;

    private readonly AnimatedValue<float> opacity    = new(0f, AnimationModel.Spring.Standard);
    private readonly AnimatedValue<float> translateY = new(12f, AnimationModel.Spring.Standard);

    protected override async Task OnMounted()
    {
        opacity.Updated += _ => Invalidate();
        translateY.Updated += _ => Invalidate();

        await Task.WhenAll(
            PlayEntranceAsync(),
            LoadOrderAsync()
        );
    }

    private async Task PlayEntranceAsync()
    {
        await Task.WhenAll(
            opacity.AnimateToAsync(1f, AnimationModel.EaseOut(Duration.Ms(200)), LifetimeToken),
            translateY.AnimateToAsync(0f, AnimationModel.Spring.Standard, LifetimeToken)
        );
    }

    private async Task LoadOrderAsync()
    {
        await Delay(Duration.Ms(600), LifetimeToken);

        order = new OrderDraft(
            OrderId:      "ord-20240315-001",
            OrderNumber:  "ORD-2024-7842",
            Items: [
                new("Wireless Headphones",   1, 79.99m),
                new("USB-C Charging Cable",  2, 12.99m),
                new("Laptop Stand",          1, 49.99m),
                new("Mechanical Keyboard",   1, 129.99m)
            ],
            Address: new ShippingAddress(
                Name:       "Alex Rivera",
                Line1:      "742 Evergreen Terrace",
                Line2:      "Apt 4B",
                City:       "Portland",
                State:      "OR",
                PostalCode: "97201"
            ),
            Subtotal:     285.96m,
            ShippingCost: 9.99m,
            Tax:          23.68m,
            Total:        319.63m
        );
        loading = false;
        Invalidate();
    }

    protected override Node Render()
    {
        if (loading)
        {
            return LoadingView();
        }

        if (order is null)
        {
            return ErrorView();
        }

        if (confirmed)
        {
            return new OrderSuccessView(order, confettiOrigin);
        }

        return new ScrollView(
            new Column(spacing: 24, children: [
                PageHeader(),
                OrderSummary(order),
                ShippingSummary(order.Address),
                PriceSummary(order),
                new AnimatePresence(
                    isVisible: error.Length > 0,
                    child:     error.Length > 0
                                   ? ErrorBanner(error)
                                   : Node.Empty,
                    enter:     AnimationModel.EaseOut(Duration.Ms(200)),
                    exit:      AnimationModel.EaseOut(Duration.Ms(150))
                ),
                ConfirmFooter()
            ]).Padding(new EdgeInsets(32, 32, 32, 32))
        )
        .Opacity(opacity.Current)
        .TranslateY(translateY.Current);
    }

    private static Node PageHeader()
    {
        return new Column(spacing: 4, children: [
            new Label("Review Your Order")
                .FontSize(24)
                .Bold(),
            new Label("Confirm the details below before placing your order")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ]);
    }

    private static Node OrderSummary(OrderDraft order)
    {
        return new Column(spacing: 0, children: [
            new Label("Items")
                .FontSize(14)
                .Bold()
                .Padding(new EdgeInsets(0, 0, 12, 0)),
            .. order.Items.Select(OrderItemRow)
        ]);
    }

    private static Node OrderItemRow(OrderLineItem item)
    {
        return new Column(spacing: 0, children: [
            new Row(spacing: 12, children: [
                new Label($"{item.Quantity}×")
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                    .Width(28),
                new Label(item.Name)
                    .FontSize(14)
                    .Grow(1),
                new Label($"${item.LineTotal:F2}")
                    .FontSize(14)
            ]).Padding(new EdgeInsets(10, 0, 10, 0)),
            new Separator()
        ]);
    }

    private static Node ShippingSummary(ShippingAddress address)
    {
        return new Column(spacing: 4, children: [
            new Label("Shipping To")
                .FontSize(14)
                .Bold(),
            new Label(address.Name)
                .FontSize(13),
            new Label(address.Line1)
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            address.Line2.Length > 0
                ? new Label(address.Line2)
                    .FontSize(13)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
                : Node.Empty,
            new Label($"{address.City}, {address.State} {address.PostalCode}")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
        ]);
    }

    private static Node PriceSummary(OrderDraft order)
    {
        return new Column(spacing: 6, children: [
            PriceLine("Subtotal",  order.Subtotal),
            PriceLine("Shipping",  order.ShippingCost),
            PriceLine("Tax",       order.Tax),
            new Separator(),
            PriceLine("Total",     order.Total, bold: true)
        ]);
    }

    private static Node PriceLine(string label, decimal amount, bool bold = false)
    {
        return new Row(children: [
            bold
                ? new Label(label).FontSize(15).Bold().Grow(1)
                : new Label(label).FontSize(13).Color(ThemeSwitcher.ActiveColors.TextMuted).Grow(1),
            bold
                ? new Label($"${amount:F2}").FontSize(15).Bold()
                : new Label($"${amount:F2}").FontSize(13)
        ]);
    }

    private Node ConfirmFooter()
    {
        return new Column(spacing: 8, children: [
            new Button(
                label:   confirming ? "Placing Order..." : "Place Order",
                onClick: () => { _ = OnConfirm(); }
            )
            .Ref(confirmButtonRef)
            .Disabled(confirming)
            .Loading(confirming),
            new Label("Your payment method will be charged upon confirmation.")
                .FontSize(11)
                .Color(ThemeSwitcher.ActiveColors.TextMuted)
                .Alignment(Alignment.Center)
        ]);
    }

    private static Node ErrorBanner(string message)
    {
        var colors = ThemeSwitcher.ActiveColors;
        return new Row(spacing: 12, children: [
            new Label("⚠").FontSize(18).Color(colors.Danger),
            new Label(message).FontSize(13).Color(colors.Danger)
        ])
        .Padding(new EdgeInsets(12, 16, 12, 16))
        .Background(colors.DangerSubtle)
        .CornerRadius(8);
    }

    private static Node LoadingView()
    {
        return new Column(
            spacing: 16,
            mainAxisAlignment:  MainAxisAlignment.Center,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: [
                new Spinner(32),
                new Label("Loading order details...")
                    .FontSize(14)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ]);
    }

    private static Node ErrorView()
    {
        return new Column(
            spacing: 16,
            mainAxisAlignment:  MainAxisAlignment.Center,
            crossAxisAlignment: CrossAxisAlignment.Center,
            children: [
                new Label("⚠").FontSize(48),
                new Label("Failed to load order details")
                    .FontSize(14),
                new Button(
                    label:   "Retry",
                    onClick: () => { }
                )
            ]).Padding(new EdgeInsets(40, 40, 40, 40));
    }

    private async Task OnConfirm()
    {
        if (confirming)
        {
            return;
        }

        confettiOrigin = confirmButtonRef.Bounds.Center;
        confirming = true;
        error      = "";
        Invalidate();

        try
        {
            await Delay(Duration.Ms(1200), LifetimeToken);
            confirmed = true;
            Invalidate();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            error = "Something went wrong. Please try again.";
            Invalidate();
        }
        finally
        {
            confirming = false;
            Invalidate();
        }
    }
}

// ── Order Success View ────────────────────────────────────────────────────────

internal sealed partial class OrderSuccessView : Component
{
    private readonly OrderDraft order;
    private readonly Point confettiOrigin;

    private readonly NodeRef<Node> checkIconRef = new();

    private bool showConfetti = false;

    private readonly AnimatedValue<float> iconScale         = new(0.4f, AnimationModel.Spring.Bouncy);
    private readonly AnimatedValue<float> iconOpacity       = new(0f, AnimationModel.Spring.Standard);
    private readonly AnimatedValue<float> contentOpacity    = new(0f, AnimationModel.Spring.Standard);
    private readonly AnimatedValue<float> contentTranslateY = new(10f, AnimationModel.Spring.Standard);

    public OrderSuccessView(OrderDraft order, Point confettiOrigin)
    {
        this.order          = order;
        this.confettiOrigin = confettiOrigin;
    }

    protected override async Task OnMounted()
    {
        iconScale.Updated += _ => Invalidate();
        iconOpacity.Updated += _ => Invalidate();
        contentOpacity.Updated += _ => Invalidate();
        contentTranslateY.Updated += _ => Invalidate();

        await Task.WhenAll(
            iconScale.AnimateToAsync(1f, AnimationModel.Spring.Bouncy, LifetimeToken),
            iconOpacity.AnimateToAsync(1f, AnimationModel.EaseOut(Duration.Ms(150)), LifetimeToken)
        );

        await Delay(Duration.Ms(100), LifetimeToken);

        await Task.WhenAll(
            contentOpacity.AnimateToAsync(1f, AnimationModel.EaseOut(Duration.Ms(250)), LifetimeToken),
            contentTranslateY.AnimateToAsync(0f, AnimationModel.Spring.Standard, LifetimeToken)
        );

        showConfetti = true;
        Invalidate();
    }

    protected override Node Render()
    {
        return new Column(spacing: 28, crossAxisAlignment: CrossAxisAlignment.Center, children: [
            new Stack(
                new Label("✓")
                    .FontSize(64)
                    .Color(ThemeSwitcher.ActiveColors.Success)
                    .Ref(checkIconRef)
                    .Scale(iconScale.Current)
                    .Opacity(iconOpacity.Current)
                    .Alignment(Alignment.Center),
                showConfetti
                    ? ParticleFactory.Particles(
                          emitter: ParticleEmitter.Burst(count: 120)
                                       .Origin(confettiOrigin),
                          shape:   ParticleShape.Mix(
                                       ParticleShape.Rect(new Size(8, 4)),
                                       ParticleShape.Circle(radius: 3)
                                   ),
                          colors:  [
                                       ThemeSwitcher.Current.Palette.Blue,
                                       ThemeSwitcher.Current.Palette.Green,
                                       ThemeSwitcher.Current.Palette.Yellow,
                                       ThemeSwitcher.Current.Palette.Red
                                   ],
                          physics: ParticlePhysics.Confetti(
                                       gravity: 0.35f,
                                       spread:  360f,
                                       tumble:  true
                                   )
                      )
                    : Node.Empty
            ).Height(120),
            new Column(spacing: 8, crossAxisAlignment: CrossAxisAlignment.Center, children: [
                new Label("Order Placed!")
                    .FontSize(22)
                    .Bold(),
                new Label($"Your order #{order.OrderNumber} has been confirmed.")
                    .FontSize(14)
                    .Color(ThemeSwitcher.ActiveColors.TextMuted)
            ])
            .Opacity(contentOpacity.Current)
            .TranslateY(contentTranslateY.Current),
            OrderNumberBadge(order.OrderNumber)
                .Opacity(contentOpacity.Current)
                .TranslateY(contentTranslateY.Current),
            new Row(spacing: 12, children: [
                new Button(
                    label:   "Track Order",
                    onClick: () => { }
                ).Grow(1),
                new Button(
                    label:   "Continue Shopping",
                    onClick: () => { }
                ).Grow(1)
            ])
            .Opacity(contentOpacity.Current)
        ])
        .Alignment(Alignment.Center)
        .Padding(new EdgeInsets(40, 40, 40, 40));
    }

    private static Node OrderNumberBadge(string orderNumber)
    {
        return new Row(spacing: 8, children: [
            new Label("Order #")
                .FontSize(13)
                .Color(ThemeSwitcher.ActiveColors.TextMuted),
            new Label(orderNumber)
                .FontSize(13)
                .Bold()
        ])
        .Padding(new EdgeInsets(8, 16, 8, 16))
        .Background(ThemeSwitcher.ActiveColors.SurfaceAlt)
        .CornerRadius(16)
        .Alignment(Alignment.Center);
    }
}
