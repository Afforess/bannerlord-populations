﻿using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.GameComponents;
using TaleWorlds.Localization;
using static Populations.PopulationManager;
using static Populations.PolicyManager;

namespace Populations.Models
{
    class InfluenceModel : DefaultClanPoliticsModel
    {
        public override ExplainedNumber CalculateInfluenceChange(Clan clan, bool includeDescriptions = false)
        {
            ExplainedNumber baseResult = base.CalculateInfluenceChange(clan, includeDescriptions);

            foreach (Settlement settlement in clan.Settlements)
            {
                if (PopulationConfig.Instance.PopulationManager != null && PopulationConfig.Instance.PopulationManager.IsSettlementPopulated(settlement))
                {
                    PopulationData data = PopulationConfig.Instance.PopulationManager.GetPopData(settlement);
                    int nobles = data.GetTypeCount(PopType.Nobles);
                    if (PopulationConfig.Instance.PopulationManager.PopSurplusExists(settlement, PopType.Nobles, true))
                    {
                        int extra = PopulationConfig.Instance.PopulationManager.GetPopCountOverLimit(settlement, PopType.Nobles);
                        baseResult.Add(((float)extra * -2f) * 0.01f, new TextObject(string.Format("Excess noble population at {0}", settlement.Name)));
                    }
                    baseResult.Add((float)nobles * 0.01f, new TextObject(string.Format("Nobles influence from {0}", settlement.Name)));

                    if (PopulationConfig.Instance.PolicyManager.IsPolicyEnacted(settlement, PolicyType.POP_GROWTH))
                    {
                        if (baseResult.ResultNumber > 1)
                            baseResult.AddFactor(-0.5f, new TextObject(string.Format("Population growth policy at {0}", settlement.Name)));
                        else baseResult.Add(-0.5f, new TextObject(string.Format("Population growth policy at {0}", settlement.Name)));
                    }
                        
                }
            }
                
            return baseResult;
        }

        public int GetMilitiaInfluenceCost(MobileParty party, Settlement settlement, Hero lord)
        {
            float cost = party.MemberRoster.TotalManCount;
            float mediumRelation = 1f;
            if (settlement.Notables != null)
                foreach (Hero notable in settlement.Notables)
                    mediumRelation += ((float)notable.GetRelation(lord) / 5f) * -0.01f;

            return (int)(cost * mediumRelation / 2f);
        }
    }
}
