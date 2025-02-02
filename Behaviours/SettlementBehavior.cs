﻿using Populations.Components;
using Populations.Models;
using Populations.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static Populations.PolicyManager;
using static Populations.PopulationManager;

namespace Populations.Behaviors
{
    public class SettlementBehavior : CampaignBehaviorBase
    {

        private PopulationManager populationManager = null;
        private PolicyManager policyManager = null;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(DailySettlementTick));
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(HourlyTickParty));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameCreated));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(OnMobilePartyDestroyed));
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(OnSettlementEntered));
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                if (PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PolicyManager != null)
                {
                    populationManager = PopulationConfig.Instance.PopulationManager;
                    policyManager = PopulationConfig.Instance.PolicyManager;
                }
            }

            dataStore.SyncData("pops", ref populationManager);
            dataStore.SyncData("policies", ref policyManager);

            if (dataStore.IsLoading)
            {
                if (populationManager == null && policyManager == null)
                {
                    PopulationConfig.Instance.InitManagers(new Dictionary<Settlement, PopulationData>(), new List<MobileParty>(),
                    new Dictionary<Settlement, List<PolicyManager.PolicyElement>>(), new Dictionary<Settlement, PolicyManager.TaxType>(),
                    new Dictionary<Settlement, PolicyManager.MilitiaPolicy>(), new Dictionary<Settlement, WorkforcePolicy>());
                }
                else
                {
                    PopulationConfig.Instance.InitManagers(populationManager, policyManager);
                }
            }
        }

        private void HourlyTickParty(MobileParty party)
        {

            if (party != null && PopulationConfig.Instance.PopulationManager != null && 
                PopulationConfig.Instance.PopulationManager.IsPopulationParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.PartyComponent;
                Settlement target = component._target;

                if (component is MilitiaComponent)
                {
                    MilitiaComponent militiaComponent = (MilitiaComponent)component;
                    AiBehavior behavior = militiaComponent.behavior;
                    if (behavior == AiBehavior.EscortParty)
                        party.SetMoveEscortParty(militiaComponent._escortTarget);
                    else party.SetMoveGoToSettlement(militiaComponent.OriginSettlement);
                    return;
                }

                if (target != null)
                {
                    float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(party, target);
                    if (distance <= 30f)
                    {
                        EnterSettlementAction.ApplyForParty(party, target);
                    } else
                    {
                        if (target.IsVillage)
                        {
                            party.Ai.SetAIState(AIState.VisitingVillage);
                            if (target.Village.VillageState != Village.VillageStates.Looted && target.Village.VillageState != Village.VillageStates.BeingRaided)
                                party.SetMoveModeHold();
                            else PartyKeepMoving(ref party, ref target);
                        }
                        else if (!target.IsVillage)
                        {
                            party.Ai.SetAIState(AIState.VisitingNearbyTown);
                            if (!target.IsUnderSiege)
                                PartyKeepMoving(ref party, ref target);
                            else party.SetMoveModeHold();
                        }
                    } 
                }
                else if (target == null)
                {
                    DestroyPartyAction.Apply(null, party);
                    PopulationConfig.Instance.PopulationManager.RemoveCaravan(party);
                }
            }

            if (party.StringId.Contains("slavecaravan") && party.Party != null && party.Party.NumberOfHealthyMembers == 0)
            {
                DestroyPartyAction.Apply(null, party);
                if (PopulationConfig.Instance.PopulationManager.IsPopulationParty(party))
                    PopulationConfig.Instance.PopulationManager.RemoveCaravan(party);
            }
        }

        private void PartyKeepMoving(ref MobileParty party, ref Settlement target)
        {
            if (target.IsVillage) party.Ai.SetAIState(AIState.VisitingVillage);
            else party.Ai.SetAIState(AIState.VisitingNearbyTown);
            party.SetMoveGoToSettlement(target);
        }

        private void OnSettlementEntered(MobileParty party, Settlement target, Hero hero)
        {
            if (party != null && PopulationConfig.Instance.PopulationManager != null)
            {
                if (PopulationConfig.Instance.PopulationManager.IsPopulationParty(party))
                {
                    PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(target);
                    PopulationPartyComponent component = (PopulationPartyComponent)party.PartyComponent;

                    if (component is MilitiaComponent && target.IsVillage) 
                    {
                        foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
                            target.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(element.Character, element.Number);
                        if (party.PrisonRoster.TotalRegulars > 0)
                            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster())
                                if (!element.Character.IsHero) data.UpdatePopType(PopType.Slaves, element.Number);
                    }

                    if (component.slaveCaravan)
                    {
                        int slaves = Helpers.Helpers.GetRosterCount(party.PrisonRoster);
                        data.UpdatePopType(PopType.Slaves, slaves);
                    }
                    else if (component.popType != PopType.None)
                    {
                        string filter = component.popType == PopType.Serfs ? "villager" : (component.popType == PopType.Craftsmen ? "craftsman" : "noble");
                        int pops = Helpers.Helpers.GetRosterCount(party.MemberRoster, filter);
                        data.UpdatePopType(component.popType, pops);
                    }

                    DestroyPartyAction.Apply(null, party);
                    PopulationConfig.Instance.PopulationManager.RemoveCaravan(party);
                } else if (party.LeaderHero != null && party.LeaderHero == target.Owner && party.LeaderHero != Hero.MainHero
                    && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(target)) // AI choices
                {
                    Hero lord = party.LeaderHero;
                    Kingdom kingdom = lord.Clan.Kingdom;
                    List<ValueTuple<PolicyType, bool>> decisions = new List<ValueTuple<PolicyType, bool>>();
                    if (!target.IsVillage && target.Town != null)
                    {
                        if (kingdom != null)
                        {
                            IEnumerable<Kingdom> enemies = FactionManager.GetEnemyKingdoms(kingdom);
                            bool atWar = enemies.Count() > 0;

                            decisions.Add((PolicyType.CONSCRIPTION, atWar));
                            decisions.Add((PolicyType.SUBSIDIZE_MILITIA, atWar));
                        }

                        TaxType tax = PopulationConfig.Instance.PolicyManager.GetSettlementTax(target);
                        if (target.Town.LoyaltyChange < 0)
                        {
                            if (!PopulationConfig.Instance.PolicyManager.IsPolicyEnacted(target, PolicyType.EXEMPTION))
                                decisions.Add((PolicyType.EXEMPTION, true));

                            if (tax == TaxType.High)
                                PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.Standard);
                            else if (tax == TaxType.Standard)
                                PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.Low);
                        } else
                        {
                            if (tax == TaxType.Standard)
                                PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.High);
                            else if (tax == TaxType.Low)
                                PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.Standard);
                        }

                        float filledCapacity = new GrowthModel().GetSettlementFilledCapacity(target);
                        bool growth = lord.Clan.Influence >= 300 && filledCapacity < 0.5f;
                        decisions.Add((PolicyType.POP_GROWTH, growth));
                    } else if (target.IsVillage)
                    {
                        if (kingdom != null)
                        {
                            IEnumerable<Kingdom> enemies = FactionManager.GetEnemyKingdoms(kingdom);
                            bool atWar = enemies.Count() > 0;
                            decisions.Add((PolicyType.SUBSIDIZE_MILITIA, atWar));
                        }

                        float hearths = target.Village.Hearth;
                        if (hearths < 300f)
                            PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.Low);
                        else if (hearths < 1000f)
                            PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.Standard);
                        else PopulationConfig.Instance.PolicyManager.UpdateTaxPolicy(target, TaxType.High);

                        float filledCapacity = new GrowthModel().GetSettlementFilledCapacity(target);
                        bool growth = lord.Clan.Influence >= 300 && filledCapacity < 0.5f;
                        decisions.Add((PolicyType.POP_GROWTH, growth));
                    }

                    foreach ((PolicyType, bool) decision in decisions) 
                        PopulationConfig.Instance.PolicyManager.UpdatePolicy(target, decision.Item1, decision.Item2);
                        
                }  
            }
        }

        private void DailySettlementTick(Settlement settlement)
        {
            if (settlement == null) return;
            
            if (PopulationConfig.Instance.PopulationManager == null)
                PopulationConfig.Instance.InitManagers(new Dictionary<Settlement, PopulationData>(), new List<MobileParty>(),
                new Dictionary<Settlement, List<PolicyManager.PolicyElement>>(), new Dictionary<Settlement, PolicyManager.TaxType>(),
                new Dictionary<Settlement, PolicyManager.MilitiaPolicy>(), new Dictionary<Settlement, WorkforcePolicy>());

            UpdateSettlementPops(settlement);
            InitializeSettlementPolicies(settlement);
            // Send Slaves
            if (PopulationConfig.Instance.PolicyManager.IsPolicyEnacted(settlement, PolicyManager.PolicyType.EXPORT_SLAVES) && DecideSendSlaveCaravan(settlement))
            {
                Village target = null;
                MBReadOnlyList<Village> villages = settlement.BoundVillages;
                foreach (Village village in villages)
                    if (village.Settlement != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(village.Settlement) && !PopulationConfig.Instance.PopulationManager.PopSurplusExists(village.Settlement, PopType.Slaves))
                    {
                        target = village;
                        break;
                    }

                if (target != null) SendSlaveCaravan(target);
            }

            // Send Travellers
            if (settlement.IsTown)
            {
                int random = MBRandom.RandomInt(1, 100);
                if (random <= 5)
                {
                    Settlement target = GetTownToTravel(settlement);
                    if (target != null)
                        if (PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(target) &&
                            PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(settlement))
                            SendTravellerParty(settlement, target);
                }
            }

            if (settlement.IsCastle && settlement.Town != null && settlement.Town.GarrisonParty != null)
            {
                foreach (Building castleBuilding in settlement.Town.Buildings)
                    if (Helpers.Helpers._buildingCastleRetinue != null && castleBuilding.BuildingType == Helpers.Helpers._buildingCastleRetinue)
                    {
                        MobileParty garrison = settlement.Town.GarrisonParty;
                        if (garrison.MemberRoster != null && garrison.MemberRoster.Count > 0)
                        {
                            List<TroopRosterElement> elements = garrison.MemberRoster.GetTroopRoster();
                            int currentRetinue = 0;
                            foreach (TroopRosterElement soldierElement in elements)
                                if (Helpers.Helpers.IsRetinueTroop(soldierElement.Character, settlement.Culture))
                                    currentRetinue += soldierElement.Number;

                            int maxRetinue = castleBuilding.CurrentLevel == 1 ? 20 : (castleBuilding.CurrentLevel == 2 ? 40 : 60);
                            if (currentRetinue < maxRetinue)
                                if (garrison.MemberRoster.Count < garrison.Party.PartySizeLimit)
                                    garrison.MemberRoster.AddToCounts(settlement.Culture.EliteBasicTroop, 1);
                        }
                    }
            }
        }

        private void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
        {
            if (mobileParty != null && PopulationConfig.Instance.PopulationManager != null &&
                PopulationConfig.Instance.PopulationManager.IsPopulationParty(mobileParty))
            {
                PopulationConfig.Instance.PopulationManager.RemoveCaravan(mobileParty);
            }
        }

        private bool DecideSendSlaveCaravan(Settlement settlement)
        {

            if (settlement.IsTown && settlement.Town != null)
            {
                MBReadOnlyList<Village> villages = settlement.BoundVillages;
                if (villages != null && villages.Count > 0)
                    if (PopulationConfig.Instance.PopulationManager.PopSurplusExists(settlement, PopType.Slaves))
                        return true;
            }
            return false;
        }

        private Settlement GetTownToTravel(Settlement origin)
        {
            if (origin.OwnerClan != null)
            {
                Kingdom kingdom = origin.OwnerClan.Kingdom;
                if (kingdom != null)
                {
                    if (kingdom.Settlements != null && kingdom.Settlements.Count > 1)
                    {
                        List<ValueTuple<Settlement, float>> list = new List<ValueTuple<Settlement, float>>();
                        foreach (Settlement settlement in kingdom.Settlements)
                            if (settlement.IsTown && settlement != origin)
                                list.Add(new ValueTuple<Settlement,float>(settlement, 1f));
                        
                        Settlement target = MBRandom.ChooseWeighted<Settlement>(list);
                        return target;
                    }
                }
            }

            return null;
        }

        private void SendTravellerParty(Settlement origin, Settlement target)
        {
            PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(origin);
            int random = MBRandom.RandomInt(1, 100);
            CharacterObject civilian;
            PopType type;
            int count;
            string name;
            if (random < 60)
            {
                civilian = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().FirstOrDefault(x => x.StringId == "villager_" + origin.Culture.StringId.ToString());
                count = MBRandom.RandomInt(30, 70);
                type = PopType.Serfs;
            }
            else if (random < 90)
            {
                civilian = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().FirstOrDefault(x => x.StringId == "craftsman_" + origin.Culture.StringId.ToString());
                count = MBRandom.RandomInt(15, 30);
                type = PopType.Craftsmen;
            } else
            {
                civilian = MBObjectManager.Instance.GetObjectTypeList<CharacterObject>().FirstOrDefault(x => x.StringId == "noble_" + origin.Culture.StringId.ToString());
                count = MBRandom.RandomInt(10, 15);
                type = PopType.Nobles;
            }

            name = "Travelling " + Helpers.Helpers.GetClassName(type, origin.Culture).ToString() + " from {0}";

            if (civilian != null)
              PopulationPartyComponent.CreateTravellerParty("travellers_", origin, target,
                name, count, type, civilian);
            
        }

        private void SendSlaveCaravan(Village target)
        {
            Settlement origin = target.MarketTown.Settlement;
            PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(origin);
            int slaves = (int)((double)data.GetTypeCount(PopType.Slaves) * 0.005d);
            data.UpdatePopType(PopType.Slaves, (int)((float)slaves * -1f));
            PopulationPartyComponent.CreateSlaveCaravan("slavecaravan_", origin, target.Settlement, "Slave Caravan from {0}", slaves);
        }

        private void OnGameCreated(CampaignGameStarter campaignGameStarter)
        {
            AddDialog(campaignGameStarter);
            AddMenus(campaignGameStarter);

            if (PopulationConfig.Instance.PopulationManager != null)
                foreach (Settlement settlement in Settlement.All)
                    if (PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(settlement))
                    {
                        PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(settlement);
                        if (data.Assimilation >= 1f)
                            settlement.Culture = settlement.Owner.Culture;
                    }

            BuildingType retinueType = MBObjectManager.Instance.GetObjectTypeList<BuildingType>().FirstOrDefault(x => x == Helpers.Helpers._buildingCastleRetinue);
            if (retinueType == null)
            {
                Helpers.Helpers._buildingCastleRetinue.Initialize(new TextObject("{=!}Retinue Barracks", null), new TextObject("{=!}Barracks for the castle retinue, a group of elite soldiers. The retinue is added to the garrison over time, up to a limit of 20, 40 or 60 (building level).", null), new int[]
                {
                     1000,
                     1500,
                     2000
                }, BuildingLocation.Castle, new Tuple<BuildingEffectEnum, float, float, float>[]
                {
                }, 0);
            }
        }

        private void AddMenus(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddGameMenuOption("town", "manage_population", "{=!}Manage population",
                new GameMenuOption.OnConditionDelegate(game_menu_town_manage_town_on_condition),
                new GameMenuOption.OnConsequenceDelegate(game_menu_town_manage_town_on_consequence), false, 5, false);

            campaignGameStarter.AddGameMenuOption("castle", "manage_population", "{=!}Manage population",
               new GameMenuOption.OnConditionDelegate(game_menu_town_manage_town_on_condition),
               new GameMenuOption.OnConsequenceDelegate(game_menu_town_manage_town_on_consequence), false, 3, false);

            campaignGameStarter.AddGameMenuOption("village", "manage_population", "{=!}Manage population",
               new GameMenuOption.OnConditionDelegate(game_menu_town_manage_town_on_condition),
               new GameMenuOption.OnConsequenceDelegate(game_menu_town_manage_town_on_consequence), false, 5, false);
        }

        private static bool game_menu_town_manage_town_on_condition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            Settlement currentSettlement = Settlement.CurrentSettlement;
            return currentSettlement.OwnerClan == Hero.MainHero.Clan && PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(currentSettlement);
        }

        public static void game_menu_town_manage_town_on_consequence(MenuCallbackArgs args) => UIManager.instance.InitializePopulationWindow();

        private void AddDialog(CampaignGameStarter starter)
        {

            starter.AddDialogLine("traveller_serf_party_start", "start", "traveller_party_greeting", 
                "M'lord! We are humble folk, travelling between towns, looking for work and trade.", 
                new ConversationSentence.OnConditionDelegate(this.traveller_serf_start_on_condition), null, 100, null);

            starter.AddDialogLine("traveller_craftsman_party_start", "start", "traveller_party_greeting",
                "Good day to you. We are craftsmen travelling for business purposes.",
                new ConversationSentence.OnConditionDelegate(this.traveller_craftsman_start_on_condition), null, 100, null);

            starter.AddDialogLine("traveller_noble_party_start", "start", "traveller_party_greeting",
                "Yes? Please do not interfere with our caravan.",
                new ConversationSentence.OnConditionDelegate(this.traveller_noble_start_on_condition), null, 100, null);


            starter.AddPlayerLine("traveller_party_loot", "traveller_party_greeting", "close_window", 
                new TextObject("{=XaPMUJV0}Whatever you have, I'm taking it. Surrender or die!", null).ToString(),
                new ConversationSentence.OnConditionDelegate(this.traveller_aggression_on_condition), 
                delegate { PlayerEncounter.Current.IsEnemy = true; }, 
                100, null, null);

            starter.AddPlayerLine("traveller_party_leave", "traveller_party_greeting", "close_window",
                new TextObject("{=dialog_end_nice}Carry on, then. Farewell.", null).ToString(), null,
                delegate { PlayerEncounter.LeaveEncounter = true; },
                100, null, null);

            starter.AddDialogLine("slavecaravan_friend_party_start", "start", "slavecaravan_party_greeting",
                "My lord, we are taking these rabble somewhere they can be put to good use.",
                new ConversationSentence.OnConditionDelegate(this.slavecaravan_amicable_on_condition), null, 100, null);

            starter.AddDialogLine("slavecaravan_neutral_party_start", "start", "slavecaravan_party_greeting",
                "If you're not planning to join those vermin back there, move away![rf:idle_angry][ib:aggressive]",
                new ConversationSentence.OnConditionDelegate(this.slavecaravan_neutral_on_condition), null, 100, null);

            starter.AddPlayerLine("slavecaravan_party_leave", "slavecaravan_party_greeting", "close_window",
               new TextObject("{=dialog_end_nice}Carry on, then. Farewell.", null).ToString(), null,
               delegate { PlayerEncounter.LeaveEncounter = true; },
               100, null, null);

            starter.AddPlayerLine("slavecaravan_party_threat", "slavecaravan_party_greeting", "slavecaravan_threat",
               new TextObject("{=!}Give me your slaves and gear, or else!", null).ToString(),
               new ConversationSentence.OnConditionDelegate(this.slavecaravan_neutral_on_condition),
               null, 100, null, null);

            starter.AddDialogLine("slavecaravan_party_threat_response", "slavecaravan_threat", "close_window",
                "One more for the mines! Lads, get the whip![rf:idle_angry][ib:aggressive]",
                null, delegate { PlayerEncounter.Current.IsEnemy = true; }, 100, null);

            starter.AddDialogLine("raised_militia_party_start", "start", "raised_militia_greeting",
                "M'lord! We are ready to serve you.",
                new ConversationSentence.OnConditionDelegate(this.raised_militia_start_on_condition), null, 100, null);

            starter.AddPlayerLine("raised_militia_party_follow", "raised_militia_greeting", "raised_militia_order",
               new TextObject("{=!}Follow my company.", null).ToString(),
               new ConversationSentence.OnConditionDelegate(this.raised_militia_order_on_condition),
               new ConversationSentence.OnConsequenceDelegate(this.raised_militia_follow_on_consequence), 100, null, null);

            starter.AddPlayerLine("raised_militia_party_retreat", "raised_militia_greeting", "raised_militia_order",
               new TextObject("{=!}You may go home.", null).ToString(),
               new ConversationSentence.OnConditionDelegate(this.raised_militia_order_on_condition),
               new ConversationSentence.OnConsequenceDelegate(this.raised_militia_retreat_on_consequence), 100, null, null);

            starter.AddDialogLine("raised_militia_order_response", "raised_militia_order", "close_window",
                "Aye!",
                null, delegate { PlayerEncounter.LeaveEncounter = true; }, 100, null);
        }

        private bool IsTravellerParty(PartyBase party)
        {
            bool value = false;
            if (party != null && party.MobileParty != null)
                if (PopulationConfig.Instance.PopulationManager.IsPopulationParty(party.MobileParty))
                    value = true;
            return value;
        }

        private bool traveller_serf_start_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                if (component.popType == PopType.Serfs)
                    value = true;
            }
    
            return value;
        }

        private void raised_militia_retreat_on_consequence()
        {
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                MilitiaComponent component = (MilitiaComponent)party.MobileParty.PartyComponent;
                component.behavior = AiBehavior.GoToSettlement;
            }
        }

        private void raised_militia_follow_on_consequence()
        {
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                MilitiaComponent component = (MilitiaComponent)party.MobileParty.PartyComponent;
                component.behavior = AiBehavior.EscortParty;
            }
        }

        private bool raised_militia_start_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
                if (party.MobileParty.PartyComponent is MilitiaComponent)
                    value = true;

            return value;
        }

        private bool raised_militia_order_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
                if (party.MobileParty.PartyComponent is MilitiaComponent && party.Owner == Hero.MainHero)
                    value = true;

            return value;
        }

        private bool traveller_craftsman_start_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                if (component.popType == PopType.Craftsmen)
                    value = true;
            }

            return value;
        }

        private bool traveller_noble_start_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                if (component.popType == PopType.Nobles)
                    value = true;
            }

            return value;
        }

        private bool traveller_aggression_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                Kingdom partyKingdom = component.OriginSettlement.OwnerClan.Kingdom;
                if (partyKingdom != null)
                    if (Hero.MainHero.Clan.Kingdom == null || component.OriginSettlement.OwnerClan.Kingdom != Hero.MainHero.Clan.Kingdom)
                        value = true;
            }

            return value;
        }

        private bool slavecaravan_neutral_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                Kingdom partyKingdom = component.OriginSettlement.OwnerClan.Kingdom;
                if (partyKingdom != null && component.slaveCaravan)
                    if (Hero.MainHero.Clan.Kingdom == null || component.OriginSettlement.OwnerClan.Kingdom != Hero.MainHero.Clan.Kingdom)
                        value = true;
            }

            return value;
        }

        private bool slavecaravan_amicable_on_condition()
        {
            bool value = false;
            PartyBase party = PlayerEncounter.EncounteredParty;
            if (IsTravellerParty(party))
            {
                PopulationPartyComponent component = (PopulationPartyComponent)party.MobileParty.PartyComponent;
                Kingdom partyKingdom = component.OriginSettlement.OwnerClan.Kingdom;
                Kingdom heroKingdom = Hero.MainHero.Clan.Kingdom;
                if (component.slaveCaravan && ((partyKingdom != null && heroKingdom != null && partyKingdom == heroKingdom) 
                    || (component.OriginSettlement.OwnerClan == Hero.MainHero.Clan))) 
                    value = true;
            }

            return value;
        }
    }
}
