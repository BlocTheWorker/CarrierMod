using Carrier.Behavior.Mission_Controller;
using Carrier.Helper;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Carrier.Behavior {
    public class CarrierCampaignBehavior : CampaignBehaviorBase {
        private CarrierConfig _config;
        private CharacterObject _flagCarrier;
        private CharacterObject FlagCarrier {
            get {
                if(_flagCarrier== null)
                    _flagCarrier = Game.Current.ObjectManager.GetObject<CharacterObject>("standard_banner_carrier");
                return _flagCarrier;
            }
            set {
                _flagCarrier = value;
            } 
        }

        // Get config
        public CarrierCampaignBehavior(CarrierConfig config) {
            _config = config;
            
        }

        public override void RegisterEvents() {
            CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, new Action<FlattenedTroopRoster>(OnPrisonerTaken));
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, new Action<IMission>(this.OnMissionStarted));
            if( _config.USE_REAL_TROOP_SYSTEM) {
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
                CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.AfterSettlementEntered));
            } else {
                CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, new Action(this.OnGameLoadFinished)); 
            }
        }

        private void OnGameLoadFinished()
        {
            // Clean up if we don't want to use troop system.
            try {
                foreach(MobileParty party in Campaign.Current.MobileParties) {
                    if(party.MemberRoster.Contains(FlagCarrier)) {
                        int count = party.MemberRoster.GetTroopCount(FlagCarrier);
                        party.MemberRoster.RemoveTroop(FlagCarrier, count);
                    }
                    if (party.PrisonRoster.Contains(FlagCarrier)) {
                        int count = party.PrisonRoster.GetTroopCount(FlagCarrier);
                        party.PrisonRoster.RemoveTroop(FlagCarrier, count);
                    }
                }
            } catch (Exception e) {

            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            this.AddGameMenus(starter);
        }

        private void AfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero) {
            if (party == null || settlement == null || hero == null || party.IsCaravan || party.IsBandit || party.IsBanditBossParty || party.IsCommonAreaParty || party.IsLeaderless) return;
            if (party.LeaderHero.MapFaction != null && !party.LeaderHero.MapFaction.IsKingdomFaction) return;
            if (party.IsMainParty) return;
            if (!settlement.IsCastle && !settlement.IsTown) return;
            if (FlagCarrier == null) return;
            int reqCount = CalculateHowManyRequired(party);
            if (reqCount < 0 ) {
                party.MemberRoster.AddToCounts(FlagCarrier, reqCount * -1);
                GiveGoldAction.ApplyBetweenCharacters(hero, null, reqCount * -1 * (int)_config.CARRIER_TROOP_COST);
            } else if (reqCount > 0) {
                party.MemberRoster.RemoveTroop(FlagCarrier, reqCount);
                GiveGoldAction.ApplyBetweenCharacters(null, hero, reqCount * (int)_config.CARRIER_TROOP_COST);
            }
        }

        private void OnPrisonerTaken(FlattenedTroopRoster fTroop) {
            // Remove carriers if they somehow end up in prisoner list - they are not actually "soldiers"
            List<FlattenedTroopRosterElement> troopList = fTroop.ToList();
            foreach (FlattenedTroopRosterElement troop in troopList) {
                if(troop.Troop.StringId.Contains("banner_carrier")) {
                    fTroop.Remove(troop.Descriptor);
                }
            }
        }

        // Hook-up the MissionStarted for adding our own behavior
        public void OnMissionStarted(IMission mission) {
            try {
                Mission currrentMission = (Mission)mission;
                if (currrentMission == null || currrentMission.CombatType != Mission.MissionCombatType.Combat || (PlayerEncounter.Battle != null && PlayerEncounter.Battle.IsAlleyFight)) return;
                if (PlayerEncounter.EncounteredBattle == null) return;
                currrentMission.AddMissionBehavior((MissionBehavior)new CarrierMissionController(_config));
                if( _config.USE_UNIT_ORDER_RELAY_SOUNDS)
                currrentMission.AddMissionBehavior((MissionBehavior)new SoldierResponseController(_config));
            } catch { }
        }

        protected void AddGameMenus(CampaignGameStarter campaignGameSystemStarter)
        {
            campaignGameSystemStarter.AddGameMenuOption("castle", "recruit_banner_carrier_mercenaries", "{=NwO0CVzn}Recruit {MEN_COUNT} {MERCENARY_NAME} ({TOTAL_AMOUNT}{GOLD_ICON})", new GameMenuOption.OnConditionDelegate(this.game_menu_castle_recruit_banner_carrier_on_condition), new GameMenuOption.OnConsequenceDelegate(this.game_menu_castle_recruit_banner_carrier_on_consequence), false, 2, false);
        }

        private void game_menu_castle_recruit_banner_carrier_on_consequence(MenuCallbackArgs args)
        {
            int countRequired = 2;
            MobileParty.MainParty.MemberRoster.AddToCounts(FlagCarrier, countRequired, false, 0, 0, true, -1);
            GiveGoldAction.ApplyBetweenCharacters((Hero)null, Hero.MainHero, -(countRequired * (int)_config.CARRIER_TROOP_COST), false);
            GameMenu.SwitchToMenu("castle");
        }

        private bool game_menu_castle_recruit_banner_carrier_on_condition(MenuCallbackArgs args)
        {
            bool canPlayerDo = true;
            args.optionLeaveType = GameMenuOption.LeaveType.RansomAndBribe;
            int countRequired = 2;
            float cost = countRequired * _config.CARRIER_TROOP_COST;
            MBTextManager.SetTextVariable("MEN_COUNT", countRequired);
            MBTextManager.SetTextVariable("MERCENARY_NAME", FlagCarrier.Name, false);
            MBTextManager.SetTextVariable("TOTAL_AMOUNT", countRequired * _config.CARRIER_TROOP_COST);

            if (Hero.MainHero.Gold < cost)
            {
                canPlayerDo = false;
            }

            TextObject disabledText = new TextObject("{=m6uSOtE4}You don't have enough money.");
            if (canPlayerDo)
            {
                int requiredCount = this.CalculateHowManyRequired(MobileParty.MainParty);
                if (requiredCount >= 0)
                {
                    canPlayerDo = false;
                    disabledText = new TextObject("{=FVEvjlis}Party Size Limit Exceeded");
                }
            }

            return MenuHelper.SetOptionProperties(args, canPlayerDo, false, disabledText);
        }


        private int CalculateHowManyRequired(MobileParty party)
        {
            int currentCount = 0;
            if (party.MemberRoster.Contains(FlagCarrier))
            {
                currentCount = party.MemberRoster.GetTroopCount(FlagCarrier);
            }
            FlattenedTroopRoster flatRoster = party.MemberRoster.ToFlattenedRoster();
            Dictionary<FormationClass, int> formationCounts = new Dictionary<FormationClass, int>();
            foreach (FlattenedTroopRosterElement ftre in flatRoster)
            {
                if (formationCounts.ContainsKey(ftre.Troop.DefaultFormationClass))
                {
                    formationCounts[ftre.Troop.DefaultFormationClass] += 1;
                }
                else
                {
                    formationCounts.Add(ftre.Troop.DefaultFormationClass, 1);
                }
            }
            foreach (KeyValuePair<FormationClass, int> keyValue in formationCounts)
            {
                float reqLimitPerFormation = CarrierHelper.MapFormationToConfigCount(keyValue.Key, _config);
                currentCount -= (int)(keyValue.Value / reqLimitPerFormation);
            }
            return currentCount;
        }

        // We really don't need to save anything 
        public override void SyncData(IDataStore dataStore) { }
    }
}
