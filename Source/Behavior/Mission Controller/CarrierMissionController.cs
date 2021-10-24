using Carrier.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Carrier.Behavior
{
    public class CarrierMissionController : MissionLogic
    {
        /// <summary>
        /// Simple struct to keep BattleSide - PartyBase relation. We need PartyBase for BattleCombatant ( AgentOrigin )
        /// </summary>
        struct SideAndParty {
            public BattleSideEnum Side;
            public PartyBase PartyBase;
        }

        private const float OCCASIONAL_TICK = 5; 
        private bool _isSpawnedTroops = false, _isAnyBannermenReachedWalls = false, _didSomehowCausedError = false;
        private List<SideAndParty> _bannerParties;
        private PartyBase _reservedReinforceDefender, _reservedReinforceAttacker;
        private Dictionary<Agent, GameEntity> _bannerBearersAndObject;
        private CarrierConfig _config;
        private float _lastTick;
        private Vec3 _randomWind;

        public CarrierMissionController(CarrierConfig config) {
            _config = config;
        }

        public override void AfterStart()
        {
            try
            {
                _lastTick = Mission.Current.Time;
                _bannerParties = new List<SideAndParty>();
                _bannerBearersAndObject = new Dictionary<Agent, GameEntity>();

                if (!Mission.Current.Scene.IsAtmosphereIndoor && !CarrierHelper.IsHideout())
                {
                    // Get spawnlogic and register it's phase change events. Although currently I have no good solution to create bannerman on reinforce waves
                    MissionAgentSpawnLogic spawnLogicBehavior = Mission.Current.GetMissionBehaviour<MissionAgentSpawnLogic>();
                    spawnLogicBehavior.AddPhaseChangeAction(BattleSideEnum.Attacker, new OnPhaseChangedDelegate(this.OnAttackerPhaseChanged));
                    spawnLogicBehavior.AddPhaseChangeAction(BattleSideEnum.Defender, new OnPhaseChangedDelegate(this.OnDefenderPhaseChanged));
                }

                // Small wind effect for fun
                _randomWind = new Vec3(MBRandom.RandomFloatRanged(-3f, 3f), MBRandom.RandomFloatRanged(-3f, 3f), MBRandom.RandomFloatRanged(-1f, 1f));

                // Get all the parties involved
                List<PartyBase> partyList = PlayerEncounter.Battle.InvolvedParties.ToList();
                partyList.OrderBy(a => a.NumberOfHealthyMembers);

                MapEvent currentMapEvent = PlayerEncounter.EncounteredBattle;
                foreach (PartyBase pb in partyList)
                {
                    // Check certain conditions with respect to config file
                    if (currentMapEvent.IsRaid)
                    {
                        if (pb.Side == BattleSideEnum.Attacker && !_config.ALLOW_RAID_ATTACKER_BANNERS)
                            continue;
                        else if ((pb.Side == BattleSideEnum.Defender && !_config.ALLOW_RAID_DEFENDER_BANNERS))
                            continue;
                    }

                    if (currentMapEvent.IsSiegeAssault)
                    {
                        if (pb.Side == BattleSideEnum.Attacker && !_config.ALLOW_SIEGE_ATTACKER_BANNERS)
                            continue;
                        else if ((pb.Side == BattleSideEnum.Defender && !_config.ALLOW_SIEGE_DEFENDER_BANNERS))
                            continue;
                    }

                    if (currentMapEvent.IsHideoutBattle && !_config.ALLOW_IN_HIDEOUT)
                    {
                        continue;
                    }

                    // IF Non-Nobles are allowed and party is mobile and not bandit 
                    // OR Party has MapFaction, NOT Villager or Caravan or Bandit or Garrison
                    if ((_config.ALLOW_NON_NOBLES && (!pb.MapFaction.IsBanditFaction && pb.IsMobile)) ||
                         (pb.MapFaction != null && (!pb.MapFaction.IsBanditFaction && pb.IsMobile && !pb.MobileParty.IsCaravan && !pb.MobileParty.IsVillager && !pb.MobileParty.IsGarrison)))
                    {
                        SideAndParty sp = new SideAndParty { PartyBase = pb, Side = pb.Side };
                        _bannerParties.Add(sp);
                        if (pb.Side == BattleSideEnum.Attacker && _reservedReinforceAttacker == null) _reservedReinforceAttacker = pb;
                        if (pb.Side == BattleSideEnum.Defender && _reservedReinforceDefender == null) _reservedReinforceDefender = pb;
                    }
                }
            } catch ( Exception e) {
                // General exception catcher
                _didSomehowCausedError = true;
            }
        }


        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_didSomehowCausedError) return;
            if( !_isSpawnedTroops) {
                foreach (Team team in Mission.Teams) {
                    if(team.ActiveAgents.Count > 0) {
                        _isSpawnedTroops = true;
                        if( _config.USE_REAL_TROOP_SYSTEM)
                        {
                            this.AssignAndChangeExistingBannerment(team);
                        } else
                        {
                            this.CreateCarriersForTeam(team);
                        }
                    }                    
                }
            }

            if (OCCASIONAL_TICK < Mission.Current.Time - _lastTick)
            {
                _lastTick = Mission.Current.Time;
                InternalOccasionalTick();
            }
        }

        
        private void AssignAndChangeExistingBannerment(Team team)
        {
            Formation[] formations = team.Formations.ToArray();
            Agent[] activeAgents = team.ActiveAgents.ToArray();
            MBQueue<Agent> agents = new MBQueue<Agent>();
            Equipment troopEquipment = CarrierHelper.GetBasicTroopEquipment(GetPartyBaseForTeam(team), _config);
            foreach (Agent agent in activeAgents)
            {
                if ( agent.IsHuman && agent.Character.StringId == "standard_banner_carrier")
                {
                    _bannerBearersAndObject.Add(agent, null);   
                    
                    Equipment eq = new Equipment(agent.Character.Equipment);
                    if (troopEquipment != null) {
                        for (int k = (int)EquipmentIndex.ArmorItemBeginSlot; k < (int)EquipmentIndex.ArmorItemEndSlot - 1; k++) {
                            eq.AddEquipmentToSlotWithoutAgent((EquipmentIndex)k, troopEquipment.GetEquipmentFromSlot((EquipmentIndex)k));
                        }
                    }
                    AgentBuildData newdata = new AgentBuildData(agent.Origin);
                    newdata.Equipment(eq);
                    agent.ResetAgentProperties();
                    agent.InitializeAgentProperties(eq, newdata);
                    agent.UpdateAgentProperties();
                    agent.EquipItemsFromSpawnEquipment();
                    agent.UpdateSpawnEquipmentAndRefreshVisuals(eq);
                    if (!CarrierHelper.ShouldUseTorch(_config))
                    {
                        agent.RemoveEquippedWeapon(EquipmentIndex.Weapon1);
                        if (_config.GIVE_SWORD_TO_HAND) {
                            ItemObject bluntSword = Game.Current.ObjectManager.GetObject<ItemObject>("carrier_cheap_sword");
                            MissionWeapon misWep = new MissionWeapon(bluntSword, null, null);
                            agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon1, ref misWep);
                        }
                    }
                    else
                    {
                        agent.AddPrefabComponentToBone("torch_burning_prefab", Game.Current.HumanMonster.MainHandItemBoneIndex);
                        Light pointLight = Light.CreatePointLight(10f);
                        pointLight.SetLightFlicker(1, 0.8f);
                        pointLight.Intensity = 70f;
                        pointLight.LightColor = new Vec3(1f, 0.68f, 0.29f, -1f);
                        GameEntity lightEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                        lightEntity.AddLight(pointLight);
                        lightEntity.SetLocalPosition(new Vec3(0.5f, 1f, 1.5f));
                        agent.AgentVisuals.AddChildEntity(lightEntity);
                        _bannerBearersAndObject[agent] = lightEntity;
                    }
                    agent.AgentVisuals.SetClothWindToWeaponAtIndex(_randomWind, true, EquipmentIndex.Weapon0);
                    agents.Enqueue(agent);
                }
            }

            foreach( Formation f in formations) {
                int count = f.CountOfUnits / (int)CarrierHelper.MapFormationToConfigCount(f.FormationIndex, _config);
                for( int i=0; i < count; i++) {
                    if( agents.Count != 0){
                        Agent a = agents.Dequeue();
                        a.Formation = f;
                        a.TeleportToPosition(f.CurrentPosition.ToVec3());
                        if (CarrierHelper.IsFormationMounted(f)) {
                            try {
                                Agent randomAgent = f.GetUnitWithIndex(MBRandom.RandomInt(f.CountOfUnits - 1));
                                MatrixFrame globalFrame = a.Frame;
                                ItemRosterElement itemRosterElement1 = new ItemRosterElement(randomAgent.Character.Equipment.Horse,1);
                                ItemRosterElement itemRosterElement2 = new ItemRosterElement(randomAgent.Character.Equipment[EquipmentIndex.HorseHarness],1);

                                globalFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                                Mission current = Mission.Current;
                                ItemRosterElement rosterElement = itemRosterElement1;
                                ItemRosterElement harnessRosterElement = itemRosterElement2;
                                ref Vec3 local1 = ref globalFrame.origin;
                                Vec2 asVec2 = globalFrame.rotation.f.AsVec2;
                                ref Vec2 local2 = ref asVec2;
                                f.RidingOrder = RidingOrder.RidingOrderDismount;
                                a.SetAgentFlags(a.GetAgentFlags() | AgentFlag.CanRide);
                                Agent horsie = current.SpawnMonster(rosterElement, harnessRosterElement, in local1, in local2, -1);
                                horsie.SetAgentFlags(horsie.GetAgentFlags() | AgentFlag.Mountable);
                                horsie.SetAgentDrivenPropertyValueFromConsole(DrivenProperty.MountDifficulty, 0);
                                a.Mount(horsie);
                                f.RidingOrder = RidingOrder.RidingOrderMount;
                            } catch (Exception e) {

                            }
                        }
                    }
                }
                if (agents.Count == 0) break;
            }

        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (_didSomehowCausedError) return;
            if ( agentState == AgentState.Killed || agentState == AgentState.Unconscious) {
                if (_bannerBearersAndObject.ContainsKey(affectedAgent)) {
                    try {
                        if(CarrierHelper.ShouldUseTorch(_config)) {
                            GameEntity entity = _bannerBearersAndObject[affectedAgent];
                            Light light = entity.GetLight();
                            light.Dispose();
                            entity.Remove(0);
                        }
                    } catch { }
                    if (CarrierHelper.IsHideout()) return;
                    Agent[] agentsAround = Mission.Current.GetNearbyAllyAgents(affectedAgent.Position.AsVec2, this._config.MORALE_RADIUS, affectedAgent.Team).ToArray();
                    foreach(Agent a in agentsAround) {
                        if(!_bannerBearersAndObject.ContainsKey(a))
                            a.SetMorale(a.GetMorale() - this._config.MORALE_EFFECT);
                    }
                }
            }
        }

        //// Currently not used until I find a proper way to handle this
        private void OnAttackerPhaseChanged() {
            HandleReinforcement(Mission.Current.AttackerTeam);
        }
        private void OnDefenderPhaseChanged() {
            HandleReinforcement(Mission.Current.DefenderTeam);
        }
        private void HandleReinforcement(Team team) {
        }
        ////////////////////////////////////////////////////////////

        /// <summary>
        /// Occasional Tick. Only gets ticked once OCCASIONAL_TICK is elapsed. Triggered by built-in OnTick
        /// </summary>
        private void InternalOccasionalTick() {
            if (CarrierHelper.IsHideout()) return;
            // Check all bannermen, update the agent morale around them until it hits the maximum
            foreach (KeyValuePair<Agent, GameEntity> pair in _bannerBearersAndObject) {
                Agent bannerAgent = pair.Key;
                if (!bannerAgent.IsActive() || bannerAgent.Team == null || bannerAgent.Health <= 0) continue;

                if (_config.ALLOW_MORALE_BOOST_FOR_WALLS) {
                    if (CarrierHelper.IsSiegeAssault() && bannerAgent.Team.IsAttacker && !_isAnyBannermenReachedWalls && IsAgentStandingOnWalls(bannerAgent)) {
                        _isAnyBannermenReachedWalls = true;
                        if ( _config.ALLOW_MORALE_BOOST_MESSAGE_FOR_WALLS) {
                            if( Mission.Current.MainAgent != null && 
                                Mission.Current.MainAgent.Team != null && 
                                Mission.Current.MainAgent.Team.Side == BattleSideEnum.Attacker) {
                                InformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject("{=BLCCliLPkWgI}Your banner has reached the walls!"), 500, bannerAgent.Character, "event:/alerts/report/battle_winning");
                            } else
                            {
                                InformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject("{=BLCCOJoEzcxN}Enemy banner has reached the walls!"), 500, bannerAgent.Character, "event:/alerts/report/battle_losing");
                            }
                        }
                        foreach(Agent a in bannerAgent.Team.ActiveAgents) {
                            a.SetMorale(100f);
                        }
                    }
                }

                Agent[] agentsAround = Mission.Current.GetNearbyAllyAgents(bannerAgent.Position.AsVec2, this._config.MORALE_RADIUS, bannerAgent.Team).ToArray();
                foreach (Agent a in agentsAround)  {
                    float currentMorale = a.GetMorale();
                    if(currentMorale < _config.MAXIMUM_MORALE_WHILE_AROUND) {
                        float newMorale = Math.Min(currentMorale, currentMorale + this._config.MORALE_EFFECT);
                        a.SetMorale(newMorale);
                        if( a.GetMorale() > 60) {
                            a.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Raycasts from agent to ground, checks if ray hits any wall objects
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        private bool IsAgentStandingOnWalls(Agent agent)
        {
            try {
                Ray ray = new Ray(agent.Position, Vec3.Up * -1, 150);
                GameEntity[] entityArray = new GameEntity[32];
                Intersection[] intersectionArray = new Intersection[32];
                UIntPtr[] entityId = new UIntPtr[32];
                float length = Mission.Current.Scene.SelectEntitiesCollidedWith(ref ray, entityArray, intersectionArray, entityId);

                bool result = false;

                foreach (GameEntity entity in entityArray) {
                    if (entity != null && entity.Name.Contains("_castle_")) {
                        return true;
                    }
                }

                return result;
            }  catch (Exception e) {
                return false;
            }
        }

        /// <summary>
        /// Return PartyBase with respect to team side. This won't return 100% correct AgentOrigin. 
        /// </summary>
        /// <param name="team"></param>
        /// <returns>PartyBase for Team's Side</returns>
        private PartyBase GetPartyBaseForTeam(Team team) {
            SideAndParty[] tmp = _bannerParties.ToArray();
            foreach ( SideAndParty sp in tmp) {
                if (team != null && sp.Side == team.Side) {
                    _bannerParties.Remove(sp);
                    return sp.PartyBase;
                }
            }
            return null;
        }

        private void CreateCarriersForTeam(Team team)
        {
            Formation[] formations = team.Formations.ToArray();
            PartyBase pb = GetPartyBaseForTeam(team);
            if (pb == null) return;
            foreach ( Formation f in formations) {
                int count = f.CountOfUnits / (int)CarrierHelper.MapFormationToConfigCount(f.FormationIndex, _config);
                if (CarrierHelper.IsHideout())
                {
                    if (f.FormationIndex == FormationClass.Infantry && team.IsAttacker) count = 2;
                    else count = 0;
                }
                int spread = f.CountOfUnits /(count > 0 ? count: 1);
                for (int i = 0; i < count; i++) {
                    Agent randomAgent = f.GetUnitWithIndex(MBRandom.RandomInt(f.CountOfUnits - 1));
                    CharacterObject flagCarrier = Game.Current.ObjectManager.GetObject<CharacterObject>("standard_banner_carrier");
                    AgentBuildData agentBuildData = new AgentBuildData(flagCarrier);
                    PartyAgentOrigin combatant = new PartyAgentOrigin(pb, flagCarrier);
                    agentBuildData.TroopOrigin(combatant);
                    agentBuildData.ClothingColor1(randomAgent.Origin.FactionColor);
                    agentBuildData.ClothingColor2(randomAgent.Origin.FactionColor2);
                    agentBuildData.Banner(randomAgent.Origin.Banner);
                    Equipment eq = new Equipment(flagCarrier.RandomBattleEquipment);
                    Equipment basicEq = CarrierHelper.GetBasicTroopEquipment(pb, _config);
                    if (basicEq != null) {
                        for (int k = (int)EquipmentIndex.ArmorItemBeginSlot; k < (int)EquipmentIndex.ArmorItemEndSlot - 1; k++) {
                            eq.AddEquipmentToSlotWithoutAgent((EquipmentIndex)k, basicEq.GetEquipmentFromSlot((EquipmentIndex)k));
                        }
                    }
                    if (CarrierHelper.IsFormationMounted(f)) {
                        eq.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, randomAgent.Character.Equipment.Horse);
                    }
                    agentBuildData.Equipment(eq);
                    agentBuildData.Formation(f);
                    agentBuildData.Team(team);
                    Vec3 newPos = new Vec3(randomAgent.Position.X + 1f, randomAgent.Position.Y, randomAgent.Position.Z);
                    int newCount = f.CountOfUnits + 1;
                    Agent agent = this.SpawnTroop(agentBuildData, team.IsPlayerTeam, true, true, newCount, (i * spread), true, true, newPos, f.Direction, null);
                    //Agent agent = Mission.Current.SpawnTroop(agentBuildData.AgentOrigin, team.IsPlayerTeam, true, true, false, false, newCount, (i * spread), true, true, false, formPos.ToVec3(), formPos.Normalized(), (string)null);
                    agent.FadeIn();
                    _bannerBearersAndObject.Add(agent, null);
                    if ( !CarrierHelper.ShouldUseTorch(_config) ) {
                        agent.RemoveEquippedWeapon(EquipmentIndex.Weapon1);
                        if (_config.GIVE_SWORD_TO_HAND) {
                            ItemObject bluntSword = Game.Current.ObjectManager.GetObject<ItemObject>("carrier_cheap_sword");
                            MissionWeapon misWep = new MissionWeapon(bluntSword, null, null);
                            agent.EquipWeaponWithNewEntity(EquipmentIndex.Weapon1, ref misWep);
                        }
                    } else {
                        agent.AddPrefabComponentToBone("torch_burning_prefab", Game.Current.HumanMonster.MainHandItemBoneIndex);
                        Light pointLight = Light.CreatePointLight(10f);
                        pointLight.SetLightFlicker(1, 0.8f);
                        pointLight.Intensity = 70f;
                        pointLight.LightColor = new Vec3(1f, 0.68f, 0.29f, -1f);
                        GameEntity lightEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                        lightEntity.AddLight(pointLight);
                        lightEntity.SetLocalPosition(new Vec3(0.5f, 1f, 1.5f + (CarrierHelper.IsFormationMounted(f) ? 1:0)));
                        agent.AgentVisuals.AddChildEntity(lightEntity);
                        _bannerBearersAndObject[agent] = lightEntity;
                    }
                    agent.AgentVisuals.SetClothWindToWeaponAtIndex(_randomWind, true, EquipmentIndex.Weapon0);
                    agent.Formation = f;
                }
            }
        }


        /// <summary>
        /// Modified version of original SpawnTroop method. Changed origin requirement to AgentBuildData so that we can tweak it outside of the method.
        /// </summary>
        /// <param name="agentBuildData"></param>
        /// <param name="isPlayerSide"></param>
        /// <param name="hasFormation"></param>
        /// <param name="spawnWithHorse"></param>
        /// <param name="formationTroopCount"></param>
        /// <param name="formationTroopIndex"></param>
        /// <param name="isAlarmed"></param>
        /// <param name="wieldInitialWeapons"></param>
        /// <param name="initialPosition"></param>
        /// <param name="initialDirection"></param>
        /// <param name="specialActionSet"></param>
        /// <returns></returns>
        public Agent SpawnTroop(
              AgentBuildData agentBuildData,
              bool isPlayerSide,
              bool hasFormation,
              bool spawnWithHorse,
              int formationTroopCount,
              int formationTroopIndex,
              bool isAlarmed,
              bool wieldInitialWeapons,
              Vec3? initialPosition,
              Vec2? initialDirection,
              string specialActionSet = null)
        {
            IAgentOriginBase troopOrigin = agentBuildData.AgentOrigin;
            BasicCharacterObject troop = troopOrigin.Troop;
            Team agentTeam = Mission.GetAgentTeam(troopOrigin, isPlayerSide);
            if (troop.IsPlayerCharacter)
                spawnWithHorse = true;
            AgentBuildData agentBuildData1 = agentBuildData;  
            if (initialPosition.HasValue)
            {
                AgentBuildData agentBuildData2 = agentBuildData1;
                Vec3 vec3 = initialPosition.Value;
                ref Vec3 local1 = ref vec3;
                agentBuildData2.InitialPosition(in local1);
                AgentBuildData agentBuildData3 = agentBuildData1;
                Vec2 vec2 = initialDirection.Value;
                ref Vec2 local2 = ref vec2;
                agentBuildData3.InitialDirection(in local2);
            }
            else if (troop.IsHero && troopOrigin != null && (troopOrigin.BattleCombatant != null && troop == troopOrigin.BattleCombatant.General) && Mission.Current.GetFormationSpawnClass(agentTeam.Side, FormationClass.NumberOfRegularFormations) == FormationClass.NumberOfRegularFormations)
            {
                WorldPosition spawnPosition;
                Vec2 direction;
                Mission.Current.GetFormationSpawnFrame(agentTeam.Side, FormationClass.NumberOfRegularFormations, false, out spawnPosition, out direction);
                AgentBuildData agentBuildData2 = agentBuildData1;
                Vec3 groundVec3 = spawnPosition.GetGroundVec3();
                ref Vec3 local = ref groundVec3;
                agentBuildData2.InitialPosition(in local).InitialDirection(in direction);
            }
            else if (!hasFormation)
            {
                WorldPosition spawnPosition;
                Vec2 direction;
                Mission.Current.GetFormationSpawnFrame(agentTeam.Side, FormationClass.NumberOfAllFormations, false, out spawnPosition, out direction);
                AgentBuildData agentBuildData2 = agentBuildData1;
                Vec3 groundVec3 = spawnPosition.GetGroundVec3();
                ref Vec3 local = ref groundVec3;
                agentBuildData2.InitialPosition(in local).InitialDirection(in direction);
            }
            if (spawnWithHorse)
                agentBuildData1.MountKey(MountCreationKey.GetRandomMountKeyString(troop.Equipment[EquipmentIndex.ArmorItemEndSlot].Item, troop.GetMountKeySeed()));
            if (hasFormation)
            {
                Formation formation = agentTeam.GetFormation(troop.GetFormationClass(troopOrigin.BattleCombatant));
                agentBuildData1.Formation(formation);
                agentBuildData1.FormationTroopCount(formationTroopCount).FormationTroopIndex(formationTroopIndex);
            }
            if (isPlayerSide && troop == Game.Current.PlayerTroop)
                agentBuildData1.Controller(Agent.ControllerType.Player);
            Agent agent = Mission.Current.SpawnAgent(agentBuildData1, false, formationTroopCount);
            if (agent.Character.IsHero)
                agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.IsUnique);
            if (agent.IsAIControlled & isAlarmed)
                agent.SetWatchState(Agent.WatchState.Alarmed);
            if (wieldInitialWeapons)
                agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp);
            if (!string.IsNullOrEmpty(specialActionSet))
            {
                AnimationSystemData animationSystemData = agentBuildData1.AgentMonster.FillAnimationSystemData(MBGlobals.GetActionSet(specialActionSet), agent.Character.GetStepSize(), false);
                AgentVisualsNativeData agentVisualsNativeData = agentBuildData1.AgentMonster.FillAgentVisualsNativeData();
                agent.SetActionSet(ref agentVisualsNativeData, ref animationSystemData);
            }
            return agent;
        }
    }
}
