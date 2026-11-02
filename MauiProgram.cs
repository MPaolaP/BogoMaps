using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace BogoMaps
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("PlusJakartaSans-Regular.ttf", "JakartaRegular");
                    fonts.AddFont("PlusJakartaSans-Medium.ttf", "JakartaMedium");
                    fonts.AddFont("PlusJakartaSans-SemiBold.ttf", "JakartaSemiBold");
                    fonts.AddFont("PlusJakartaSans-Bold.ttf", "JakartaBold");
                    fonts.AddFont("PlusJakartaSans-ExtraBold.ttf", "JakartaExtraBold");
                    fonts.AddFont("Inter_24pt-Medium.ttf", "InterMedium");
                    fonts.AddFont("Inter_24pt-SemiBold.ttf", "InterSemiBold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
