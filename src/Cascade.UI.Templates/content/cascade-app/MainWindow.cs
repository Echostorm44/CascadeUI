using Cascade.UI;

namespace CascadeApp;

public class MainWindow : Component
{
//#if (UseSample)
    protected override Node Render() => new SamplePage();
//#elseif (UseCounter)
    protected override Node Render() => new CounterPage();
//#else
    protected override Node Render() => new BlankPage();
//#endif
}
