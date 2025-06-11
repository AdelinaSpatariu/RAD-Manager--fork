﻿namespace RetrainRADModel
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using RadUtils;
	using RadWidgets;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.RadToolkit;

	public class RetrainRadModelDialog : Dialog
	{
		private readonly Button _okButton;
		private readonly MultiTimeRangeSelector _timeRangeSelector;
		private readonly CollapsibleCheckboxList<Guid> _excludedSubgroupsList = null;

		public RetrainRadModelDialog(IEngine engine, RadGroupID groupID, IRadGroupBaseInfo groupInfo) : base(engine)
		{
			ShowScriptAbortPopup = false;
			GroupID = groupID;

			Title = $"Retrain model for parameter group '{groupInfo.GroupName}'";

			var label = new Label($"Retrain the model using the following time ranges with normal behavior:");

			_timeRangeSelector = new MultiTimeRangeSelector(engine);
			_timeRangeSelector.Changed += (sender, args) => OnTimeRangeSelectorChanged();

			if (groupInfo is RadSharedModelGroupInfo sharedModelGroupInfo)
			{
				var parametersCache = new EngineParametersCache(engine);
				var options = sharedModelGroupInfo.Subgroups.Select(s => new Option<Guid>(SubgroupToString(engine, parametersCache, s), s.ID))
					.OrderBy(o => o.DisplayValue);
				_excludedSubgroupsList = new CollapsibleCheckboxList<Guid>(options, _timeRangeSelector.ColumnCount)
				{
					Text = "Exclude specific subgroups",
					Tooltip = "Data from the selected subgroups will not be taken into account while retraining the model. This can be used to exclude subgroups that had anomalous behavior during the " +
						"selected time range.",
					ExpandText = "Select",
					CollapseText = "Unselect all",
				};
			}

			_okButton = new Button("Retrain")
			{
				Style = ButtonStyle.CallToAction,
				Tooltip = "Train the selected parameter group using the trend data in the time ranges selected above.",
			};
			_okButton.Pressed += (sender, args) => Accepted?.Invoke(this, EventArgs.Empty);

			var cancelButton = new Button("Cancel");
			cancelButton.Pressed += (sender, args) => Cancelled?.Invoke(this, EventArgs.Empty);

			OnTimeRangeSelectorChanged();

			int row = 0;
			AddWidget(label, row, 0, 1, _timeRangeSelector.ColumnCount);
			row++;

			AddSection(_timeRangeSelector, row, 0);
			row += _timeRangeSelector.RowCount;

			if (_excludedSubgroupsList != null)
			{
				AddSection(_excludedSubgroupsList, row, 0);
				row += _excludedSubgroupsList.RowCount;
			}

			AddWidget(cancelButton, row, 0, 1, 2);
			AddWidget(_okButton, row, 2, 1, _timeRangeSelector.ColumnCount - 2);
		}

		public event EventHandler Accepted;

		public event EventHandler Cancelled;

		public RadGroupID GroupID { get; private set; }

		public List<Guid> ExcludedSubgroupIDs => _excludedSubgroupsList?.Checked.ToList() ?? new List<Guid>();

		public IEnumerable<TimeRange> GetSelectedTimeRanges()
		{
			return _timeRangeSelector.GetSelected().Select(i => i.TimeRange);
		}

		private static string SubgroupToString(IEngine engine, ParametersCache parametersCache, RadSubgroupInfo s)
		{
			return string.IsNullOrEmpty(s.Name) ? RadWidgets.Utils.GetParameterDescription(engine, parametersCache, s) : s.Name;
		}

		private void OnTimeRangeSelectorChanged()
		{
			_okButton.IsEnabled = _timeRangeSelector.GetSelected().Any();
		}
	}
}
