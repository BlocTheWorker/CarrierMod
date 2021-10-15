using System;
using System.Collections.Generic;

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Carrier.Behavior.Mission_Controller
{
    public class SoldierResponseController : MissionLogic
    {
        private struct VoiceAction
        {
            public Agent ResponsiveAgent;
            public SkinVoiceManager.SkinVoiceType Voice;
            public Action<Agent, SkinVoiceManager.SkinVoiceType> Action;
        }
        private CarrierConfig _config;
        private PriorityQueue<float, VoiceAction> _actionQueue;

        public SoldierResponseController(CarrierConfig config)
        {
            _actionQueue = new PriorityQueue<float, VoiceAction>();
            _config = config;
        }

        public override void AfterStart()
        {
            if( Mission.Current != null && Mission.Current.PlayerTeam != null && Mission.Current.IsOrderShoutingAllowed())
            {
                if (_config.USE_UNIT_ORDER_RELAY_SOUNDS)
                {
                    Mission.Current.PlayerTeam.OnOrderIssued += PlayerTeam_OnOrderIssued;
                }
            }
                
        }

        public override void OnMissionTick(float dt) {
            base.OnMissionTick(dt);
            bool stackDone = false;
            while (!stackDone) {
                if (_actionQueue.Count > 0) {
                    KeyValuePair<float, VoiceAction> pair = _actionQueue.Peek();
                    if ((pair.Key * -1) <= Mission.Current.Time) {
                        VoiceAction vAction = _actionQueue.Dequeue().Value;
                        vAction.Action(vAction.ResponsiveAgent, vAction.Voice);
                    } else {
                        stackDone = true;
                    }
                } else {
                    break;
                }
            }
        }


        private void PlayerTeam_OnOrderIssued(OrderType orderType, IEnumerable<Formation> appliedFormations, params object[] delegateParams)
        {
            try
            {
                foreach (Formation f in appliedFormations)
                {
                    int count = 0;
                    if (f.CountOfUnits != 0)
                        count = f.CountOfUnits / 8;

                    float inc = (count > 50? 5f: 1.8f) / (count != 0? count : 1);
                    for (int k = 0; k < count; k++) {
                        Agent agent = f.GetUnitWithIndex(MBRandom.RandomInt(f.CountOfUnits - 1));
                        VoiceAction va = new VoiceAction() { Action = ShoutAndRelayOrders, Voice = SkinVoiceManager.VoiceType.Advance, ResponsiveAgent = agent };
                        float currentTime = Mission.Current.Time;
                        if (orderType == OrderType.Advance)
                            va.Voice = SkinVoiceManager.VoiceType.Advance;
                        else if (orderType == OrderType.HoldFire)
                            va.Voice = SkinVoiceManager.VoiceType.HoldFire;
                        else if (orderType == OrderType.Move) 
                            va.Voice = MBRandom.RandomFloat > 0.5? SkinVoiceManager.VoiceType.MpAffirmative : SkinVoiceManager.VoiceType.Move;
                        else if (orderType == OrderType.StandYourGround)
                            va.Voice = SkinVoiceManager.VoiceType.Idle;
                        else if (orderType == OrderType.FallBack)
                            va.Voice = SkinVoiceManager.VoiceType.FallBack;
                        else if (orderType == OrderType.Charge)
                            va.Voice = MBRandom.RandomFloat > 0.5 ? SkinVoiceManager.VoiceType.Yell : SkinVoiceManager.VoiceType.Charge;
                        else if (orderType == OrderType.ArrangementSchiltron)
                            va.Voice = MBRandom.RandomFloat > 0.5 ? SkinVoiceManager.VoiceType.FormShieldWall : SkinVoiceManager.VoiceType.MpAffirmative;
                        else if (orderType == OrderType.FireAtWill)
                            va.Voice = SkinVoiceManager.VoiceType.FireAtWill;
                        else
                            va.Voice = MBRandom.RandomFloat > 0.5 ? SkinVoiceManager.VoiceType.MpAffirmative : SkinVoiceManager.VoiceType.Grunt;
                        _actionQueue.Enqueue( -1 * ( (k*inc) + currentTime + 0.1f ), va);
                    }
                }
            }
            catch { }
        }

        private void ShoutAndRelayOrders( Agent agent, SkinVoiceManager.SkinVoiceType voiceType) {
            agent.MakeVoice(voiceType, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        }
    }
}
