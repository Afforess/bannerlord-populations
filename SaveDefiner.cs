﻿using Populations.Components;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;
using static Populations.PolicyManager;
using static Populations.PopulationManager;

namespace Populations
{
    class SaveDefiner : SaveableTypeDefiner
    {

        public SaveDefiner() : base(82818189)
        {

        }

        protected override void DefineClassTypes()
        {
            base.AddEnumDefinition(typeof(PopType), 1);
            base.AddClassDefinition(typeof(PopulationClass), 2);
            base.AddClassDefinition(typeof(PopulationData), 3);
            base.AddEnumDefinition(typeof(PolicyType), 4);
            base.AddClassDefinition(typeof(PolicyElement), 5);
            base.AddEnumDefinition(typeof(TaxType), 6);
            base.AddEnumDefinition(typeof(MilitiaPolicy), 7);
            base.AddEnumDefinition(typeof(WorkforcePolicy), 8);
            base.AddClassDefinition(typeof(PopulationManager), 9);
            base.AddClassDefinition(typeof(PolicyManager), 10);
            base.AddClassDefinition(typeof(PopulationPartyComponent), 11);
            base.AddClassDefinition(typeof(MilitiaComponent), 12);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(List<PopulationClass>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, PopulationData>));
            base.ConstructContainerDefinition(typeof(List<PolicyElement>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, List<PolicyElement>>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, TaxType>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, MilitiaPolicy>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, WorkforcePolicy>));
        }
    }
}
