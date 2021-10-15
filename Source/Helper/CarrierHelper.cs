using Carrier.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Carrier.Helper
{
    class CarrierHelper
    {


        /// <summary>
        /// Map Formation to preset Config "Limit Per Formation" count
        /// </summary>
        /// <param name="f">Formation you want to convert. Unrecognized will return 5</param>
        /// <returns></returns>
        public static float MapFormationToConfigCount(FormationClass f, CarrierConfig _config)
        {
            switch (f)
            {
                case FormationClass.Infantry:
                    return _config.PER_INFANTRY;
                case FormationClass.HorseArcher:
                    return _config.PER_HORSE_ARCHER;
                case FormationClass.Ranged:
                    return _config.PER_ARCHER;
                case FormationClass.Skirmisher:
                    return _config.PER_SKIRMISHER;
                case FormationClass.Cavalry:
                    return _config.PER_CAVALRY;
                case FormationClass.HeavyInfantry:
                    return _config.PER_HEAVY_INFANTRY;
                case FormationClass.HeavyCavalry:
                    return _config.PER_HEAVY_CAVALRY;
                case FormationClass.LightCavalry:
                    return _config.PER_LIGHT_CAV;
                default:
                    return 5;
            }
        }

        /// <summary>
        /// Get basic troop equipment. If tier based is allowed, this will return better equipment with respect to upgrade path of troop 
        /// </summary>
        /// <param name="pb"></param>
        /// <returns></returns>
        public static Equipment GetBasicTroopEquipment(PartyBase pb, CarrierConfig _config)
        {
            if (pb != null && pb.MapFaction != null)
            {
                if (pb.MapFaction.BasicTroop != null)
                {
                    CharacterObject basicTroop = pb.Culture.BasicTroop;
                    Equipment resEq = basicTroop.RandomBattleEquipment;
                    if (_config.USE_TIER_BASED_BANNERMAN)
                    {
                        if (pb.LeaderHero != null && pb.LeaderHero.Clan != null)
                        {
                            int tier = pb.LeaderHero.Clan.Tier;
                            while (basicTroop.Tier != tier && basicTroop != null)
                            {
                                CharacterObject[] targets = basicTroop.UpgradeTargets;
                                if (targets.Length == 0)
                                    break;
                                basicTroop = basicTroop.UpgradeTargets.First();
                                if (basicTroop != null)
                                    resEq = basicTroop.RandomBattleEquipment;
                            }
                        }
                    }
                    return resEq;
                }
            }
            return null;
        }


        public static bool IsHideout()
        {
            return (PlayerEncounter.Battle != null && PlayerEncounter.Battle.IsHideoutBattle);
        }

        public static bool IsSiegeAssault()
        {
            return (PlayerEncounter.Battle != null && PlayerEncounter.Battle.IsSiegeAssault);
        }

        public static bool ShouldUseTorch(CarrierConfig _config)
        {
            return (_config.USE_TORCH_IN_NIGHT_BATTLE && (Mission.Current.Scene.TimeOfDay >= 17 || Mission.Current.Scene.TimeOfDay <= 4f));
        }
    }
}
