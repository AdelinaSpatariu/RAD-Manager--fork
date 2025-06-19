﻿namespace RadWidgets.Widgets
{
	using System;
	using RadWidgets.Widgets.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public abstract class ParameterSelectorBase<T> : MultiSelectorItemSelector<T>, IValidationWidget where T : MultiSelectorItem
	{
		private readonly IEngine _engine;
		private readonly RadParametersDropDown _parametersDropDown;
		private readonly TextBox _instanceTextBox;
		private UIValidationState _validationState = UIValidationState.Valid;
		private string _validationText = string.Empty;

		protected ParameterSelectorBase(IEngine engine, bool leaveFirstColEmpty)
		{
			_engine = engine;

			var parametersLabel = new Label("Parameter");
			_parametersDropDown = new RadParametersDropDown(engine);
			_parametersDropDown.Changed += (sender, args) => OnSelectedParameterChanged();

			string instanceTooltip = "Specify the display key to include specific cells from the current table column. Use * and ? as wildcards.";
			var instanceLabel = new Label("Display key filter")
			{
				Tooltip = instanceTooltip,
			};
			_instanceTextBox = new TextBox()
			{
				Tooltip = instanceTooltip,
			};
			_instanceTextBox.Changed += (sender, args) => OnInstanceChanged();

			int parametersCol = leaveFirstColEmpty ? 1 : 0;
			int parametersColSpan = leaveFirstColEmpty ? 1 : 2;
			AddWidget(parametersLabel, 0, parametersCol, 1, parametersColSpan);
			AddWidget(_parametersDropDown, 1, parametersCol, 1, parametersColSpan);
			AddWidget(instanceLabel, 0, 2);
			AddWidget(_instanceTextBox, 1, 2);
		}

		public event EventHandler<EventArgs> Changed;

		public virtual UIValidationState ValidationState
		{
			get => _validationState;
			set
			{
				if (_validationState == value)
					return;

				_validationState = value;
				UpdateValidationState();
			}
		}

		public string ValidationText
		{
			get => _validationText;
			set
			{
				if (_validationText == value)
					return;

				_validationText = value;
				UpdateValidationState();
			}
		}

		protected IEngine Engine => _engine;

		protected DropDown<ParameterInfo> ParametersDropDown => _parametersDropDown;

		protected TextBox InstanceTextBox => _instanceTextBox;

		protected bool HasInvalidInstance { get; set; } = false;

		protected void OnSelectedParameterChanged()
		{
			var parameter = _parametersDropDown.Selected;
			if (parameter?.IsTableColumn != true)
			{
				_instanceTextBox.IsEnabled = false;
				_instanceTextBox.Text = string.Empty;
			}
			else
			{
				_instanceTextBox.IsEnabled = true;
			}

			Changed?.Invoke(this, EventArgs.Empty);
			UpdateValidationState();
		}

		protected virtual void UpdateValidationState()
		{
			if (_parametersDropDown.Selected == null)
			{
				_parametersDropDown.ValidationState = UIValidationState.Invalid;
				_parametersDropDown.ValidationText = "Select a valid parameter";
				_instanceTextBox.ValidationState = UIValidationState.Valid;
				_instanceTextBox.ValidationText = string.Empty;
			}
			else if (HasInvalidInstance)
			{
				_instanceTextBox.ValidationState = UIValidationState.Invalid;
				_instanceTextBox.ValidationText = "No matching instances found";
				_parametersDropDown.ValidationState = UIValidationState.Valid;
				_parametersDropDown.ValidationText = string.Empty;
			}
			else
			{
				if (_instanceTextBox.IsEnabled)
				{
					_instanceTextBox.ValidationState = _validationState;
					_instanceTextBox.ValidationText = _validationText;
					_parametersDropDown.ValidationState = UIValidationState.Valid;
					_parametersDropDown.ValidationText = string.Empty;
				}
				else
				{
					_instanceTextBox.ValidationState = UIValidationState.Valid;
					_instanceTextBox.ValidationText = string.Empty;
					_parametersDropDown.ValidationState = _validationState;
					_parametersDropDown.ValidationText = _validationText;
				}
			}
		}

		protected void SetPossibleParameters(int dataMinerID, int elementID)
		{
			_parametersDropDown.SetPossibleParameters(dataMinerID, elementID);
			OnSelectedParameterChanged();
		}

		protected void SetPossibleParameters(string protocolName, string protocolVersion)
		{
			_parametersDropDown.SetPossibleParameters(protocolName, protocolVersion);
			OnSelectedParameterChanged();
		}

		protected void ClearPossibleParameters()
		{
			_parametersDropDown.ClearPossibleParameters();
			OnSelectedParameterChanged();
		}

		private void OnInstanceChanged()
		{
			HasInvalidInstance = false;
			UpdateValidationState();
			Changed?.Invoke(this, EventArgs.Empty);
		}
	}
}
