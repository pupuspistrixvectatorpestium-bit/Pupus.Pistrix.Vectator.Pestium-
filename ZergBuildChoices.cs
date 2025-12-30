using SC2APIProtocol;
using Sharky.Builds;
using Sharky.Builds.Zerg;
//using Maw;
//using Maw.Builds;
//using Maw.Builds.Zerg;
using Sharky.DefaultBot;
using SharkyZergExampleBot.Builds;
using System.Collections.Generic;

namespace SharkyZergExampleBot
{
    public class ZergBuildChoices
    {
        public BuildChoices BuildChoices { get; private set; }

        public ZergBuildChoices(DefaultSharkyBot defaultSharkyBot)
        {
            var zerglingRush = new BasicZerglingRush(defaultSharkyBot);
            var mutaliskRush = new MutaliskRush(defaultSharkyBot);
            var buildTest = new BuildTest(defaultSharkyBot);

            var builds = new Dictionary<string, ISharkyBuild>
            {
                [zerglingRush.Name()] = zerglingRush,
                [mutaliskRush.Name()] = mutaliskRush,
                [buildTest.Name()] = buildTest
            };

            var versusEverything = new List<List<string>>
            {
                //new List<string> { zerglingRush.Name(), mutaliskRush.Name() },
                //new List<string> { mutaliskRush.Name() },
                new List<string> { buildTest.Name() } ,  
            };

            var transitions = new List<List<string>>
            {
                new List<string> { buildTest.Name() },
            };

            var buildSequences = new Dictionary<string, List<List<string>>>
            {
                [Race.Terran.ToString()] = versusEverything,
                [Race.Zerg.ToString()] = versusEverything,
                [Race.Protoss.ToString()] = versusEverything,
                [Race.Random.ToString()] = versusEverything,
                ["Transition"] = transitions,
            };

            BuildChoices = new BuildChoices { Builds = builds, BuildSequences = buildSequences };

            AddZergTasks(defaultSharkyBot);
        }

        void AddZergTasks(DefaultSharkyBot defaultSharkyBot)
        {

        }
    }
}
