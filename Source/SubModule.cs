using Carrier.Behavior;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Carrier
{
    public class CarrierConfig
    {
        public float MORALE_EFFECT = 10, 
                     MORALE_RADIUS = 5, 
                     CARRIER_TROOP_COST = 10,
                     MAXIMUM_MORALE_WHILE_AROUND=40;

        public bool USE_TORCH_IN_NIGHT_BATTLE = false,
                    ALLOW_NON_NOBLES = false,
                    ALLOW_IN_HIDEOUT = false,
                    ALLOW_SIEGE_DEFENDER_BANNERS = false,
                    ALLOW_SIEGE_ATTACKER_BANNERS = true,
                    ALLOW_RAID_ATTACKER_BANNERS = true,
                    USE_TIER_BASED_BANNERMAN = true,
                    ALLOW_MORALE_BOOST_FOR_WALLS = true,
                    ALLOW_MORALE_BOOST_MESSAGE_FOR_WALLS = true,
                    ALLOW_RAID_DEFENDER_BANNERS = false,
                    USE_UNIT_ORDER_RELAY_SOUNDS = true,
                    GIVE_SWORD_TO_HAND= true,
                    USE_REAL_TROOP_SYSTEM = false;

        public int PER_INFANTRY = 5,
                   PER_CAVALRY = 5,
                   PER_ARCHER = 5,
                   PER_HEAVY_CAVALRY = 5,
                   PER_HORSE_ARCHER = 5,
                   PER_SKIRMISHER = 5,
                   PER_HEAVY_INFANTRY = 5,
                   PER_LIGHT_CAV = 2;
    }

    public class SubModule : MBSubModuleBase
    {
        private CarrierConfig config;
        protected override void OnSubModuleLoad() {
            config = new CarrierConfig();
            JObject readConfig = ReadConfig();
            FillConfig(readConfig);
        }

        private JObject ReadConfig() {
            try { 
                return JObject.Parse(System.IO.File.ReadAllText(@"..\..\Modules\Carrier\Config.json"));
            } catch (System.Exception e) {
                return null; 
            }
        }

        private void FillConfig(JObject conf) {
            if (conf == null) return;

            JToken extras = conf.SelectToken("Extra");
            if(extras != null ) {
                config.USE_TORCH_IN_NIGHT_BATTLE = (bool)extras["AlsoUseTorchAtNight"];
                config.ALLOW_NON_NOBLES = (bool)extras["AllowNonNobleArmiesToCarryBanner"];
                config.USE_TIER_BASED_BANNERMAN = (bool)extras["UseTierBasedBannerman"];
                config.USE_UNIT_ORDER_RELAY_SOUNDS = (bool)extras["UseResponsiveUnits"];
            }
            
            if (conf.ContainsKey("Banner"))
            {
                config.ALLOW_SIEGE_ATTACKER_BANNERS = (bool)conf.SelectToken("Banner.AllowSiegeAttackers");
                config.ALLOW_SIEGE_DEFENDER_BANNERS = (bool)conf.SelectToken("Banner.AllowSiegeDefenders");
                config.ALLOW_RAID_ATTACKER_BANNERS = (bool)conf.SelectToken("Banner.AllowRaidAttackers");
                config.ALLOW_RAID_DEFENDER_BANNERS = (bool)conf.SelectToken("Banner.AllowRaidDefenders");
                config.ALLOW_MORALE_BOOST_FOR_WALLS = (bool)conf.SelectToken("Banner.AllowMoraleBoostWhenBannermenReachWalls");
                config.ALLOW_MORALE_BOOST_MESSAGE_FOR_WALLS = (bool)conf.SelectToken("Banner.AllowBannermenReachedMessageAndSound");
                config.USE_REAL_TROOP_SYSTEM = (bool)conf.SelectToken("Banner.UseRealTroopSystem");
                config.GIVE_SWORD_TO_HAND = (bool)conf.SelectToken("Banner.GiveSwordToHand");

                if (int.TryParse((string)conf["Banner"]["MoraleRadius"], out int moraleRadius)) config.MORALE_RADIUS = moraleRadius;
                if (int.TryParse((string)conf["Banner"]["MoraleDropWhenBannermanKilled"], out int moraleEffect)) config.MORALE_EFFECT = moraleEffect;
                if (int.TryParse((string)conf["Banner"]["CarrierTroopCost"], out int carrierCost)) config.CARRIER_TROOP_COST = carrierCost;
                if (int.TryParse((string)conf["Banner"]["MaximumMoraleWhenAroundAllyBannerman"], out int moraleMax)) config.MAXIMUM_MORALE_WHILE_AROUND = moraleMax;
                if (int.TryParse((string)conf["Banner"]["PerInfantry"], out int perInfantry)) config.PER_INFANTRY = perInfantry;
                if (int.TryParse((string)conf["Banner"]["PerCavalry"], out int PerCavalry)) config.PER_CAVALRY = PerCavalry;
                if (int.TryParse((string)conf["Banner"]["PerArcher"], out int PerArcher)) config.PER_ARCHER = PerArcher;
                if (int.TryParse((string)conf["Banner"]["PerHorseArcher"], out int PerHorseArcher)) config.PER_HORSE_ARCHER = PerHorseArcher;
                if (int.TryParse((string)conf["Banner"]["PerHeavyCavalry"], out int PerHeavyCavalry)) config.PER_HEAVY_CAVALRY = PerHeavyCavalry;
                if (int.TryParse((string)conf["Banner"]["PerHeavyInfantry"], out int PerHeavyInfantry)) config.PER_HEAVY_INFANTRY = PerHeavyInfantry;
                if (int.TryParse((string)conf["Banner"]["PerSkirmisher"], out int PerSkirmisher)) config.PER_SKIRMISHER = PerSkirmisher;
                if (int.TryParse((string)conf["Banner"]["PerLightCavalry"], out int PerLightCavalry)) config.PER_LIGHT_CAV = PerLightCavalry;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject) {
            if (gameStarterObject.GetType() == typeof(CampaignGameStarter)) {
                base.OnCampaignStart(game, gameStarterObject);
                CampaignGameStarter starter = (CampaignGameStarter)gameStarterObject;
                starter.AddBehavior(new CarrierCampaignBehavior(config));
            }
        }
    }
}