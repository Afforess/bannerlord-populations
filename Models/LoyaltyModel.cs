﻿
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static Populations.PolicyManager;
using static Populations.PopulationManager;

namespace Populations.Models
{
    class LoyaltyModel : DefaultSettlementLoyaltyModel
    {
        private static readonly float SLAVE_LOYALTY = -0.0005f;
		private static readonly float LOYALTY_FACTOR = 4f;
		private static readonly float NOBLE_LOYALTY_WEIGHT = 3F;

		public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
        {
            if (PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(town.Settlement))
            {
				ExplainedNumber baseResult = CalculateLoyaltyChangeInternal(town, true);
				PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(town.Settlement);
                int slaves = data.GetTypeCount(PopType.Slaves);
                bool surplusExists = PopulationConfig.Instance.PopulationManager.PopSurplusExists(town.Settlement, PopType.Slaves, true);
                baseResult.Add((float)slaves * SLAVE_LOYALTY * (surplusExists ? 1.1f : 1f), new TextObject("Slave population"));

                if (PopulationConfig.Instance.PolicyManager.IsPolicyEnacted(town.Settlement, PolicyType.EXEMPTION))
                {
					float fraction = data.GetCurrentTypeFraction(PopType.Nobles);
                    baseResult.Add(LOYALTY_FACTOR * fraction * NOBLE_LOYALTY_WEIGHT, new TextObject("Nobles exemption policy"));
                }

				if (PopulationConfig.Instance.PolicyManager.GetSettlementTax(town.Settlement) == TaxType.Low)
				{
					float fraction1 = data.GetCurrentTypeFraction(PopType.Craftsmen);
					float fraction2 = data.GetCurrentTypeFraction(PopType.Serfs) * 0.8f;
					baseResult.Add((fraction1 + fraction2) * LOYALTY_FACTOR, new TextObject("Low tax policy"));
				}
				else if (PopulationConfig.Instance.PolicyManager.GetSettlementTax(town.Settlement) == TaxType.High)
				{
					float fraction1 = data.GetCurrentTypeFraction(PopType.Craftsmen);
					float fraction2 = data.GetCurrentTypeFraction(PopType.Serfs) * 0.8f;
					baseResult.Add((fraction1 + fraction2) * LOYALTY_FACTOR * -1f, new TextObject("High tax policy"));
				}


				return baseResult;
            } else return base.CalculateLoyaltyChange(town, includeDescriptions); 
        }



		private ExplainedNumber CalculateLoyaltyChangeInternal(Town town, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			this.GetSettlementLoyaltyChangeDueToFoodStocks(town, ref result);
			this.GetSettlementLoyaltyChangeDueToGovernorCulture(town, ref result);
			this.GetSettlementLoyaltyChangeDueToOwnerCulture(town, ref result);
			this.GetSettlementLoyaltyChangeDueToPolicies(town, ref result);
			this.GetSettlementLoyaltyChangeDueToProjects(town, ref result);
			this.GetSettlementLoyaltyChangeDueToIssues(town, ref result);
			this.GetSettlementLoyaltyChangeDueToSecurity(town, ref result);
			this.GetSettlementLoyaltyChangeDueToNotableRelations(town, ref result);
			this.GetSettlementLoyaltyChangeDueToGovernorPerks(town, ref result);
			this.GetSettlementLoyaltyChangeDueToLoyaltyDrift(town, ref result);
			return result;
		}

		// Token: 0x06002D67 RID: 11623 RVA: 0x000B46D4 File Offset: 0x000B28D4
		private void GetSettlementLoyaltyChangeDueToGovernorPerks(Town town, ref ExplainedNumber explainedNumber)
		{
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Leadership.HeroicLeader, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Charm.NaturalLeader, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Medicine.PhysicianOfPeople, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Athletics.HealthyCitizens, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Bow.Discipline, town, ref explainedNumber);
			if (town.Settlement.Parties.Any((MobileParty x) => x.LeaderHero != null && x.LeaderHero.Clan == town.Settlement.OwnerClan && x.HasPerk(DefaultPerks.Charm.Parade, false)))
			{
				explainedNumber.Add(DefaultPerks.Charm.Parade.PrimaryBonus, ParadePerkBonus, null);
			}
		}

		// Token: 0x06002D68 RID: 11624 RVA: 0x000B477C File Offset: 0x000B297C
		private void GetSettlementLoyaltyChangeDueToNotableRelations(Town town, ref ExplainedNumber explainedNumber)
		{
			float num = 0f;
			foreach (Hero hero in town.Settlement.Notables)
			{
				if (hero.SupporterOf != null)
				{
					if (hero.SupporterOf == town.Settlement.OwnerClan)
					{
						num += 0.5f;
					}
					else if (town.Settlement.OwnerClan.IsAtWarWith(hero.SupporterOf))
					{
						num += -0.5f;
					}
				}
			}
			if (num > 0f)
			{
				explainedNumber.Add(num, NotableText, null);
			}
		}

		private void GetSettlementLoyaltyChangeDueToOwnerCulture(Town town, ref ExplainedNumber explainedNumber)
		{
			if (PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(town.Settlement))
            {
				PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(town.Settlement);
				float factor = data.Assimilation - 1f + (data.Assimilation * 1f);
				float result = (float)LOYALTY_FACTOR * factor;
				explainedNumber.Add(result, new TextObject("Cultural Assimilation"));

				if (town.Governor != null)
					explainedNumber.Add(result * ((town.Governor.Culture == town.Culture) ? 0.1f : -0.1f), GovernorCultureText);


				} else if (town.Settlement.OwnerClan.Culture != town.Settlement.Culture) // vanilla behavior
				explainedNumber.Add(-3f, CultureText, null);
			
		}

		private void GetSettlementLoyaltyChangeDueToPolicies(Town town, ref ExplainedNumber explainedNumber)
		{
			Kingdom kingdom = town.Owner.Settlement.OwnerClan.Kingdom;
			if (kingdom != null)
			{
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.Citizenship))
				{
					if (town.Settlement.OwnerClan.Culture == kingdom.RulingClan.Culture)
					{
						explainedNumber.Add(0.5f, DefaultPolicies.Citizenship.Name, null);
					}
					else
					{
						explainedNumber.Add(-0.5f, DefaultPolicies.Citizenship.Name, null);
					}
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.HuntingRights))
				{
					explainedNumber.Add(-0.2f, DefaultPolicies.HuntingRights.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.GrazingRights))
				{
					explainedNumber.Add(0.5f, DefaultPolicies.GrazingRights.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.TrialByJury))
				{
					explainedNumber.Add(0.5f, DefaultPolicies.TrialByJury.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.ImperialTowns))
				{
					if (kingdom.RulingClan == town.Settlement.OwnerClan)
					{
						explainedNumber.Add(1f, DefaultPolicies.ImperialTowns.Name, null);
					}
					else
					{
						explainedNumber.Add(-0.3f, DefaultPolicies.ImperialTowns.Name, null);
					}
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.ForgivenessOfDebts))
				{
					explainedNumber.Add(2f, DefaultPolicies.ForgivenessOfDebts.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.TribunesOfThePeople))
				{
					explainedNumber.Add(1f, DefaultPolicies.TribunesOfThePeople.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency))
				{
					explainedNumber.Add(-1f, DefaultPolicies.DebasementOfTheCurrency.Name, null);
				}
			}
		}

		private void GetSettlementLoyaltyChangeDueToGovernorCulture(Town town, ref ExplainedNumber explainedNumber)
		{
			if (PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(town.Settlement))
			{
				// Ignore if populated. Governor effect is calculated in GetSettlementLoyaltyChangeDueToOwnerCulture
			}
			else if (town.Governor != null) 
			{
				explainedNumber.Add((town.Governor.Culture == town.Culture) ? 1f : -1f, GovernorCultureText, null);
			}
		}

		private void GetSettlementLoyaltyChangeDueToFoodStocks(Town town, ref ExplainedNumber explainedNumber)
		{
			int foodLimitForBonus = (int)((float)town.FoodStocksUpperLimit() * 0.8f);
			if (town.FoodStocks >= foodLimitForBonus)
				explainedNumber.Add(0.5f, new TextObject("Well fed populace"));
			else if (town.Settlement.IsStarving)
				explainedNumber.AddFactor(-0.3f, StarvingText);
		}

		private void GetSettlementLoyaltyChangeDueToSecurity(Town town, ref ExplainedNumber explainedNumber)
		{
			float value = (town.Security > 50f) ? MBMath.Map(town.Security, 50f, 100f, 0f, 1f) : MBMath.Map(town.Security, 0f, 50f, -2f, 0f);
			explainedNumber.Add(value, SecurityText, null);
		}

		private void GetSettlementLoyaltyChangeDueToProjects(Town town, ref ExplainedNumber explainedNumber)
		{
			if (town.BuildingsInProgress.IsEmpty<Building>() && town.CurrentDefaultBuilding.BuildingType == DefaultBuildingTypes.FestivalsAndGamesDaily)
			{
				BuildingHelper.AddDefaultDailyBonus(town, BuildingEffectEnum.LoyaltyDaily, ref explainedNumber);
			}
			foreach (Building building in town.Buildings)
			{
				float buildingEffectAmount = building.GetBuildingEffectAmount(BuildingEffectEnum.Loyalty);
				if (!building.BuildingType.IsDefaultProject && buildingEffectAmount > 0f)
				{
					explainedNumber.Add(buildingEffectAmount, building.Name, null);
				}
			}
		}


		private void GetSettlementLoyaltyChangeDueToIssues(Town town, ref ExplainedNumber explainedNumber)
		{
			Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementLoyalty, town.Settlement, ref explainedNumber);
		}

		private void GetSettlementLoyaltyChangeDueToLoyaltyDrift(Town town, ref ExplainedNumber explainedNumber)
		{
			explainedNumber.Add(-1f * (town.Loyalty - 50f) * 0.1f, LoyaltyDriftText, null);
		}

		private static readonly TextObject StarvingText = GameTexts.FindText("str_starving", null);
		private static readonly TextObject CultureText = new TextObject("{=YjoXyFDX}Owner Culture", null);
		private static readonly TextObject NotableText = GameTexts.FindText("str_notable_relations", null);
		private static readonly TextObject ParadePerkBonus = new TextObject("{=8aior6PH}Parade perk bonus", null);
		private static readonly TextObject GovernorCultureText = new TextObject("{=5Vo8dJub}Governor's Culture", null);
		private static readonly TextObject SecurityText = GameTexts.FindText("str_security", null);
		private static readonly TextObject LoyaltyDriftText = GameTexts.FindText("str_loyalty_drift", null);
	}
}
