﻿namespace EditRADParameterGroup
{
	using System;
	using System.Linq;
	using RadUtils;
	using RadWidgets;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class EditParameterGroupDialog : Dialog
	{
		private readonly RadGroupEditor _groupEditor;
		private readonly Button _okButton;

		public EditParameterGroupDialog(IEngine engine, RadGroupSettings groupSettings, int dataMinerID) : base(engine)
		{
			ShowScriptAbortPopup = false;
			DataMinerID = dataMinerID;
			Title = $"Edit group '{groupSettings.GroupName}'";

			var groupNames = RadWidgets.Utils.FetchRadGroupNames(engine).Select(id => id.GroupName).Distinct().ToList();
			_groupEditor = new RadGroupEditor(engine, groupNames, groupSettings);
			_groupEditor.ValidationChanged += (sender, args) => OnGroupEditorValidationChanged();

			_okButton = new Button("Apply")
			{
				Style = ButtonStyle.CallToAction,
			};
			_okButton.Pressed += (sender, args) => Accepted?.Invoke(this, EventArgs.Empty);

			var cancelButton = new Button("Cancel");
			cancelButton.Pressed += (sender, args) => Cancelled?.Invoke(this, EventArgs.Empty);

			OnGroupEditorValidationChanged();

			int row = 0;
			AddSection(_groupEditor, row, 0);
			row += _groupEditor.RowCount;

			AddWidget(cancelButton, row, 0, 1, 1);
			AddWidget(_okButton, row, 1, 1, 3);
		}

		public event EventHandler Accepted;

		public event EventHandler Cancelled;

		public int DataMinerID { get; private set; }

		public RadGroupSettings GroupSettings => _groupEditor.Settings;

		private void OnGroupEditorValidationChanged()
		{
			if (_groupEditor.IsValid)
			{
				_okButton.IsEnabled = true;
				_okButton.Tooltip = "Edit the selected parameter group as specified above";
			}
			else
			{
				_okButton.IsEnabled = false;
				_okButton.Tooltip = _groupEditor.ValidationText;
			}
		}
	}
}
