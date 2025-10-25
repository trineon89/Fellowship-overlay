using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.UI.WPF;

namespace MeikoBuffOverlay.Services
{
    public static class Updater
    {
        private const string AppCastUrl = "https://trineon89.github.io/Fellowship-overlay/appcast.xml";

        public static Sparkle Create()
        {
            var sparkle = new Sparkle(AppCastUrl)
            {
                UIFactory = new NetSparkleUpdater.UI.WPF.UIFactory(iconBitmap: null),
                SecurityProtocolType = System.Net.SecurityProtocolType.Tls12
            };
            sparkle.CloseApplication += (sender, args) =>
            {
                System.Windows.Application.Current.Shutdown();
            };
            return sparkle;
        }
    }
}
