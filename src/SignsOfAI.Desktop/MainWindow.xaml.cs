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

        // Both registered after AddSignsOfAiUi so they win over the browser's defaults.

        // The PDF/ODT/EPUB/RTF extractors are already on disk here, so there is no reason to tell
        // the user to paste their PDF as text.
        services.AddScoped<IDocumentReader, DesktopDocumentReader>();

        // A real folder dialog and a real folder path — the thing a browser tab is never given.
        services.AddScoped<IFolderBatch, DesktopFolderBatch>();

        // Native HTTP: Ollama on localhost is simply reachable, with no CORS workaround to explain.
        services.AddSingleton(HostCapabilities.Desktop);

        // The XAML binds Services="{DynamicResource services}", so the provider has to be in
        // Resources before InitializeComponent builds the visual tree.
        Resources.Add("services", services.BuildServiceProvider());

        InitializeComponent();
    }
}
