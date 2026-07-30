using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SignsOfAI.UI;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        // Right-click → Inspect inside the WebView. Debug builds only: shipping it would put dev
        // tools in front of end users.
        services.AddBlazorWebViewDeveloperTools();
#endif

        // The single registration call the web app also makes — see SignsOfAI.UI/UiServices.cs.
        // Anything the interface needs arrives here automatically, so a service added for the web
        // can never be missing on the desktop.
        services.AddSignsOfAiUi();

        // Registered after AddSignsOfAiUi so it wins over the browser's reader: on the desktop the
        // PDF/ODT/EPUB/RTF extractors are already on disk, so there is no reason to tell the user to
        // paste their PDF as text.
        services.AddScoped<IDocumentReader, DesktopDocumentReader>();

        // The XAML binds Services="{DynamicResource services}", so the provider has to be in
        // Resources before InitializeComponent builds the visual tree.
        Resources.Add("services", services.BuildServiceProvider());

        InitializeComponent();
    }
}
