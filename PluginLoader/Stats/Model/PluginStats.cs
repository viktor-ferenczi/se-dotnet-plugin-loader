using avaness.PluginLoader.Data;
using System.Collections.Generic;

namespace avaness.PluginLoader.Stats.Model
{
    // Statistics for all plugins
    public class PluginStats
    {
        // Key: pluginId
        public Dictionary<string, PluginStat> Stats { get; set; } = new Dictionary<string, PluginStat>();

        // Token the player is required to present for voting (making it harder to spoof votes)
        public string VotingToken { get; set; }

        public PluginStat GetStatsForPlugin(PluginData data)
        {
            if (Stats.TryGetValue(data.Id, out PluginStat result))
                return result;
            return new PluginStat();
        }
    }
}