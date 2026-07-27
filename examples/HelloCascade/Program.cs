// HelloCascade — First real Cascade UI application
//
// Inspired by Golden Example 01 (New User Form), simplified to use
// only the controls we know work end-to-end: Column, Row, Center,
// Label, Button, and reactive state.
//
// This is the first app to open a real GPU-rendered window.

using Cascade.UI;
using Cascade.UI.Backend.Etch;
using HelloCascade;

App.Run<MainView>(config =>
{
    config.UseEtch();
    config.Theme = new AppleTheme(ThemeMode.Dark);
});
