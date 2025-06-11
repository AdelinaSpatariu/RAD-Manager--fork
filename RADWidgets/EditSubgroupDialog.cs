﻿namespace RadWidgets
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.RadToolkit;

	public class EditSubgroupDialog : Dialog
	{
		private readonly RadSubgroupEditor _subgroupEditor;
		private readonly Button _okButton;
		private readonly Button _cancelButton;

		public EditSubgroupDialog(IEngine engine, List<RadSubgroupSelectorItem> existingSubgroups, List<string> labels, RadSubgroupSelectorItem settings,
			string groupNamePlaceHolder, RadGroupOptions parentOptions) : base(engine)
		{
			Title = string.IsNullOrEmpty(settings?.Name) ? "Edit subgroup" : $"Edit subgroup '{settings.Name}'";
			_subgroupEditor = new RadSubgroupEditor(engine, existingSubgroups, parentOptions, labels, groupNamePlaceHolder, settings);
			_subgroupEditor.ValidationChanged += (sender, args) => OnSubgroupEditorValidationChanged();

			_okButton = new Button("Apply")
			{
				Style = ButtonStyle.CallToAction,
				Tooltip = "Apply the specified settings to the subgroup.",
			};
			_okButton.Pressed += (s, e) => Accepted?.Invoke(this, EventArgs.Empty);

			_cancelButton = new Button("Cancel")
			{
				Tooltip = "Do not add the subgroup specified above.",
			};
			_cancelButton.Pressed += (s, e) => Cancelled?.Invoke(this, EventArgs.Empty);

			int row = 0;
			AddSection(_subgroupEditor, 0, 0);
			row += _subgroupEditor.RowCount;

			AddWidget(_cancelButton, row, 0);
			AddWidget(_okButton, row, 1, 1, _subgroupEditor.ColumnCount - 1);
		}

		public event EventHandler Accepted;

		public event EventHandler Cancelled;

		public RadSubgroupSelectorItem Settings
		{
			get
			{
				return _subgroupEditor.Settings;
			}
		}

		private void OnSubgroupEditorValidationChanged()
		{
			if (_subgroupEditor.IsValid)
			{
				_okButton.Tooltip = "Create a subgroup with the specified settings.";
			}
			else
			{
				_okButton.Tooltip = "Create a subgroup with the specified settings. Note that some fields above are invalid and will have to be fixed " +
					$"before submitting the shared model group: {_subgroupEditor.ValidationText}.";
			}
		}
	}
}
