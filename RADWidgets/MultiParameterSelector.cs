﻿namespace RadWidgets
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.DataTypes;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;

	public class MultiParameterSelector : MultiSelector<ParameterSelectorInfo>
	{
		private bool _parameterAlreadySelected = false;

		public MultiParameterSelector(IEngine engine, IEnumerable<ParameterKey> parameters = null) : base(new ParameterSelector(engine), null, "No parameters selected")
		{
			AddButtonTooltip = "Add the instance specified on the left to the parameter group.";
			RemoveButtonTooltip = "Remove the instance(s) selected on the left from the parameter group.";

			if (parameters != null)
			{
				var selection = new List<ParameterSelectorInfo>(parameters.Count());
				foreach (var parameter in parameters)
				{
					var element = engine.FindElement(parameter.DataMinerID, parameter.ElementID);
					var paramInfo = Utils.FetchParameterInfo(engine, parameter.DataMinerID, parameter.ElementID, parameter.ParameterID);
					selection.Add(new ParameterSelectorInfo()
					{
						ElementName = element?.ElementName ?? "Unknown element",
						ParameterName = paramInfo?.DisplayName ?? "Unknown parameter",
						DataMinerID = parameter.DataMinerID,
						ElementID = parameter.ElementID,
						ParameterID = parameter.ParameterID,
						DisplayKeyFilter = parameter.DisplayInstance,
						MatchingInstances = new List<DynamicTableIndex>() { new DynamicTableIndex(parameter.Instance, parameter.DisplayInstance) },
						IsTableColumn = paramInfo?.IsTableColumn ?? false,
					});
				}

				SetSelected(selection);
			}

			var selector = ItemSelector as ParameterSelector;
			selector.InstanceChanged += (sender, args) => OnChanged();
			Changed += (sender, args) => OnChanged();
		}

		public IEnumerable<ParameterKey> GetSelectedParameters()
		{
			return GetSelected().SelectMany(i => i.GetParameterKeys()).Distinct();
		}

		protected override bool AddItem(ParameterSelectorInfo item)
		{
			var newParameters = item.GetParameterKeys().ToList();
			if (!newParameters.Except(GetSelectedParameters()).Any())
			{
				var selector = ItemSelector as ParameterSelector;
				selector.ValidationState = UIValidationState.Invalid;
				selector.ValidationText = newParameters.Count > 1 ? "Parameters already selected" : "Parameter already selected";
				_parameterAlreadySelected = true;
				return false;
			}

			return base.AddItem(item);
		}

		private void OnChanged()
		{
			if (_parameterAlreadySelected)
			{
				// If an item has been added or removed, then the validation state of the selector should be reset
				var selector = ItemSelector as ParameterSelector;
				selector.ValidationState = UIValidationState.Valid;
				_parameterAlreadySelected = false;
			}
		}
	}
}
