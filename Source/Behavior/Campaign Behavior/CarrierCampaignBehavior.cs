using Carrier.Behavior.MissionController;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Carrier.Behavior {
    public class CarrierCampaignBehavior : CampaignBehaviorBase {
        private CarrierConfig _config;

        // Get config
        public CarrierCampaignBehavior(CarrierConfig config) {
            _config = config;
        }

        public override void RegisterEvents() {
            CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(this, new Action<FlattenedTroopRoster>(OnPrisonerTaken));
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, new Action<IMission>(this.OnMissionStarted));
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
            Mission currrentMission = (Mission)mission;
            if (currrentMission.CombatType != Mission.MissionCombatType.Combat) return;
            if (PlayerEncounter.EncounteredBattle == null) return;
            currrentMission.AddMissionBehaviour((MissionBehaviour)new CarrierMissionBehavior(_config));
        }

        // We really don't need to save anything 
        public override void SyncData(IDataStore dataStore) { }
    }
}
