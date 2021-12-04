using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Populations.UI
{
    public class PrefabPatches
    {


		[PrefabExtension("TownManagement", "descendant::Widget", "TownManagement")]
		public class PopulationsTownMangementPatch : PrefabExtensionInsertPatch
		{
			public override InsertType Type => InsertType.Replace;
			[PrefabExtensionFileName]
			public string GetContent() => "PopulationTownManagement";
		}

		/*
		[PrefabExtension("TownManagement", "descendant::Widget/Children/BrushWidget/Children/Widget[3]/Children/ListPanel/Children/Widget[2]/Children/ListPanel/Children/ListPanel[1]", "TownManagement")]
		public class CEKTownManagementTitle : PrefabExtensionInsertPatch
		{
			public override InsertType Type => InsertType.Replace;
			[PrefabExtensionText]
			public string GetContent() => "<ListPanel WidthSizePolicy=\"StretchToParent\" HeightSizePolicy="CoverChildren" VerticalAlignment="Top" MarginTop="0" StackLayout.LayoutMethod="VerticalBottomToTop"><Children><!--Projects Title--><RichTextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" Brush="TownManagement.BottomPart.Title.Text" Text="@ProjectsText" /><GridWidget " +
				"DataSource="{AvailableProjects}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" MarginLeft="70" DefaultCellWidth="141" DefaultCellHeight="145" ColumnCount="6" LayoutImp.VerticalLayoutMethod="TopToBottom"><ItemTemplate><ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.HoverBegin="ExecuteSetAsCurrent" Command.HoverEnd="ExecuteResetCurrent" UpdateChildrenStates="true"><Children><DevelopmentItem WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="110" SuggestedHeight="110" HorizontalAlignment="Center" VerticalAlignment="Top" Parameter.IsProgressIndicatorEnabled="true" Parameter.UseSmallVisual="true" /></Children></ButtonWidget></ItemTemplate></GridWidget><!--Daily Defaults Title--><RichTextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" PositionYOffset="-4" Brush="TownManagement.BottomPart.Title.Text" Text="@DailyDefaultsText" /> " +
			"<GridWidget DataSource="{DailyDefaultList}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" DefaultCellWidth="140" DefaultCellHeight="145" ColumnCount="6" LayoutImp.VerticalLayoutMethod="TopToBottom"><ItemTemplate><ButtonWidget DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" PositionYOffset="-5" Command.Click="ExecuteSetAsActiveDevelopment" Command.HoverBegin="ExecuteSetAsCurrent" " +
		"Command.HoverEnd="ExecuteResetCurrent" IsSelected="@IsDefault" UpdateChildrenStates="true"><Children><DailyDefaultItem WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="110" SuggestedHeight="110" HorizontalAlignment="Center" VerticalAlignment="Top" Parameter.UseSmallVisual="true" /></Children></ButtonWidget></ItemTemplate></GridWidget></Children></ListPanel>";
		}
		*/
	}
}
