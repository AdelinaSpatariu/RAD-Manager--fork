﻿namespace RemoveRADParameterGroup
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection.Emit;
	using RadUtils;
	using RadWidgets;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.RadToolkit;

	public class LiteRadSubgroupInfo
	{
		public string Name { get; set; }

		public Guid ID { get; set; }

		public string ParameterDescription { get; set; }
	}

	public class RemoveParameterGroupDialog : Dialog
    {
		public const int IndentWidth = 10;
		public const int TextWrapWidth = 150;
		public const int TextWrapIndentWidth = 5;
		private readonly IEngine _engine;
		private readonly ParametersCache _parametersCache;
		private readonly WrappingLabel _label;
		private readonly Button _acceptButton;
		private readonly Button _rejectButton;
		private List<AGroupRemoveSection> _groupRemoveWidgets;
		private List<RadGroupID> _extraGroupsToRemove;

		public RemoveParameterGroupDialog(IEngine engine, List<IRadGroupID> groupIDs) : base(engine)
		{
			_engine = engine ?? throw new ArgumentNullException(nameof(engine));
			_parametersCache = new EngineParametersCache(engine);
			ShowScriptAbortPopup = false;

			_label = new WrappingLabel(TextWrapWidth);

			_rejectButton = new Button()
			{
				MinWidth = 300,
			};
			_rejectButton.Pressed += (sender, args) => Cancelled?.Invoke(this, EventArgs.Empty);

			_acceptButton = new Button()
			{
				Style = ButtonStyle.CallToAction,
				MinWidth = 300,
			};
			_acceptButton.Pressed += (sender, args) => Accepted?.Invoke(this, EventArgs.Empty);

			_groupRemoveWidgets = new List<AGroupRemoveSection>();
			_extraGroupsToRemove = new List<RadGroupID>();
			var parameterGroups = groupIDs.GroupBy(g => new RadGroupID(g.DataMinerID, g.GroupName));
			if (parameterGroups.Count() == 1)
			{
				var g = parameterGroups.First();
				SetWidgetsForSingleGroup(g.Key, g.OfType<RadSubgroupID>());
			}
			else
			{
				SetWidgetsForMultipleGroups(parameterGroups);
			}

			// White spaces to make the indentation look pretty
			var whitespace1 = new WhiteSpace()
			{
				MinWidth = IndentWidth,
			};
			var whitespace2 = new WhiteSpace()
			{
				MinWidth = IndentWidth,
			};
			var whitespace3 = new WhiteSpace()
			{
				MinWidth = _rejectButton.MinWidth - (2 * IndentWidth),
			};

			int row = 0;
			AddWidget(_label, row, 0, 1, 4);
			row++;

			foreach (var groupWidget in _groupRemoveWidgets)
			{
				AddSection(groupWidget, row, 0);
				row += groupWidget.RowCount;
			}

			AddWidget(whitespace1, row, 0);
			AddWidget(whitespace2, row, 1);
			AddWidget(whitespace3, row, 2);
			row += 1;

			AddWidget(_rejectButton, row, 0, 1, 3);
			AddWidget(_acceptButton, row, 3);
		}

		public event EventHandler Accepted;

		public event EventHandler Cancelled;

		public List<RadGroupID> GetGroupsToRemove() => _groupRemoveWidgets.Where(g => g.RemoveGroup).Select(g => g.GroupID).Concat(_extraGroupsToRemove).ToList();

		public List<RadSubgroupID> GetSubgroupsToRemove() => _groupRemoveWidgets.SelectMany(g => g.GetSubgroupsToRemove()).ToList();

		private void SetWidgetsForSingleGroup(RadGroupID groupID, IEnumerable<RadSubgroupID> subgroupIDs)
		{
			Title = "Remove parameter group";

			var groupInfo = FetchGroupInfo(groupID);
			_groupRemoveWidgets = new List<AGroupRemoveSection>();
			_extraGroupsToRemove = new List<RadGroupID>();
			if (groupInfo.Subgroups.Count > 1)
			{
				var matchingSubgroups = GetMatchingSubgroups(groupInfo, subgroupIDs);

				if (matchingSubgroups.Count == 1)
				{
					var subgroup = matchingSubgroups.First();
					if (string.IsNullOrEmpty(subgroup.Name))
						_label.Text = $"Do you want to remove the whole shared model group '{groupID.GroupName}' or only the subgroup on {subgroup.ParameterDescription} from Relational Anomaly Detection?";
					else
						_label.Text = $"Do you want to remove the whole shared model group '{groupID.GroupName}' or only the subgroup '{subgroup.Name}' from Relational Anomaly Detection?";
				}
				else
				{
					_label.Text = $"Do you want to remove the whole shared model group '{groupID.GroupName}' or only the subgroups below from Relational Anomaly Detection?";
				}

				_acceptButton.Text = "OK";
				_rejectButton.Text = "Cancel";

				var section = new SharedModelRemoveRadioButtonList(groupID, matchingSubgroups, matchingSubgroups.Count == groupInfo.Subgroups.Count,
					4, TextWrapWidth, TextWrapIndentWidth);
				_groupRemoveWidgets.Add(section);
			}
			else
			{
				_label.Text = $"Are you sure you want to remove the parameter group '{groupID.GroupName}' from Relational Anomaly Detection?";
				_acceptButton.Text = "Yes";
				_rejectButton.Text = "No";
				_extraGroupsToRemove.Add(groupID);
			}
		}

		private void SetWidgetsForMultipleGroups(IEnumerable<IGrouping<RadGroupID, IRadGroupID>> parameterGroups)
		{
			Title = "Remove parameter groups";
			_label.Text = "Are you sure you want to remove the following parameter groups from Relational Anomaly Detection?";
			_acceptButton.Text = "Yes";
			_rejectButton.Text = "No";

			_groupRemoveWidgets = new List<AGroupRemoveSection>();
			_extraGroupsToRemove = new List<RadGroupID>();
			foreach (var group in parameterGroups)
			{
				var groupInfo = FetchGroupInfo(group.Key);
				if (groupInfo.Subgroups.Count > 1)
				{
					var subgroups = GetMatchingSubgroups(groupInfo, group.OfType<RadSubgroupID>());
					var section = new SharedModelRemoveCheckBox(group.Key, subgroups, subgroups.Count == groupInfo.Subgroups.Count, 4, TextWrapWidth, TextWrapIndentWidth);
					_groupRemoveWidgets.Add(section);
				}
				else
				{
					var checkBox = new GroupRemoveCheckBox(group.Key, 4);
					_groupRemoveWidgets.Add(checkBox);
				}
			}
		}

		private RadGroupInfo FetchGroupInfo(RadGroupID groupID)
		{
			try
			{
				return _engine.GetRadHelper().FetchParameterGroupInfo(groupID.DataMinerID, groupID.GroupName);
			}
			catch (Exception e)
			{
				_engine.Log($"Failed to fetch group info for parameter group {groupID.DataMinerID}/{groupID.GroupName}: {e}");
				throw new DataMinerException("Failed to fetch group info for parameter group", e);
			}
		}

		private List<LiteRadSubgroupInfo> GetMatchingSubgroups(RadGroupInfo groupInfo, IEnumerable<RadSubgroupID> subgroupIDs)
		{
			var subgroupGUIDs = subgroupIDs.Select(id => id.SubgroupID).Where(id => id != null).ToHashSet();
			var subgroupNames = subgroupIDs.Select(id => id.GroupName).Where(n => n != null).ToHashSet();
			var subgroups = groupInfo.Subgroups.Where(s => subgroupGUIDs.Contains(s.ID) || subgroupNames.Contains(s.Name));
			return subgroups.Select(s => new LiteRadSubgroupInfo()
			{
				Name = s.Name,
				ID = s.ID,
				ParameterDescription = string.IsNullOrEmpty(s.Name) ? RadWidgets.Utils.GetParameterDescription(_engine, _parametersCache, s) : string.Empty,
			}).ToList();
		}
	}
}
