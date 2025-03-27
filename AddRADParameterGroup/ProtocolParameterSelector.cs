﻿namespace AddParameterGroup
{
	using System;
	using RadWidgets;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;

	public class ProtocolParameterSelectorInfo : MultiSelectorItem
	{
		public string ParameterName { get; set; }

		public int ParameterID { get; set; }

		public int? ParentTableID { get; set; }

		public string DisplayKeyFilter { get; set; }

		public override string GetKey()
		{
			if (!string.IsNullOrEmpty(DisplayKeyFilter))
				return $"{ParameterID}/{DisplayKeyFilter}";
			else
				return $"{ParameterID}";
		}

		public override string GetDisplayValue()
		{
			if (!string.IsNullOrEmpty(DisplayKeyFilter))
				return $"{ParameterName}/{DisplayKeyFilter}";
			else
				return $"{ParameterName}";
		}
	}

	public class ProtocolParameterSelector : ParameterSelectorBase<ProtocolParameterSelectorInfo>
	{
		public ProtocolParameterSelector(string protocolName, string protocolVersion, IEngine engine) : base(engine, false)
		{
			SetProtocol(protocolName, protocolVersion);
		}

		public override ProtocolParameterSelectorInfo SelectedItem
		{
			get
			{
				var parameter = ParametersDropDown.Selected;
				if (parameter == null)
					return null;

				int? parentTableID;
				string displayKeyFilter;
				if (parameter.IsTableColumn)
				{
					parentTableID = parameter.ParentTablePid;
					displayKeyFilter = InstanceTextBox.Text;
				}
				else
				{
					parentTableID = null;
					displayKeyFilter = string.Empty;
				}

				return new ProtocolParameterSelectorInfo
				{
					ParameterName = parameter.DisplayName,
					ParameterID = parameter.ID,
					ParentTableID = parentTableID,
					DisplayKeyFilter = displayKeyFilter,
				};
			}
		}

		public void SetProtocol(string protocolName, string protocolVersion)
		{
			if (string.IsNullOrEmpty(protocolName) || string.IsNullOrEmpty(protocolVersion))
			{
				ClearPossibleParameters();
				return;
			}

			SetPossibleParameters(FetchProtocol(protocolName, protocolVersion));
		}

		private GetProtocolInfoResponseMessage FetchProtocol(string protocolName, string protocolVersion)
		{
			try
			{
				var request = new GetProtocolMessage(protocolName, protocolVersion);
				return Engine.SendSLNetSingleResponseMessage(request) as GetProtocolInfoResponseMessage;
			}
			catch (Exception e)
			{
				Engine.Log($"Could not fetch protocol with name '{protocolName}' and version '{protocolVersion}': {e}");
				return null;
			}
		}
	}
}
