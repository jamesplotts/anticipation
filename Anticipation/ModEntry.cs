using Railroader.Modding;

namespace Anticipation
{
    // Entry point for Railroader's built-in mod loader (0.6+).
    public sealed class ModEntry : IModEntryPoint, IWorldLifecycle
    {
        internal static IModLogger Log;

        public void OnModLoad(IModContext context)
        {
            Log = context.Log;
            OpenButton.Install();
        }

        public void OnModUnload()
        {
            AnticipationWindow.CloseIfOpen();
            OpenButton.Remove();
            Log = null;
        }

        public void OnWorldLoad()
        {
        }

        public void OnWorldUnload()
        {
            AnticipationWindow.CloseIfOpen();
        }
    }
}
