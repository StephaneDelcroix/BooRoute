using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;

namespace BooRoute.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args) => AppBuilder.Configure<AvaloniaApp>()
        .WithInterFont()
        .With(new FontManagerOptions
        {
            FontFallbacks =
            [
                new FontFallback { FontFamily = new FontFamily("avares://BooRoute.Browser/Assets/Fonts#Noto Color Emoji") },
                new FontFallback { FontFamily = new FontFamily("avares://BooRoute.Browser/Assets/Fonts#Noto Sans Symbols 2") }
            ]
        })
        .StartBrowserAppAsync("out");
}
