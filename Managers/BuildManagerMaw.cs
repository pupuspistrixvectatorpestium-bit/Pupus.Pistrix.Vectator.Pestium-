using Sharky;
using Sharky.Managers;
using Sharky.DefaultBot;

namespace Maw.Managers
{
    /// <summary>
    /// Thin adapter to construct Sharky BuildManager from a DefaultSharkyBot.
    /// Keep Maw-specific adapters here; do not duplicate Sharky logic.
    /// </summary>
    public class BuildManagerMaw
    {
        public BuildManager BuildManager { get; }

        public BuildManagerMaw(DefaultSharkyBot bot)
        {
            BuildManager = new BuildManager(bot);
        }
    }
}