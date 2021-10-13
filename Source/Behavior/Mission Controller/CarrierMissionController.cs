﻿using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Carrier.Behavior
{
    public class CarrierMissionBehavior : MissionLogic
    {
        /// <summary>
        /// Simple struct to keep BattleSide - PartyBase relation. We need PartyBase for BattleCombatant ( AgentOrigin )
        /// </summary>
        struct SideAndParty {
            public BattleSideEnum Side;
            public PartyBase PartyBase;
        }

        private const float OCCASIONAL_TICK = 5; 
        private bool _isSpawnedTroops = false, _isAnyBannermenReachedWalls = false;
        private List<SideAndParty> _bannerParties;
        private PartyBase _reservedReinforceDefender, _reservedReinforceAttacker;
        private Dictionary<Agent, GameEntity> _bannerBearersAndObject;
        private CarrierConfig _config;
        private float _lastTick;
        private Vec3 _randomWind;

        
        public CarrierMissionBehavior(CarrierConfig config) {
            _config = config;
        }

        public override void AfterStart()
        {
            
            _lastTick = Mission.Current.Time;
            _bannerParties = new List<SideAndParty>();
            _bannerBearersAndObject = new Dictionary<Agent, GameEntity>();

            if (!Mission.Current.Scene.IsAtmosphereIndoor && !this.IsHideout() )
            {
                // Get spawnlogic and register it's phase change events. Although currently I have no good solution to create bannerman on reinforce waves
                MissionAgentSpawnLogic spawnLogicBehavior = Mission.Current.GetMissionBehaviour<MissionAgentSpawnLogic>();
                spawnLogicBehavior.AddPhaseChangeAction(BattleSideEnum.Attacker, new OnPhaseChangedDelegate(this.OnAttackerPhaseChanged));
                spawnLogicBehavior.AddPhaseChangeAction(BattleSideEnum.Defender, new OnPhaseChangedDelegate(this.OnDefenderPhaseChanged));
            }
            // Get all the parties involved
            List<PartyBase> partyList = PlayerEncounter.Battle.InvolvedParties.ToList();
            partyList.OrderBy(a => a.NumberOfHealthyMembers);

            MapEvent currentMapEvent = PlayerEncounter.EncounteredBattle;
            foreach (PartyBase pb in partyList){
                // Check certain conditions with respect to config file
                if (currentMapEvent.IsRaid) { 
                    if (pb.Side == BattleSideEnum.Attacker && !_config.ALLOW_RAID_ATTACKER_BANNERS)
                        continue;
                    else if ((pb.Side == BattleSideEnum.Defender && !_config.ALLOW_RAID_DEFENDER_BANNERS))
                        continue;
                }

                if (currentMapEvent.IsSiegeAssault || currentMapEvent.IsSiegeOutside) {
                    if (pb.Side == BattleSideEnum.Attacker && !_config.ALLOW_SIEGE_ATTACKER_BANNERS)
                        continue;
                    else if ((pb.Side == BattleSideEnum.Defender && !_config.ALLOW_SIEGE_DEFENDER_BANNERS))
                        continue;
                }

                if(currentMapEvent.IsHideoutBattle && !_config.ALLOW_IN_HIDEOUT) {
                   continue;
                }

                // IF Non-Nobles are allowed and party is mobile and not bandit 
                // OR Party has MapFaction, NOT Villager or Caravan or Bandit or Garrison
                if ( (_config.ALLOW_NON_NOBLES && (!pb.MapFaction.IsBanditFaction && pb.IsMobile)) ||
                     (pb.MapFaction != null && (!pb.MapFaction.IsBanditFaction && pb.IsMobile && !pb.MobileParty.IsCaravan && !pb.MobileParty.IsVillager && !pb.MobileParty.IsGarrison)) ) {
                    SideAndParty sp = new SideAndParty { PartyBase = pb, Side = pb.Side };
                    _bannerParties.Add( sp );
                    if (pb.Side == BattleSideEnum.Attacker && _reservedReinforceAttacker == null) _reservedReinforceAttacker = pb;
                    if (pb.Side == BattleSideEnum.Defender && _reservedReinforceDefender == null) _reservedReinforceDefender = pb;
                }     
            }

            // Small wind effect for fun
            _randomWind = new Vec3(MBRandom.RandomFloatRanged(-3f,3f), MBRandom.RandomFloatRanged(-3f,3f), MBRandom.RandomFloatRanged(-1f,1f));
        }


        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if( !_isSpawnedTroops) {
                foreach (Team team in Mission.Teams) {
                    if(team.ActiveAgents.Count > 0) {
                        _isSpawnedTroops = true;
                        this.CreateCarriersForTeam(team);
                    }                    
                }
            }

            if (OCCASIONAL_TICK < Mission.Current.Time - _lastTick)
            {
                _lastTick = Mission.Current.Time;
                InternalOccasionalTick();
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if( agentState == AgentState.Killed || agentState == AgentState.Unconscious) {
                if (_bannerBearersAndObject.ContainsKey(affectedAgent)) {
                    try {
                        if( this.ShouldUseTorch()) {
                            GameEntity entity = _bannerBearersAndObject[affectedAgent];
                            Light light = entity.GetLight();
                            light.Dispose();
                            entity.Remove(0);
                        }
                    } catch { }
                    if (this.IsHideout()) return;
                    Agent[] agentsAround = Mission.Current.GetNearbyAllyAgents(affectedAgent.Position.AsVec2, this._config.MORALE_RADIUS, affectedAgent.Team).ToArray();
                    foreach(Agent a in agentsAround) {
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
            if (this.IsHideout()) return;
            // Check all bannermen, update the agent morale around them until it hits the maximum
            foreach (KeyValuePair<Agent, GameEntity> pair in _bannerBearersAndObject) {
                Agent bannerAgent = pair.Key;
                if (!bannerAgent.IsActive() || bannerAgent.Team == null || bannerAgent.Health <= 0) continue;

                if (_config.ALLOW_MORALE_BOOST_FOR_WALLS) {
                    if (this.IsSiegeAssault() && !_isAnyBannermenReachedWalls && IsAgentStandingOnWalls(bannerAgent)) {
                        _isAnyBannermenReachedWalls = true;
                        if ( _config.ALLOW_MORALE_BOOST_MESSAGE_FOR_WALLS) {
                            if( Mission.Current.MainAgent != null && 
                                Mission.Current.MainAgent.Team != null && 
                                Mission.Current.MainAgent.Team.Side == BattleSideEnum.Attacker) {
                                InformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject("{=BLCCliLPkWgI}Your banner has reached to the walls!"), 500, bannerAgent.Character, "event:/alerts/report/battle_winning");
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
                if (sp.Side == team.Side) {
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
                int count = f.CountOfUnits / (int)MapFormationToConfigCount(f.FormationIndex);
                if (this.IsHideout())
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
                    Equipment basicEq = GetBasicTroopEquipment(pb);
                    if (basicEq != null) {
                        for (int k = (int)EquipmentIndex.ArmorItemBeginSlot; k < (int)EquipmentIndex.ArmorItemEndSlot - 1; k++) {
                            eq.AddEquipmentToSlotWithoutAgent((EquipmentIndex)k, basicEq.GetEquipmentFromSlot((EquipmentIndex)k));
                        }
                    }
                    if (f.IsCavalry()) {
                        ItemObject horse = Game.Current.ObjectManager.GetObject<ItemObject>("sumpter_horse");
                        eq.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, new EquipmentElement(horse));
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
                    if ( !ShouldUseTorch() ) {
                        agent.RemoveEquippedWeapon(EquipmentIndex.Weapon1);
                    } else {
                        agent.AddPrefabComponentToBone("torch_burning_prefab", Game.Current.HumanMonster.MainHandItemBoneIndex);
                        Light pointLight = Light.CreatePointLight(10f);
                        pointLight.SetLightFlicker(1, 0.8f);
                        pointLight.Intensity = 70f;
                        pointLight.LightColor = new Vec3(1f, 0.68f, 0.29f, -1f);
                        GameEntity lightEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                        lightEntity.AddLight(pointLight);
                        lightEntity.SetLocalPosition(new Vec3(0.5f, 1f, 1.5f + (f.IsCavalry() ? 1:0)));
                        agent.AgentVisuals.AddChildEntity(lightEntity);
                        _bannerBearersAndObject[agent] = lightEntity;
                    }
                    agent.AgentVisuals.SetClothWindToWeaponAtIndex(_randomWind, true, EquipmentIndex.Weapon0);
                    agent.Formation = f;
                }
            }
        }


        /// <summary>
        /// Get basic troop equipment. If tier based is allowed, this will return better equipment with respect to upgrade path of troop 
        /// </summary>
        /// <param name="pb"></param>
        /// <returns></returns>
        private Equipment GetBasicTroopEquipment(PartyBase pb) {
            if( pb.MapFaction != null) {
                if (pb.MapFaction.BasicTroop != null) {
                    CharacterObject basicTroop = pb.Culture.BasicTroop;
                    Equipment resEq = basicTroop.RandomBattleEquipment;
                    if( _config.USE_TIER_BASED_BANNERMAN) {
                        if( pb.LeaderHero != null && pb.LeaderHero.Clan != null) {
                            int tier = pb.LeaderHero.Clan.Tier;
                            while(basicTroop.Tier != tier && basicTroop != null) {
                                CharacterObject[] targets = basicTroop.UpgradeTargets;
                                if(targets.Length == 0)
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
        /// <summary>
        /// Map Formation to preset Config "Limit Per Formation" count
        /// </summary>
        /// <param name="f">Formation you want to convert. Unrecognized will return 5</param>
        /// <returns></returns>
        private float MapFormationToConfigCount(FormationClass f)
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

        private bool IsHideout()
        {
            return (PlayerEncounter.Battle != null && PlayerEncounter.Battle.IsHideoutBattle);
        }

        private bool IsSiegeAssault()
        {
            return (PlayerEncounter.Battle != null && PlayerEncounter.Battle.IsSiegeAssault);
        }

        private bool ShouldUseTorch()
        {
            return (_config.USE_TORCH_IN_NIGHT_BATTLE && (Mission.Current.Scene.TimeOfDay >= 17 || Mission.Current.Scene.TimeOfDay <= 4f));
        }

    }
}