﻿namespace RadWidgets.Widgets.Editors
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using RadUtils;
	using RadWidgets.Widgets;
	using RadWidgets.Widgets.Dialogs;
	using RadWidgets.Widgets.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.RadToolkit;

	public class RadSharedModelGroupEditor : VisibilitySection
	{
		private readonly IEngine _engine;
		private readonly GroupNameSection _groupNameSection;
		private readonly RadGroupOptionsEditor _optionsEditor;
		private readonly Numeric _parametersCountNumeric;
		private readonly RadSubgroupSelector _subgroupSelector;
		private readonly MarginLabel _detailsLabel;
		private List<string> _parameterLabels;
		private List<string> _oldParameterLabels;
		private List<string> _duplicatedParameterLabels;
		private bool _hasMissingParameterLabels;
		private bool _hasWhiteSpaceLabels;

		public RadSharedModelGroupEditor(IEngine engine, List<string> existingGroupNames, ParametersCache parametersCache,
			RadGroupInfo settings = null, Guid? selectedSubgroup = null)
		{
			_engine = engine;
			_groupNameSection = new GroupNameSection(settings?.GroupName, existingGroupNames, 2);
			_groupNameSection.ValidationChanged += (sender, args) => OnGroupNameSectionValidationChanged();

			const string parametersPerSubgroupTooltip = "Each subgroup will have this many parameters";
			var parametersCountLabel = new Label("Number of parameters per subgroup")
			{
				Tooltip = parametersPerSubgroupTooltip,
			};
			var firstSubgroup = settings?.Subgroups?.FirstOrDefault();
			_parametersCountNumeric = new Numeric
			{
				Tooltip = parametersPerSubgroupTooltip,
				Minimum = RadGroupEditor.MIN_PARAMETERS,
				Maximum = RadGroupEditor.MAX_PARAMETERS,
				StepSize = 1,
			};
			_parametersCountNumeric.Changed += (sender, args) => OnParametersCountNumericChanged();

			if (firstSubgroup != null)
			{
				_parameterLabels = firstSubgroup.Parameters.Select(p => p.Label).ToList();
				_parametersCountNumeric.Value = firstSubgroup.Parameters.Count;
			}
			else
			{
				_parameterLabels = Enumerable.Range(0, RadGroupEditor.MIN_PARAMETERS).Select(i => string.Empty).ToList();
				_parametersCountNumeric.Value = RadGroupEditor.MIN_PARAMETERS;
			}

			_oldParameterLabels = new List<string>();

			var parameterLabelsEditorButton = new Button("Edit labels...")
			{
				Tooltip = "Edit the labels of the parameters in the subgroups. These labels are used to identify the parameters in the subgroups.",
			};
			parameterLabelsEditorButton.Pressed += (sender, args) => OnEditLabelsButtonPressed();

			_optionsEditor = new RadGroupOptionsEditor(3, settings?.Options);
			_optionsEditor.Changed += (sender, args) => _subgroupSelector.UpdateParentOptions(_optionsEditor.Options);

			_subgroupSelector = new RadSubgroupSelector(engine, _optionsEditor.Options, _parameterLabels, parametersCache, settings?.Subgroups, selectedSubgroup);
			_subgroupSelector.ValidationChanged += (sender, args) => OnSubgroupSelectorValidationChanged();

			_detailsLabel = new MarginLabel(string.Empty, 3, 10)
			{
				MaxTextWidth = 200,
			};

			UpdateParameterLabelsValid();
			UpdateDetailsLabel();
			UpdateIsValid();

			int row = 0;
			AddSection(_groupNameSection, row, 0);
			row += _groupNameSection.RowCount;

			AddWidget(parametersCountLabel, row, 0);
			AddWidget(_parametersCountNumeric, row, 1);
			AddWidget(parameterLabelsEditorButton, row, 2);
			row++;

			AddSection(_subgroupSelector, row, 0);
			row += _subgroupSelector.RowCount;

			AddSection(_optionsEditor, row, 0);
			row += _optionsEditor.RowCount;

			AddSection(_detailsLabel, row, 0, GetDetailsLabelVisible);
		}

		public event EventHandler ValidationChanged;

		public bool IsValid { get; private set; }

		public string ValidationText { get; private set; }

		public RadGroupSettings GetSettings()
		{
			return new RadGroupSettings(_groupNameSection.GroupName, _optionsEditor.Options, _subgroupSelector.GetSubgroups());
		}

		private void UpdateParameterLabelsValid()
		{
			_hasMissingParameterLabels = _parameterLabels.Any(s => !string.IsNullOrEmpty(s)) && _parameterLabels.Any(s => string.IsNullOrEmpty(s));
			_hasWhiteSpaceLabels = _parameterLabels.Any(s => !string.IsNullOrEmpty(s) && string.IsNullOrWhiteSpace(s));
			_duplicatedParameterLabels = _parameterLabels.Where(s => !string.IsNullOrWhiteSpace(s))
				.GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key)
				.ToList();
		}

		private bool GetDetailsLabelVisible()
		{
			return _groupNameSection.IsValid && (!_subgroupSelector.IsValid || _hasMissingParameterLabels || _hasWhiteSpaceLabels || _duplicatedParameterLabels.Count > 0);
		}

		private void UpdateValidationText()
		{
			if (!_groupNameSection.IsValid)
			{
				ValidationText = "Provide a valid subgroup name.";
			}
			else if (!_subgroupSelector.IsValid)
			{
				ValidationText = "Make sure all subgroup configurations are valid.";
			}
			else if (_hasMissingParameterLabels || _hasWhiteSpaceLabels || _duplicatedParameterLabels.Count > 0)
			{
				ValidationText = "Provide valid labels for the parameters.";
			}
			else
			{
				ValidationText = string.Empty;
			}
		}

		private void UpdateDetailsLabel()
		{
			_detailsLabel.IsVisible = GetDetailsLabelVisible();

			if (!_subgroupSelector.IsValid)
			{
				_detailsLabel.Text = _subgroupSelector.ValidationText;
			}
			else if (_hasMissingParameterLabels)
			{
				_detailsLabel.Text = "Either provide a label for all parameters, or do not provide any labels.";
			}
			else if (_hasWhiteSpaceLabels)
			{
				_detailsLabel.Text = "Parameter labels cannot only contain whitespace characters.";
			}
			else if (_duplicatedParameterLabels.Count > 0)
			{
				if (_duplicatedParameterLabels.Count == 1)
					_detailsLabel.Text = $"Provide a unique label for each parameter. The following label is duplicated: {_duplicatedParameterLabels.First()}";
				else
					_detailsLabel.Text = $"Provide a unique label for each parameter. The following labels are duplicated: {_duplicatedParameterLabels.HumanReadableJoin()}";
			}
			else
			{
				_detailsLabel.Text = string.Empty;
			}
		}

		private void UpdateIsValid()
		{
			UpdateValidationText();
			IsValid = string.IsNullOrEmpty(ValidationText);
			ValidationChanged?.Invoke(this, EventArgs.Empty);
		}

		private void OnEditLabelsButtonPressed()
		{
			InteractiveController app = new InteractiveController(_engine);
			RadLabelEditorDialog dialog = new RadLabelEditorDialog(_engine, _parameterLabels);
			dialog.Accepted += (sender, args) =>
			{
				var d = sender as RadLabelEditorDialog;
				if (d == null)
					return;

				app.Stop();

				_parameterLabels = d.GetLabels();
				_oldParameterLabels = new List<string>();
				_subgroupSelector.UpdateParameterLabels(_parameterLabels);

				UpdateParameterLabelsValid();
				UpdateDetailsLabel();
				UpdateIsValid();
			};
			dialog.Cancelled += (sender, args) => app.Stop();

			app.ShowDialog(dialog);
		}

		private void OnParametersCountNumericChanged()
		{
			int newCount = (int)_parametersCountNumeric.Value;
			if (newCount == _parameterLabels.Count)
				return;

			if (newCount > _parameterLabels.Count)
			{
				int nrFromOldLabels = Math.Min(_oldParameterLabels.Count, newCount - _parameterLabels.Count);
				_parameterLabels.AddRange(_oldParameterLabels.Take(nrFromOldLabels));
				_oldParameterLabels.RemoveRange(0, nrFromOldLabels);
				if (newCount > _parameterLabels.Count)
					_parameterLabels.AddRange(Enumerable.Range(0, newCount - _parameterLabels.Count).Select(i => string.Empty));
			}
			else
			{
				_oldParameterLabels.InsertRange(0, _parameterLabels.Skip(newCount));
				_parameterLabels = _parameterLabels.Take(newCount).ToList();
			}

			_subgroupSelector.UpdateParameterLabels(_parameterLabels);

			UpdateParameterLabelsValid();
			UpdateDetailsLabel();
			UpdateIsValid();
		}

		private void OnGroupNameSectionValidationChanged()
		{
			_detailsLabel.IsVisible = GetDetailsLabelVisible();
			UpdateIsValid();
		}

		private void OnSubgroupSelectorValidationChanged()
		{
			UpdateDetailsLabel();
			UpdateIsValid();
		}
	}
}
